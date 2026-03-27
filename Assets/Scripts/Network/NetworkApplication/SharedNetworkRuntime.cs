using System;
using System.Threading.Tasks;
using Network.NetworkTransport;

namespace Network.NetworkApplication
{
    public sealed class SharedNetworkRuntime
    {
        public SharedNetworkRuntime(ITransport transport, INetworkMessageDispatcher dispatcher)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            MessageManager = new MessageManager(transport, dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)));
        }

        public ITransport Transport { get; }

        public MessageManager MessageManager { get; }

        public Task StartAsync()
        {
            return Transport.StartAsync();
        }

        public void Stop()
        {
            Transport.Stop();
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return MessageManager.DrainPendingMessagesAsync(maxMessages);
        }
    }
}
