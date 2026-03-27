using System;
using System.Net;

namespace Network.NetworkApplication
{
    public sealed class ManagedNetworkSession
    {
        public ManagedNetworkSession(
            IPEndPoint remoteEndPoint,
            SessionManager sessionManager,
            ClockSyncState clockSync)
        {
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            ClockSync = clockSync ?? throw new ArgumentNullException(nameof(clockSync));
        }

        public IPEndPoint RemoteEndPoint { get; }

        public SessionManager SessionManager { get; }

        public ClockSyncState ClockSync { get; }
    }
}
