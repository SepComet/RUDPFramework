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
            Func<DateTimeOffset> utcNowProvider = null,
            ITransport syncTransport = null,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver = null,
            SyncSequenceTracker syncSequenceTracker = null,
            ClockSyncState clockSync = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            SyncTransport = syncTransport;
            SessionManager = new SessionManager(reconnectPolicy, utcNowProvider);
            ClockSync = clockSync ?? new ClockSyncState(utcNowProvider);
            MessageManager = new MessageManager(
                transport,
                dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)),
                deliveryPolicyResolver ?? new DefaultMessageDeliveryPolicyResolver(),
                syncTransport,
                syncSequenceTracker ?? new SyncSequenceTracker());
        }

        public ITransport Transport { get; }

        public ITransport SyncTransport { get; }

        public MessageManager MessageManager { get; }

        public SessionManager SessionManager { get; }

        public ClockSyncState ClockSync { get; }

        public event Action<SessionLifecycleEvent> LifecycleChanged
        {
            add => SessionManager.LifecycleChanged += value;
            remove => SessionManager.LifecycleChanged -= value;
        }

        public async Task StartAsync()
        {
            await Transport.StartAsync();

            if (SyncTransport != null && !ReferenceEquals(SyncTransport, Transport))
            {
                await SyncTransport.StartAsync();
            }

            SessionManager.NotifyTransportConnected();
            PublishMetricsSessionSnapshot();
        }

        public void Stop()
        {
            Transport.Stop();
            if (SyncTransport != null && !ReferenceEquals(SyncTransport, Transport))
            {
                SyncTransport.Stop();
            }

            SessionManager.NotifyTransportDisconnected("Transport stopped");
            PublishMetricsSessionSnapshot();
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return MessageManager.DrainPendingMessagesAsync(maxMessages);
        }

        public void NotifyLoginStarted()
        {
            SessionManager.NotifyLoginStarted();
            PublishMetricsSessionSnapshot();
        }

        public void NotifyLoginSucceeded()
        {
            SessionManager.NotifyLoginSucceeded();
            PublishMetricsSessionSnapshot();
        }

        public void NotifyLoginFailed(string reason = null)
        {
            SessionManager.NotifyLoginFailed(reason);
            PublishMetricsSessionSnapshot();
        }

        public void NotifyHeartbeatSent()
        {
            SessionManager.NotifyHeartbeatSent();
            PublishMetricsSessionSnapshot();
        }

        public void NotifyHeartbeatReceived(long? serverTick = null)
        {
            SessionManager.NotifyHeartbeatReceived();
            ClockSync.ObserveSample(serverTick);
            PublishMetricsSessionSnapshot();
        }

        public void NotifyInboundActivity()
        {
            SessionManager.NotifyInboundActivity();
            PublishMetricsSessionSnapshot();
        }

        public void UpdateLifecycle()
        {
            SessionManager.Evaluate();
            PublishMetricsSessionSnapshot();
        }

        public void ObserveAuthoritativeState(long? serverTick)
        {
            ClockSync.ObserveSample(serverTick);
            PublishMetricsSessionSnapshot();
        }

        private void PublishMetricsSessionSnapshot()
        {
            RecordMetricsSessionSnapshot(Transport, "shared-runtime", SessionManager, ClockSync, remoteEndPoint: null);

            if (SyncTransport != null && !ReferenceEquals(SyncTransport, Transport))
            {
                RecordMetricsSessionSnapshot(SyncTransport, "shared-runtime-sync", SessionManager, ClockSync, remoteEndPoint: null);
            }
        }

        private static void RecordMetricsSessionSnapshot(
            ITransport transport,
            string scope,
            SessionManager sessionManager,
            ClockSyncState clockSync,
            System.Net.IPEndPoint remoteEndPoint)
        {
            if (transport is not ITransportMetricsSink metricsSink || sessionManager == null)
            {
                return;
            }

            metricsSink.RecordApplicationSessionSnapshot(new TransportApplicationSessionSnapshot
            {
                Scope = scope,
                RemoteEndPoint = remoteEndPoint?.ToString(),
                ConnectionState = sessionManager.State.ToString(),
                CanSendHeartbeat = sessionManager.CanSendHeartbeat,
                LastRoundTripTimeMs = sessionManager.LastRoundTripTime.HasValue
                    ? (long?)System.Math.Max(0d, sessionManager.LastRoundTripTime.Value.TotalMilliseconds)
                    : null,
                LastFailureReason = sessionManager.LastFailureReason,
                LastLivenessUtc = sessionManager.LastLivenessUtc,
                LastHeartbeatSentUtc = sessionManager.LastHeartbeatSentUtc,
                NextReconnectAtUtc = sessionManager.NextReconnectAtUtc,
                CurrentServerTick = clockSync?.CurrentServerTick,
                ObservedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }
}
