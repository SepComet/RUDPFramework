using System;
using System.Net;

namespace Network.NetworkApplication
{
    public interface INetworkMessageLane
    {
        event Action<byte[], IPEndPoint> Received;

        void Send(byte[] data);

        void SendTo(byte[] data, IPEndPoint target);

        void SendToAll(byte[] data);
    }
}
