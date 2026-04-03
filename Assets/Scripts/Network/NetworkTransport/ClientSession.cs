using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace Network.NetworkTransport
{
    public class ClientSession
    {
        public IPEndPoint EndPoint { get; }
        public DateTime LastActivity { get; private set; }

        public uint SendSequenceNumber { get; private set; } = 0;

        //TODO: 数据结构——ConcurrentDictionary
        public ConcurrentDictionary<uint, (Packet packet, DateTime sendTime)> PendingAcks { get; } =
            new ConcurrentDictionary<uint, (Packet packet, DateTime sendTime)>();

        public uint ExpectedReceiveSequence { get; private set; } = 0;
        private HashSet<uint> _receivedSequences { get; } = new HashSet<uint>();

        private readonly object _lockObj = new object();

        public ClientSession(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            LastActivity = DateTime.Now;
        }

        public uint GetNextSendSequence()
        {
            lock (_lockObj)
            {
                return SendSequenceNumber++;
            }
        }

        public bool TryProcessReceiveSequence(uint sequenceNumber, out bool shouldDeliver)
        {
            lock (_lockObj)
            {
                LastActivity = DateTime.Now;

                if (sequenceNumber == ExpectedReceiveSequence)
                {
                    ExpectedReceiveSequence++;
                    _receivedSequences.Add(sequenceNumber);
                    shouldDeliver = true;
                    return true;
                }
                else if (sequenceNumber < ExpectedReceiveSequence)
                {
                    shouldDeliver = false;
                    return _receivedSequences.Contains(sequenceNumber);
                }
                else
                {
                    shouldDeliver = false;
                    return false;
                }
            }
        }
    }
}