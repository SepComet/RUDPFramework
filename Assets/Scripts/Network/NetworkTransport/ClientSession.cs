using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Network.NetworkTransport
{
    public class ClientSession
    {
        private ITransport _transport;

        private IPEndPoint _remote;

        public long LastActivityTs { get; private set; }

        public uint SendSequenceNumber { get; private set; } = 0;

        private int _currentTicks = 0;

        private int _nextSendTicks = 0;

        private int _sendInterval = 10;

        // 重传时间 5s
        private long _retransmitTicks = 5000;

        // 上层交付
        private readonly LinkedList<Packet> _sendQueue = new LinkedList<Packet>();

        // 已发送但未确认
        private readonly LinkedList<Packet> _sendBuffer = new LinkedList<Packet>();

        // 已收到但乱序
        private readonly LinkedList<Packet> _receiveBuffer = new LinkedList<Packet>();

        // 已收到可交付
        private readonly LinkedList<Packet> _receiveQueue = new LinkedList<Packet>();

        private bool _hasReceived = false;

        private uint _expectedAck = 0;

        private readonly object _lockObj = new object();

        public ClientSession(ITransport transport, IPEndPoint remote)
        {
            _transport = transport;
            _remote = remote;
            LastActivityTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public uint GetExpectedAck() => _expectedAck;
        
        public void SetSendInterval(int interval) => _sendInterval = interval;

        private uint GetNextSendSequence()
        {
            lock (_lockObj)
            {
                return SendSequenceNumber++;
            }
        }

        public void Tick(int currentTicks)
        {
            _currentTicks = currentTicks;
            if (_currentTicks >= _nextSendTicks)
            {
                _nextSendTicks = currentTicks + _sendInterval;
                SendPacketInternal();
            }
        }

        public void SendPacket(byte[] data)
        {
            _sendQueue.AddLast(Packet.CreateDataPacket(GetNextSendSequence(), data));
        }

        public List<Packet> ReceivePackets()
        {
            var list = new List<Packet>();
            lock (_lockObj)
            {
                while (_receiveQueue.Count > 0)
                {
                    var packet = _receiveQueue.First.Value;

                    list.Add(packet);
                    _receiveQueue.RemoveFirst();
                }
            }

            return list;
        }

        private void SendPacketInternal()
        {
            if (_hasReceived)
            {
                var packet = Packet.CreateAckPacket(_expectedAck);
                _sendBuffer.AddLast(packet);
                var bytes = packet.ToBytes();
                _transport.SendTo(bytes, _remote);
                _hasReceived = false;
            }

            foreach (var packet in _receiveBuffer)
            {
                if (_currentTicks - packet.Timestamp > _retransmitTicks)
                {
                    var bytes = packet.ToBytes();
                    _transport.SendTo(bytes, _remote);
                }
                else break;
            }

            while (_sendQueue.Count > 0)
            {
                var packet = _sendQueue.First.Value;
                _sendBuffer.AddLast(packet);
                var bytes = packet.ToBytes();
                _transport.SendTo(bytes, _remote);
            }
        }

        public void ReceivePacketsInternal(Packet packet)
        {
            uint seq = packet.SequenceNumber;

            // 是否是按序到达的包
            if (seq == _expectedAck)
            {
                _receiveQueue.AddLast(packet);
                while (_receiveBuffer.Count > 0)
                {
                    var pendingPacket = _receiveBuffer.First.Value;
                    if (seq != pendingPacket.SequenceNumber) break;
                    seq++;
                    _receiveQueue.AddLast(pendingPacket);
                    _receiveBuffer.RemoveFirst();
                }

                _expectedAck = seq + 1;
                _hasReceived = true;
            }
            // 将包按顺序追加在 receivingPackets 后面
            else
            {
                var firstNode = _receiveBuffer.First;
                while (firstNode.Next != null)
                {
                    if (firstNode.Value.SequenceNumber > seq)
                    {
                        var node = new LinkedListNode<Packet>(packet);
                        _receiveBuffer.AddBefore(firstNode, node);
                        break;
                    }

                    firstNode = firstNode.Next;
                }

                if (firstNode == null) _receiveBuffer.AddLast(packet);
            }
        }


        public bool TryProcessReceiveSequence(uint sequenceNumber, out bool shouldDeliver)
        {
            lock (_lockObj)
            {
                LastActivityTs = DateTime.Now;

                if (sequenceNumber == _expectedAck)
                {
                    _expectedAck++;
                    _receivedSequences.Add(sequenceNumber);
                    shouldDeliver = true;
                    return true;
                }
                else if (sequenceNumber < _expectedAck)
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