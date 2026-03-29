using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkTransport;

namespace Network.NetworkHost
{
    public sealed class ServerNetworkHost
    {
        private readonly ITransport transport;
        private readonly ITransport syncTransport;
        private readonly MessageManager messageManager;
        private readonly ServerAuthoritativeMovementCoordinator authoritativeMovementCoordinator;

        public ServerNetworkHost(
            ITransport transport,
            INetworkMessageDispatcher dispatcher = null,
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null,
            ITransport syncTransport = null,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver = null,
            SyncSequenceTracker syncSequenceTracker = null,
            ServerAuthoritativeMovementConfiguration authoritativeMovement = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.syncTransport = syncTransport;
            SessionCoordinator = new MultiSessionManager(reconnectPolicy, utcNowProvider);
            this.transport.OnReceive += HandleTransportReceive;
            if (this.syncTransport != null && !ReferenceEquals(this.syncTransport, this.transport))
            {
                this.syncTransport.OnReceive += HandleTransportReceive;
            }

            messageManager = new MessageManager(
                this.transport,
                dispatcher ?? new ImmediateNetworkMessageDispatcher(),
                deliveryPolicyResolver ?? new DefaultMessageDeliveryPolicyResolver(),
                this.syncTransport,
                syncSequenceTracker ?? new SyncSequenceTracker());
            authoritativeMovementCoordinator = new ServerAuthoritativeMovementCoordinator(
                this,
                messageManager,
                authoritativeMovement ?? new ServerAuthoritativeMovementConfiguration());
            messageManager.RegisterHandler(MessageType.MoveInput, authoritativeMovementCoordinator.HandleMoveInputAsync);
        }

        public MessageManager MessageManager => messageManager;

        public ITransport Transport => transport;

        public ITransport SyncTransport => syncTransport;

        public MultiSessionManager SessionCoordinator { get; }

        public IReadOnlyList<ManagedNetworkSession> ManagedSessions => SessionCoordinator.Sessions;

        public IReadOnlyList<ServerAuthoritativeMovementState> AuthoritativeMovementStates => authoritativeMovementCoordinator.States;

        public event Action<MultiSessionLifecycleEvent> LifecycleChanged
        {
            add => SessionCoordinator.LifecycleChanged += value;
            remove => SessionCoordinator.LifecycleChanged -= value;
        }

        public Task StartAsync()
        {
            var startTask = transport.StartAsync();
            if (syncTransport == null || ReferenceEquals(syncTransport, transport))
            {
                return startTask;
            }

            return StartWithSyncAsync(startTask);
        }

        public void Stop()
        {
            PublishMetricsSessionSnapshots();
            transport.Stop();
            if (syncTransport != null && !ReferenceEquals(syncTransport, transport))
            {
                syncTransport.Stop();
            }

            SessionCoordinator.RemoveAllSessions("Transport stopped");
            authoritativeMovementCoordinator.Clear();
            PublishMetricsSessionSnapshots();
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return messageManager.DrainPendingMessagesAsync(maxMessages);
        }

        public void UpdateLifecycle()
        {
            SessionCoordinator.UpdateLifecycle();
            PublishMetricsSessionSnapshots();
        }

        public void UpdateAuthoritativeMovement(TimeSpan elapsed)
        {
            authoritativeMovementCoordinator.Update(elapsed);
        }

        public bool TryGetSession(IPEndPoint remoteEndPoint, out ManagedNetworkSession session)
        {
            return SessionCoordinator.TryGetSession(remoteEndPoint, out session);
        }

        public bool TryGetAuthoritativeMovementState(IPEndPoint remoteEndPoint, out ServerAuthoritativeMovementState state)
        {
            return authoritativeMovementCoordinator.TryGetState(remoteEndPoint, out state);
        }

        public void NotifyLoginStarted(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyLoginStarted(remoteEndPoint);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyLoginSucceeded(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyLoginSucceeded(remoteEndPoint);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyLoginFailed(IPEndPoint remoteEndPoint, string reason = null)
        {
            SessionCoordinator.NotifyLoginFailed(remoteEndPoint, reason);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyHeartbeatSent(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyHeartbeatSent(remoteEndPoint);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyHeartbeatReceived(IPEndPoint remoteEndPoint, long? serverTick = null)
        {
            SessionCoordinator.NotifyHeartbeatReceived(remoteEndPoint, serverTick);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void ObserveAuthoritativeState(IPEndPoint remoteEndPoint, long? serverTick)
        {
            SessionCoordinator.ObserveAuthoritativeState(remoteEndPoint, serverTick);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyInboundActivity(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyInboundActivity(remoteEndPoint);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public bool RemoveSession(IPEndPoint remoteEndPoint, string reason = null)
        {
            if (!SessionCoordinator.TryGetSession(remoteEndPoint, out var session))
            {
                return false;
            }

            var removed = SessionCoordinator.RemoveSession(remoteEndPoint, reason);
            if (!removed)
            {
                return false;
            }

            authoritativeMovementCoordinator.RemoveState(remoteEndPoint);

            RecordMetricsSessionSnapshot(transport, "server-host", session, ConnectionState.Disconnected);
            if (syncTransport != null && !ReferenceEquals(syncTransport, transport))
            {
                RecordMetricsSessionSnapshot(syncTransport, "server-host-sync", session, ConnectionState.Disconnected);
            }

            return true;
        }

        private void HandleTransportReceive(byte[] _, IPEndPoint sender)
        {
            SessionCoordinator.ObserveTransportActivity(sender);
            PublishMetricsSessionSnapshot(sender);
        }

        private void PublishMetricsSessionSnapshots()
        {
            foreach (var session in ManagedSessions)
            {
                RecordMetricsSessionSnapshot(transport, "server-host", session);
                if (syncTransport != null && !ReferenceEquals(syncTransport, transport))
                {
                    RecordMetricsSessionSnapshot(syncTransport, "server-host-sync", session);
                }
            }
        }

        private void PublishMetricsSessionSnapshot(IPEndPoint remoteEndPoint)
        {
            if (!TryGetSession(remoteEndPoint, out var session))
            {
                return;
            }

            RecordMetricsSessionSnapshot(transport, "server-host", session);
            if (syncTransport != null && !ReferenceEquals(syncTransport, transport))
            {
                RecordMetricsSessionSnapshot(syncTransport, "server-host-sync", session);
            }
        }

        private static void RecordMetricsSessionSnapshot(
            ITransport targetTransport,
            string scope,
            ManagedNetworkSession session,
            ConnectionState? overrideState = null)
        {
            if (targetTransport is not ITransportMetricsSink metricsSink || session == null)
            {
                return;
            }

            metricsSink.RecordApplicationSessionSnapshot(new TransportApplicationSessionSnapshot
            {
                Scope = scope,
                RemoteEndPoint = session.RemoteEndPoint.ToString(),
                ConnectionState = (overrideState ?? session.SessionManager.State).ToString(),
                CanSendHeartbeat = overrideState.HasValue ? overrideState.Value == ConnectionState.LoggedIn : session.SessionManager.CanSendHeartbeat,
                LastRoundTripTimeMs = session.SessionManager.LastRoundTripTime.HasValue
                    ? (long?)Math.Max(0d, session.SessionManager.LastRoundTripTime.Value.TotalMilliseconds)
                    : null,
                LastFailureReason = session.SessionManager.LastFailureReason,
                LastLivenessUtc = session.SessionManager.LastLivenessUtc,
                LastHeartbeatSentUtc = session.SessionManager.LastHeartbeatSentUtc,
                NextReconnectAtUtc = session.SessionManager.NextReconnectAtUtc,
                CurrentServerTick = session.ClockSync.CurrentServerTick,
                ObservedAtUtc = DateTimeOffset.UtcNow
            });
        }

        private async Task StartWithSyncAsync(Task transportStartTask)
        {
            await transportStartTask;
            await syncTransport.StartAsync();
        }
    }
}
