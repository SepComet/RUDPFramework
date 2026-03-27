using System;
using System.Net;
using Network.NetworkTransport;

namespace Network.NetworkApplication
{
    public sealed class TransportMessageLane : INetworkMessageLane
    {
        private readonly ITransport transport;

        public TransportMessageLane(ITransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.transport.OnReceive += HandleReceive;
        }

        public event Action<byte[], IPEndPoint> Received;

        public void Send(byte[] data)
        {
            transport.Send(data);
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            transport.SendTo(data, target);
        }

        public void SendToAll(byte[] data)
        {
            transport.SendToAll(data);
        }

        private void HandleReceive(byte[] data, IPEndPoint sender)
        {
            Received?.Invoke(data, sender);
        }
    }
}
