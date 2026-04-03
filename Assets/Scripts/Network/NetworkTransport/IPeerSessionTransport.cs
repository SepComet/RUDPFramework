using System.Net;

namespace Network.NetworkTransport
{
    public interface IPeerSessionTransport
    {
        bool RemovePeerSession(IPEndPoint remoteEndPoint);
    }
}
