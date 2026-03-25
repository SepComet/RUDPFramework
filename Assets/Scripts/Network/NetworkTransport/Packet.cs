using System;
using System.Linq;
using UnityEngine;

namespace Network.NetworkTransport
{
    public enum PacketType : byte
    {
        Data = 1,
        Ack = 2,
    }

    public struct Packet
    {
        public PacketType Type;
        public uint SequenceNumber;
        public byte[] Data;
        public long Timestamp;

        public byte[] ToBytes()
        {
            var result = new byte[1 + 4 + 8 + Data.Length];
            result[0] = (byte)Type;
            BitConverter.GetBytes(SequenceNumber).CopyTo(result, 1);
            BitConverter.GetBytes(Timestamp).CopyTo(result, 5);
            Data.CopyTo(result, 13);
            return result;
        }

        public static Packet FromBytes(byte[] data)
        {
            return new Packet
            {
                Type = (PacketType)data[0],
                SequenceNumber = BitConverter.ToUInt32(data, 1),
                Timestamp = BitConverter.ToInt64(data, 5),
                Data = new ArraySegment<byte>(data, 5, data.Length - 5).ToArray()
            };
        }

        public static Packet CreateDataPacket(uint seqNum, byte[] data)
        {
            return new Packet
            {
                Type = PacketType.Data,
                SequenceNumber = seqNum,
                Data = data,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static Packet CreateAckPacket(uint seqNum)
        {
            return new Packet
            {
                Type = PacketType.Ack,
                SequenceNumber = seqNum,
                Data = Array.Empty<byte>(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }
}
