using System;

namespace Network.NetworkApplication
{
    public sealed class SessionReconnectPolicy
    {
        public static SessionReconnectPolicy Default { get; } = new(
            heartbeatInterval: TimeSpan.FromSeconds(2),
            heartbeatTimeout: TimeSpan.FromSeconds(6),
            reconnectDelay: TimeSpan.FromSeconds(1),
            autoReconnect: true);

        public SessionReconnectPolicy(
            TimeSpan heartbeatInterval,
            TimeSpan heartbeatTimeout,
            TimeSpan reconnectDelay,
            bool autoReconnect)
        {
            if (heartbeatInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
            }

            if (heartbeatTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(heartbeatTimeout));
            }

            if (reconnectDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(reconnectDelay));
            }

            HeartbeatInterval = heartbeatInterval;
            HeartbeatTimeout = heartbeatTimeout;
            ReconnectDelay = reconnectDelay;
            AutoReconnect = autoReconnect;
        }

        public TimeSpan HeartbeatInterval { get; }

        public TimeSpan HeartbeatTimeout { get; }

        public TimeSpan ReconnectDelay { get; }

        public bool AutoReconnect { get; }
    }
}
