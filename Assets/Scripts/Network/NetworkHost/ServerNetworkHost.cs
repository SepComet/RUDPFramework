using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Network.NetworkApplication;
using Network.NetworkTransport;

namespace Network.NetworkHost
{
    public sealed class ServerNetworkHost
    {
        private readonly ITransport transport;
        private readonly MessageManager messageManager;

        public ServerNetworkHost(
            ITransport transport,
            INetworkMessageDispatcher dispatcher = null,
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            SessionCoordinator = new MultiSessionManager(reconnectPolicy, utcNowProvider);
            this.transport.OnReceive += HandleTransportReceive;
            messageManager = new MessageManager(this.transport, dispatcher ?? new ImmediateNetworkMessageDispatcher());
        }

        public MessageManager MessageManager => messageManager;

        public ITransport Transport => transport;

        // Server-side lifecycle entry point: inspect and control per-peer session state here.
        public MultiSessionManager SessionCoordinator { get; }

        public IReadOnlyList<ManagedNetworkSession> ManagedSessions => SessionCoordinator.Sessions;

        public event Action<MultiSessionLifecycleEvent> LifecycleChanged
        {
            add => SessionCoordinator.LifecycleChanged += value;
            remove => SessionCoordinator.LifecycleChanged -= value;
        }

        public Task StartAsync()
        {
            return transport.StartAsync();
        }

        public void Stop()
        {
            transport.Stop();
            SessionCoordinator.RemoveAllSessions("Transport stopped");
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return messageManager.DrainPendingMessagesAsync(maxMessages);
        }

        public void UpdateLifecycle()
        {
            SessionCoordinator.UpdateLifecycle();
        }

        public bool TryGetSession(IPEndPoint remoteEndPoint, out ManagedNetworkSession session)
        {
            return SessionCoordinator.TryGetSession(remoteEndPoint, out session);
        }

        public void NotifyLoginStarted(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyLoginStarted(remoteEndPoint);
        }

        public void NotifyLoginSucceeded(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyLoginSucceeded(remoteEndPoint);
        }

        public void NotifyLoginFailed(IPEndPoint remoteEndPoint, string reason = null)
        {
            SessionCoordinator.NotifyLoginFailed(remoteEndPoint, reason);
        }

        public void NotifyHeartbeatSent(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyHeartbeatSent(remoteEndPoint);
        }

        public void NotifyHeartbeatReceived(IPEndPoint remoteEndPoint, long? serverTick = null)
        {
            SessionCoordinator.NotifyHeartbeatReceived(remoteEndPoint, serverTick);
        }

        public void NotifyInboundActivity(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyInboundActivity(remoteEndPoint);
        }

        public bool RemoveSession(IPEndPoint remoteEndPoint, string reason = null)
        {
            return SessionCoordinator.RemoveSession(remoteEndPoint, reason);
        }

        private void HandleTransportReceive(byte[] _, IPEndPoint sender)
        {
            SessionCoordinator.ObserveTransportActivity(sender);
        }
    }
}

