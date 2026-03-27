using System;
using System.Threading.Tasks;
using Network.NetworkApplication;
using Network.NetworkTransport;

namespace Network.NetworkHost
{
    public sealed class ServerNetworkHost
    {
        private readonly SharedNetworkRuntime runtime;

        public ServerNetworkHost(ITransport transport, INetworkMessageDispatcher dispatcher = null)
        {
            runtime = new SharedNetworkRuntime(
                transport ?? throw new ArgumentNullException(nameof(transport)),
                dispatcher ?? new ImmediateNetworkMessageDispatcher());
        }

        public MessageManager MessageManager => runtime.MessageManager;

        public ITransport Transport => runtime.Transport;

        public Task StartAsync()
        {
            return runtime.StartAsync();
        }

        public void Stop()
        {
            runtime.Stop();
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return runtime.DrainPendingMessagesAsync(maxMessages);
        }
    }
}
