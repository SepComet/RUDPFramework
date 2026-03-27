using System;
using System.Net;

namespace Network.NetworkApplication
{
    public sealed class MultiSessionLifecycleEvent
    {
        public MultiSessionLifecycleEvent(
            IPEndPoint remoteEndPoint,
            SessionManager sessionManager,
            SessionLifecycleEvent lifecycleEvent)
        {
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            LifecycleEvent = lifecycleEvent ?? throw new ArgumentNullException(nameof(lifecycleEvent));
        }

        public IPEndPoint RemoteEndPoint { get; }

        public SessionManager SessionManager { get; }

        public SessionLifecycleEvent LifecycleEvent { get; }
    }
}
