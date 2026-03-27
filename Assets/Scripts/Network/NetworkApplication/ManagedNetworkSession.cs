using System;
using System.Net;

namespace Network.NetworkApplication
{
    public sealed class ManagedNetworkSession
    {
        public ManagedNetworkSession(IPEndPoint remoteEndPoint, SessionManager sessionManager)
        {
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public IPEndPoint RemoteEndPoint { get; }

        public SessionManager SessionManager { get; }
    }
}
