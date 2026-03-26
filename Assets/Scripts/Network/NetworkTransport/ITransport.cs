using System;
using System.Net;
using System.Threading.Tasks;

namespace Network.NetworkTransport
{
    public interface ITransport
    {
        void Send(byte[] data);
        void SendTo(byte[] data, IPEndPoint target);
        void SendToAll(byte[] data);
        event Action<byte[], IPEndPoint> OnReceive;
        Task StartAsync();
        void Stop();
    }
}
