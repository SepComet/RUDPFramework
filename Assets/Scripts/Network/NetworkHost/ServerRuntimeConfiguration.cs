using System;
using Network.NetworkApplication;
using Network.NetworkTransport;

namespace Network.NetworkHost
{
    public sealed class ServerRuntimeConfiguration
    {
        public ServerRuntimeConfiguration(int reliablePort)
        {
            if (reliablePort <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reliablePort), "Reliable port must be positive.");
            }

            ReliablePort = reliablePort;
            AuthoritativeMovementWorldValidator = PermissiveAuthoritativeMovementWorldValidator.Instance;
        }

        public int ReliablePort { get; }

        public int? SyncPort { get; set; }

        public INetworkMessageDispatcher Dispatcher { get; set; }

        public SessionReconnectPolicy ReconnectPolicy { get; set; }

        public Func<DateTimeOffset> UtcNowProvider { get; set; }

        public IMessageDeliveryPolicyResolver DeliveryPolicyResolver { get; set; }

        public SyncSequenceTracker SyncSequenceTracker { get; set; }

        public Func<int, ITransport> TransportFactory { get; set; }

        public ServerAuthoritativeMovementConfiguration AuthoritativeMovement { get; set; }

        public ServerAuthoritativeCombatConfiguration AuthoritativeCombat { get; set; }

        public IAuthoritativeMovementWorldValidator AuthoritativeMovementWorldValidator { get; set; }

        internal void Validate()
        {
            if (ReliablePort <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ReliablePort), "Reliable port must be positive.");
            }

            if (SyncPort.HasValue)
            {
                if (SyncPort.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(SyncPort), "Sync port must be positive.");
                }

                if (SyncPort.Value == ReliablePort)
                {
                    throw new ArgumentException("Sync port must differ from reliable port.", nameof(SyncPort));
                }
            }

            AuthoritativeMovement?.Validate();
            AuthoritativeCombat?.Validate();
            if (AuthoritativeMovementWorldValidator == null)
            {
                throw new ArgumentNullException(nameof(AuthoritativeMovementWorldValidator));
            }
        }
    }
}
