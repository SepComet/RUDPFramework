using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Network.NetworkApplication
{
    public sealed class MultiSessionManager
    {
        private readonly object gate = new();
        private readonly Dictionary<string, SessionRegistration> sessions = new();
        private readonly SessionReconnectPolicy reconnectPolicy;
        private readonly Func<DateTimeOffset> utcNowProvider;

        public MultiSessionManager(
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null)
        {
            this.reconnectPolicy = reconnectPolicy ?? SessionReconnectPolicy.Default;
            this.utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
        }

        public event Action<MultiSessionLifecycleEvent> LifecycleChanged;

        public int SessionCount
        {
            get
            {
                lock (gate)
                {
                    return sessions.Count;
                }
            }
        }

        public IReadOnlyList<ManagedNetworkSession> Sessions
        {
            get
            {
                lock (gate)
                {
                    return sessions.Values
                        .Select(registration => registration.Session)
                        .ToArray();
                }
            }
        }

        public ManagedNetworkSession GetOrCreateSession(IPEndPoint remoteEndPoint)
        {
            return GetOrCreateRegistration(remoteEndPoint).Session;
        }

        public bool TryGetSession(IPEndPoint remoteEndPoint, out ManagedNetworkSession session)
        {
            var key = BuildKey(remoteEndPoint);

            lock (gate)
            {
                if (sessions.TryGetValue(key, out var registration))
                {
                    session = registration.Session;
                    return true;
                }
            }

            session = null;
            return false;
        }

        public bool TryGetSessionManager(IPEndPoint remoteEndPoint, out SessionManager sessionManager)
        {
            if (TryGetSession(remoteEndPoint, out var session))
            {
                sessionManager = session.SessionManager;
                return true;
            }

            sessionManager = null;
            return false;
        }

        public void ObserveTransportActivity(IPEndPoint remoteEndPoint)
        {
            var sessionManager = GetOrCreateSession(remoteEndPoint).SessionManager;

            if (sessionManager.State == ConnectionState.Disconnected)
            {
                sessionManager.NotifyTransportConnected();
                return;
            }

            sessionManager.NotifyTransportActivity();
        }

        public void NotifyTransportConnected(IPEndPoint remoteEndPoint)
        {
            GetOrCreateSession(remoteEndPoint).SessionManager.NotifyTransportConnected();
        }

        public void NotifyLoginStarted(IPEndPoint remoteEndPoint)
        {
            GetOrCreateSession(remoteEndPoint).SessionManager.NotifyLoginStarted();
        }

        public void NotifyLoginSucceeded(IPEndPoint remoteEndPoint)
        {
            GetOrCreateSession(remoteEndPoint).SessionManager.NotifyLoginSucceeded();
        }

        public void NotifyLoginFailed(IPEndPoint remoteEndPoint, string reason = null)
        {
            GetOrCreateSession(remoteEndPoint).SessionManager.NotifyLoginFailed(reason);
        }

        public void NotifyHeartbeatSent(IPEndPoint remoteEndPoint)
        {
            GetOrCreateSession(remoteEndPoint).SessionManager.NotifyHeartbeatSent();
        }

        public void NotifyHeartbeatReceived(IPEndPoint remoteEndPoint, long? serverTick = null)
        {
            var session = GetOrCreateSession(remoteEndPoint);
            session.SessionManager.NotifyHeartbeatReceived();
            session.ClockSync.ObserveSample(serverTick);
        }

        public void NotifyInboundActivity(IPEndPoint remoteEndPoint)
        {
            GetOrCreateSession(remoteEndPoint).SessionManager.NotifyInboundActivity();
        }

        public void ObserveAuthoritativeState(IPEndPoint remoteEndPoint, long? serverTick)
        {
            GetOrCreateSession(remoteEndPoint).ClockSync.ObserveSample(serverTick);
        }

        public bool RemoveSession(IPEndPoint remoteEndPoint, string reason = null)
        {
            SessionRegistration registration;
            var key = BuildKey(remoteEndPoint);

            lock (gate)
            {
                if (!sessions.TryGetValue(key, out registration))
                {
                    return false;
                }

                sessions.Remove(key);
            }

            registration.Session.SessionManager.NotifyTransportDisconnected(reason);
            registration.Session.SessionManager.LifecycleChanged -= registration.Handler;
            return true;
        }

        public void RemoveAllSessions(string reason = null)
        {
            SessionRegistration[] registrations;

            lock (gate)
            {
                registrations = sessions.Values.ToArray();
                sessions.Clear();
            }

            foreach (var registration in registrations)
            {
                registration.Session.SessionManager.NotifyTransportDisconnected(reason);
                registration.Session.SessionManager.LifecycleChanged -= registration.Handler;
            }
        }

        public void UpdateLifecycle()
        {
            SessionManager[] activeSessions;

            lock (gate)
            {
                activeSessions = sessions.Values
                    .Select(registration => registration.Session.SessionManager)
                    .ToArray();
            }

            foreach (var session in activeSessions)
            {
                session.Evaluate();
            }
        }

        private SessionRegistration GetOrCreateRegistration(IPEndPoint remoteEndPoint)
        {
            var normalizedEndPoint = Normalize(remoteEndPoint);
            var key = normalizedEndPoint.ToString();

            lock (gate)
            {
                if (sessions.TryGetValue(key, out var registration))
                {
                    return registration;
                }

                var sessionManager = new SessionManager(reconnectPolicy, utcNowProvider);
                var clockSync = new ClockSyncState(utcNowProvider);
                var session = new ManagedNetworkSession(normalizedEndPoint, sessionManager, clockSync);
                Action<SessionLifecycleEvent> handler = lifecycleEvent =>
                    LifecycleChanged?.Invoke(new MultiSessionLifecycleEvent(session.RemoteEndPoint, session.SessionManager, lifecycleEvent));

                sessionManager.LifecycleChanged += handler;

                registration = new SessionRegistration(session, handler);
                sessions.Add(key, registration);
                return registration;
            }
        }

        private static string BuildKey(IPEndPoint remoteEndPoint)
        {
            return Normalize(remoteEndPoint).ToString();
        }

        private static IPEndPoint Normalize(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            return new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
        }

        private sealed class SessionRegistration
        {
            public SessionRegistration(ManagedNetworkSession session, Action<SessionLifecycleEvent> handler)
            {
                Session = session;
                Handler = handler;
            }

            public ManagedNetworkSession Session { get; }

            public Action<SessionLifecycleEvent> Handler { get; }
        }
    }
}
