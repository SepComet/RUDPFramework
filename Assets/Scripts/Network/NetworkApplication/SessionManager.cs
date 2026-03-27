using System;

namespace Network.NetworkApplication
{
    public sealed class SessionManager
    {
        private readonly Func<DateTimeOffset> utcNowProvider;
        private DateTimeOffset? lastLivenessUtc;
        private DateTimeOffset? lastHeartbeatSentUtc;
        private DateTimeOffset? nextReconnectAtUtc;

        public SessionManager(
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null)
        {
            ReconnectPolicy = reconnectPolicy ?? SessionReconnectPolicy.Default;
            this.utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
            State = ConnectionState.Disconnected;
        }

        public event Action<SessionLifecycleEvent> LifecycleChanged;

        public ConnectionState State { get; private set; }

        public SessionReconnectPolicy ReconnectPolicy { get; }

        public DateTimeOffset? LastLivenessUtc => lastLivenessUtc;

        public DateTimeOffset? LastHeartbeatSentUtc => lastHeartbeatSentUtc;

        public DateTimeOffset? NextReconnectAtUtc => nextReconnectAtUtc;

        public TimeSpan? LastRoundTripTime { get; private set; }

        public string LastFailureReason { get; private set; }

        public bool CanSendHeartbeat => State == ConnectionState.LoggedIn;

        public bool IsReconnectDue
        {
            get
            {
                if (State != ConnectionState.ReconnectPending || nextReconnectAtUtc == null)
                {
                    return false;
                }

                return utcNowProvider() >= nextReconnectAtUtc.Value;
            }
        }

        public bool IsHeartbeatDue
        {
            get
            {
                if (!CanSendHeartbeat)
                {
                    return false;
                }

                if (lastHeartbeatSentUtc == null)
                {
                    return true;
                }

                return utcNowProvider() - lastHeartbeatSentUtc.Value >= ReconnectPolicy.HeartbeatInterval;
            }
        }

        public void NotifyTransportConnected()
        {
            var now = utcNowProvider();
            lastLivenessUtc = now;
            lastHeartbeatSentUtc = null;
            nextReconnectAtUtc = null;
            LastFailureReason = null;
            TransitionTo(ConnectionState.TransportConnected, SessionEventKind.TransportConnected, now);
        }

        public void NotifyLoginStarted()
        {
            TransitionTo(ConnectionState.LoginPending, SessionEventKind.LoginStarted, utcNowProvider());
        }

        public void NotifyLoginSucceeded()
        {
            var now = utcNowProvider();
            lastLivenessUtc = now;
            LastFailureReason = null;
            TransitionTo(ConnectionState.LoggedIn, SessionEventKind.LoginSucceeded, now);
        }

        public void NotifyLoginFailed(string reason = null)
        {
            LastFailureReason = reason;
            TransitionTo(ConnectionState.LoginFailed, SessionEventKind.LoginFailed, utcNowProvider(), reason);
        }

        public void NotifyHeartbeatSent()
        {
            lastHeartbeatSentUtc = utcNowProvider();
            RaiseEvent(SessionEventKind.HeartbeatSent, State, State, lastHeartbeatSentUtc.Value);
        }

        public void NotifyHeartbeatReceived()
        {
            var now = utcNowProvider();
            lastLivenessUtc = now;
            if (lastHeartbeatSentUtc.HasValue)
            {
                LastRoundTripTime = now - lastHeartbeatSentUtc.Value;
            }

            RaiseEvent(SessionEventKind.HeartbeatReceived, State, State, now);
        }

        public void NotifyInboundActivity()
        {
            lastLivenessUtc = utcNowProvider();
        }

        public void NotifyTransportDisconnected(string reason = null)
        {
            LastFailureReason = reason;
            nextReconnectAtUtc = null;
            TransitionTo(ConnectionState.Disconnected, SessionEventKind.Disconnected, utcNowProvider(), reason);
        }

        public void Evaluate()
        {
            var now = utcNowProvider();

            if (ShouldTimeout(now))
            {
                LastFailureReason = "Heartbeat timeout";
                TransitionTo(ConnectionState.TimedOut, SessionEventKind.TimedOut, now, LastFailureReason);

                if (ReconnectPolicy.AutoReconnect)
                {
                    nextReconnectAtUtc = now + ReconnectPolicy.ReconnectDelay;
                    TransitionTo(ConnectionState.ReconnectPending, SessionEventKind.ReconnectScheduled, now, LastFailureReason);
                }

                return;
            }

            if (State == ConnectionState.ReconnectPending && nextReconnectAtUtc.HasValue && now >= nextReconnectAtUtc.Value)
            {
                TransitionTo(ConnectionState.Reconnecting, SessionEventKind.ReconnectStarted, now, LastFailureReason);
            }
        }

        private bool ShouldTimeout(DateTimeOffset now)
        {
            if (State != ConnectionState.TransportConnected && State != ConnectionState.LoginPending && State != ConnectionState.LoggedIn)
            {
                return false;
            }

            if (!lastLivenessUtc.HasValue)
            {
                return false;
            }

            return now - lastLivenessUtc.Value >= ReconnectPolicy.HeartbeatTimeout;
        }

        private void TransitionTo(
            ConnectionState newState,
            SessionEventKind eventKind,
            DateTimeOffset occurredAtUtc,
            string reason = null)
        {
            if (State == newState)
            {
                RaiseEvent(eventKind, State, State, occurredAtUtc, reason);
                return;
            }

            var previousState = State;
            State = newState;
            RaiseEvent(eventKind, previousState, newState, occurredAtUtc, reason);
        }

        private void RaiseEvent(
            SessionEventKind kind,
            ConnectionState previousState,
            ConnectionState currentState,
            DateTimeOffset occurredAtUtc,
            string reason = null)
        {
            LifecycleChanged?.Invoke(new SessionLifecycleEvent(kind, previousState, currentState, occurredAtUtc, reason));
        }
    }
}
