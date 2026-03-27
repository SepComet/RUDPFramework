using System;
using System.Threading.Tasks;
using Network.NetworkTransport;

namespace Network.NetworkApplication
{
    public sealed class SharedNetworkRuntime
    {
        public SharedNetworkRuntime(
            ITransport transport,
            INetworkMessageDispatcher dispatcher,
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            SessionManager = new SessionManager(reconnectPolicy, utcNowProvider);
            MessageManager = new MessageManager(transport, dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)));
        }

        public ITransport Transport { get; }

        public MessageManager MessageManager { get; }

        public SessionManager SessionManager { get; }

        public event Action<SessionLifecycleEvent> LifecycleChanged
        {
            add => SessionManager.LifecycleChanged += value;
            remove => SessionManager.LifecycleChanged -= value;
        }

        public async Task StartAsync()
        {
            await Transport.StartAsync();
            SessionManager.NotifyTransportConnected();
        }

        public void Stop()
        {
            Transport.Stop();
            SessionManager.NotifyTransportDisconnected("Transport stopped");
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return MessageManager.DrainPendingMessagesAsync(maxMessages);
        }

        public void NotifyLoginStarted()
        {
            SessionManager.NotifyLoginStarted();
        }

        public void NotifyLoginSucceeded()
        {
            SessionManager.NotifyLoginSucceeded();
        }

        public void NotifyLoginFailed(string reason = null)
        {
            SessionManager.NotifyLoginFailed(reason);
        }

        public void NotifyHeartbeatSent()
        {
            SessionManager.NotifyHeartbeatSent();
        }

        public void NotifyHeartbeatReceived(long? serverTick = null)
        {
            SessionManager.NotifyHeartbeatReceived(serverTick);
        }

        public void NotifyInboundActivity()
        {
            SessionManager.NotifyInboundActivity();
        }

        public void UpdateLifecycle()
        {
            SessionManager.Evaluate();
        }
    }
}
