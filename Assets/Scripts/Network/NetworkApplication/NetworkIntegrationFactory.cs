using System;
using System.Threading.Tasks;
using Network.NetworkHost;
using Network.NetworkTransport;

namespace Network.NetworkApplication
{
    public static class NetworkIntegrationFactory
    {
        public static SharedNetworkRuntime CreateClientRuntime(
            string serverIp,
            int reliablePort,
            INetworkMessageDispatcher dispatcher,
            int? syncPort = null,
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver = null,
            SyncSequenceTracker syncSequenceTracker = null,
            ClockSyncState clockSync = null,
            Func<string, int, ITransport> transportFactory = null)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            ValidateDualPortConfiguration(reliablePort, syncPort);

            transportFactory ??= static (ip, port) => new KcpTransport(ip, port);

            var reliableTransport = transportFactory(serverIp, reliablePort)
                ?? throw new InvalidOperationException("Reliable transport factory returned null.");
            var syncTransport = syncPort.HasValue
                ? transportFactory(serverIp, syncPort.Value)
                : null;

            if (syncPort.HasValue && syncTransport == null)
            {
                throw new InvalidOperationException("Sync transport factory returned null.");
            }

            return new SharedNetworkRuntime(
                reliableTransport,
                dispatcher,
                reconnectPolicy,
                utcNowProvider,
                syncTransport,
                deliveryPolicyResolver,
                syncSequenceTracker,
                clockSync);
        }

        public static ServerNetworkHost CreateServerHost(
            int reliablePort,
            int? syncPort = null,
            INetworkMessageDispatcher dispatcher = null,
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver = null,
            SyncSequenceTracker syncSequenceTracker = null,
            Func<int, ITransport> transportFactory = null,
            ServerAuthoritativeMovementConfiguration authoritativeMovement = null)
        {
            ValidateDualPortConfiguration(reliablePort, syncPort);

            transportFactory ??= static port => new KcpTransport(port);

            var reliableTransport = transportFactory(reliablePort)
                ?? throw new InvalidOperationException("Reliable transport factory returned null.");
            var syncTransport = syncPort.HasValue
                ? transportFactory(syncPort.Value)
                : null;

            if (syncPort.HasValue && syncTransport == null)
            {
                throw new InvalidOperationException("Sync transport factory returned null.");
            }

            return new ServerNetworkHost(
                reliableTransport,
                dispatcher,
                reconnectPolicy,
                utcNowProvider,
                syncTransport,
                deliveryPolicyResolver,
                syncSequenceTracker,
                authoritativeMovement);
        }

        public static Task<ServerRuntimeHandle> StartServerRuntimeAsync(ServerRuntimeConfiguration configuration)
        {
            return ServerRuntimeEntryPoint.StartAsync(configuration);
        }

        private static void ValidateDualPortConfiguration(int reliablePort, int? syncPort)
        {
            if (reliablePort <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reliablePort), "Reliable port must be positive.");
            }

            if (!syncPort.HasValue)
            {
                return;
            }

            if (syncPort.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(syncPort), "Sync port must be positive.");
            }

            if (syncPort.Value == reliablePort)
            {
                throw new ArgumentException("Sync port must differ from reliable port.", nameof(syncPort));
            }
        }
    }
}
