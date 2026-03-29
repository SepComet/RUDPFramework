using System;
using System.Threading.Tasks;
using Network.NetworkApplication;

namespace Network.NetworkHost
{
    public static class ServerRuntimeEntryPoint
    {
        public static async Task<ServerRuntimeHandle> StartAsync(ServerRuntimeConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Validate();

            var host = NetworkIntegrationFactory.CreateServerHost(
                configuration.ReliablePort,
                configuration.SyncPort,
                configuration.Dispatcher,
                configuration.ReconnectPolicy,
                configuration.UtcNowProvider,
                configuration.DeliveryPolicyResolver,
                configuration.SyncSequenceTracker,
                configuration.TransportFactory,
                configuration.AuthoritativeMovement);

            try
            {
                await host.StartAsync();
                return new ServerRuntimeHandle(host);
            }
            catch
            {
                host.Stop();
                throw;
            }
        }
    }
}
