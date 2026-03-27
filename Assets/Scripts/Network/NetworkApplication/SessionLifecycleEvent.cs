using System;

namespace Network.NetworkApplication
{
    public sealed class SessionLifecycleEvent
    {
        public SessionLifecycleEvent(
            SessionEventKind kind,
            ConnectionState previousState,
            ConnectionState currentState,
            DateTimeOffset occurredAtUtc,
            string reason = null)
        {
            Kind = kind;
            PreviousState = previousState;
            CurrentState = currentState;
            OccurredAtUtc = occurredAtUtc;
            Reason = reason;
        }

        public SessionEventKind Kind { get; }

        public ConnectionState PreviousState { get; }

        public ConnectionState CurrentState { get; }

        public DateTimeOffset OccurredAtUtc { get; }

        public string Reason { get; }
    }
}
