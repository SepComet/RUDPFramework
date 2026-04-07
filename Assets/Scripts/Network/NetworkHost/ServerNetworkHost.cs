using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ServerAuthoritativeCombatCoordinator authoritativeCombatCoordinator;
        private readonly object playerIdentityGate = new();
        private readonly Dictionary<string, string> playerIdsByPeer = new();
        private readonly Dictionary<string, IPEndPoint> canonicalPeersByPlayerId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> peerKeysByPlayerId = new(StringComparer.Ordinal);

        public ServerNetworkHost(
            ITransport transport,
            INetworkMessageDispatcher dispatcher = null,
            SessionReconnectPolicy reconnectPolicy = null,
            Func<DateTimeOffset> utcNowProvider = null,
            ITransport syncTransport = null,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver = null,
            SyncSequenceTracker syncSequenceTracker = null,
            ServerAuthoritativeMovementConfiguration authoritativeMovement = null,
            ServerAuthoritativeCombatConfiguration authoritativeCombat = null,
            IAuthoritativeMovementWorldValidator authoritativeMovementWorldValidator = null)
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
            var resolvedWorldValidator = authoritativeMovementWorldValidator ?? PermissiveAuthoritativeMovementWorldValidator.Instance;
            authoritativeMovementCoordinator = new ServerAuthoritativeMovementCoordinator(
                this,
                messageManager,
                authoritativeMovement ?? new ServerAuthoritativeMovementConfiguration(),
                resolvedWorldValidator);
            authoritativeCombatCoordinator = new ServerAuthoritativeCombatCoordinator(
                this,
                messageManager,
                authoritativeMovementCoordinator,
                authoritativeCombat ?? new ServerAuthoritativeCombatConfiguration());
            messageManager.RegisterHandler(MessageType.MoveInput, authoritativeMovementCoordinator.HandleMoveInputAsync);
            messageManager.RegisterHandler(MessageType.ShootInput, authoritativeCombatCoordinator.HandleShootInputAsync);
        }

        public MessageManager MessageManager => messageManager;

        public ITransport Transport => transport;

        public ITransport SyncTransport => syncTransport;

        public MultiSessionManager SessionCoordinator { get; }

        public float AuthoritativeMoveSpeed => authoritativeMovementCoordinator.MoveSpeed;

        public TimeSpan AuthoritativeMovementCadence => authoritativeMovementCoordinator.SimulationInterval;

        public IReadOnlyList<ManagedNetworkSession> ManagedSessions => SessionCoordinator.Sessions;

        public IReadOnlyList<ServerAuthoritativeMovementState> AuthoritativeMovementStates => authoritativeMovementCoordinator.States;

        public IReadOnlyList<ServerAuthoritativeCombatState> AuthoritativeCombatStates => authoritativeCombatCoordinator.States;

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
            authoritativeCombatCoordinator.Clear();
            lock (playerIdentityGate)
            {
                playerIdsByPeer.Clear();
                canonicalPeersByPlayerId.Clear();
                peerKeysByPlayerId.Clear();
            }
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

        public bool TryGetAuthoritativeCombatState(IPEndPoint remoteEndPoint, out ServerAuthoritativeCombatState state)
        {
            return authoritativeCombatCoordinator.TryGetState(remoteEndPoint, out state);
        }

        public bool TryGetAcceptedPlayerId(IPEndPoint remoteEndPoint, out string playerId)
        {
            return TryGetKnownPlayerId(remoteEndPoint, out playerId);
        }

        public bool IsAcceptedPlayer(IPEndPoint remoteEndPoint, string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                TryGetKnownPlayerId(remoteEndPoint, out var acceptedPlayerId) &&
                string.Equals(acceptedPlayerId, playerId, StringComparison.Ordinal);
        }

        public bool TryResolveAcceptedPeer(IPEndPoint remoteEndPoint, string playerId, out IPEndPoint acceptedPeer)
        {
            acceptedPeer = null;
            if (remoteEndPoint == null || string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            var normalizedRemoteEndPoint = Normalize(remoteEndPoint);
            var remoteKey = normalizedRemoteEndPoint.ToString();

            lock (playerIdentityGate)
            {
                if (playerIdsByPeer.TryGetValue(remoteKey, out var mappedPlayerId))
                {
                    if (!string.Equals(mappedPlayerId, playerId, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (!canonicalPeersByPlayerId.TryGetValue(playerId, out acceptedPeer))
                    {
                        acceptedPeer = normalizedRemoteEndPoint;
                    }

                    return true;
                }

                if (!canonicalPeersByPlayerId.TryGetValue(playerId, out acceptedPeer))
                {
                    return false;
                }

                playerIdsByPeer[remoteKey] = playerId;
                if (!peerKeysByPlayerId.TryGetValue(playerId, out var peerKeys))
                {
                    peerKeys = new HashSet<string>(StringComparer.Ordinal);
                    peerKeysByPlayerId[playerId] = peerKeys;
                }

                peerKeys.Add(remoteKey);
                return true;
            }
        }

        public bool IsPlayerIdInUse(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            lock (playerIdentityGate)
            {
                foreach (var acceptedPlayerId in playerIdsByPeer.Values)
                {
                    if (string.Equals(acceptedPlayerId, playerId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            foreach (var state in authoritativeMovementCoordinator.States)
            {
                if (string.Equals(state.PlayerId, playerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (var state in authoritativeCombatCoordinator.States)
            {
                if (string.Equals(state.PlayerId, playerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryRefreshAcceptedGameplayActivity(IPEndPoint remoteEndPoint, string playerId)
        {
            if (!TryResolveAcceptedPeer(remoteEndPoint, playerId, out var acceptedPeer))
            {
                return false;
            }

            if (!TryGetSession(acceptedPeer, out var session) ||
                session.SessionManager.State != ConnectionState.LoggedIn)
            {
                return false;
            }

            NotifyInboundActivity(acceptedPeer);
            return true;
        }

        public void NotifyLoginStarted(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyLoginStarted(remoteEndPoint);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyLoginSucceeded(IPEndPoint remoteEndPoint)
        {
            SessionCoordinator.NotifyLoginSucceeded(remoteEndPoint);
            BootstrapAuthoritativeMovementState(remoteEndPoint, null);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyLoginSucceeded(IPEndPoint remoteEndPoint, string playerId)
        {
            NotifyLoginSucceeded(remoteEndPoint, playerId, null);
        }

        public void NotifyLoginSucceeded(IPEndPoint remoteEndPoint, string playerId, float? speed)
        {
            RememberPlayerId(remoteEndPoint, playerId);
            SessionCoordinator.NotifyLoginSucceeded(remoteEndPoint);
            BootstrapAuthoritativeMovementState(remoteEndPoint, speed);
            BootstrapAuthoritativeCombatState(remoteEndPoint, playerId);
            PublishMetricsSessionSnapshot(remoteEndPoint);
        }

        public void NotifyLoginFailed(IPEndPoint remoteEndPoint, string reason = null)
        {
            SessionCoordinator.NotifyLoginFailed(remoteEndPoint, reason);
            ForgetPeerIdentity(remoteEndPoint);
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
            if (!TryGetKnownPlayerId(remoteEndPoint, out var playerId) ||
                !TryResolveAcceptedPeer(remoteEndPoint, playerId, out var acceptedPeer) ||
                !SessionCoordinator.TryGetSession(acceptedPeer, out var session))
            {
                return false;
            }

            var removed = SessionCoordinator.RemoveSession(acceptedPeer, reason);
            if (!removed)
            {
                return false;
            }

            authoritativeMovementCoordinator.RemoveState(acceptedPeer);
            authoritativeCombatCoordinator.RemoveState(acceptedPeer);

            var knownPeerEndpoints = GetKnownPeerEndpointsForPlayerId(playerId);
            ForgetPlayerId(playerId);

            foreach (var peerEndPoint in knownPeerEndpoints)
            {
                SessionCoordinator.RemoveSession(peerEndPoint, reason);
                RemoveTransportPeerSession(transport, peerEndPoint);
                if (syncTransport != null && !ReferenceEquals(syncTransport, transport))
                {
                    RemoveTransportPeerSession(syncTransport, peerEndPoint);
                }
            }

            RecordMetricsSessionSnapshot(transport, "server-host", session, ConnectionState.Disconnected);
            if (syncTransport != null && !ReferenceEquals(syncTransport, transport))
            {
                RecordMetricsSessionSnapshot(syncTransport, "server-host-sync", session, ConnectionState.Disconnected);
            }

            return true;
        }

        private void HandleTransportReceive(byte[] data, IPEndPoint sender)
        {
            SessionCoordinator.ObserveTransportActivity(sender);
            PublishMetricsSessionSnapshot(sender);
        }

        private static void RemoveTransportPeerSession(ITransport transport, IPEndPoint remoteEndPoint)
        {
            if (transport is IPeerSessionTransport peerSessionTransport)
            {
                peerSessionTransport.RemovePeerSession(remoteEndPoint);
            }
        }

        private void BootstrapAuthoritativeMovementState(IPEndPoint remoteEndPoint, float? speed)
        {
            if (!TryGetKnownPlayerId(remoteEndPoint, out var playerId))
            {
                return;
            }

            authoritativeMovementCoordinator.EnsureState(remoteEndPoint, playerId, speed, out _);
        }

        private void BootstrapAuthoritativeCombatState(IPEndPoint remoteEndPoint, string playerId)
        {
            if (!TryGetKnownPlayerId(remoteEndPoint, out var resolvedPlayerId))
            {
                return;
            }

            if (!authoritativeMovementCoordinator.TryGetState(remoteEndPoint, out var movementState))
            {
                return;
            }

            authoritativeCombatCoordinator.BootstrapState(remoteEndPoint, resolvedPlayerId, movementState.Hp, movementState.IsDead);
        }

        private void RememberPlayerId(IPEndPoint remoteEndPoint, string playerId)
        {
            if (remoteEndPoint == null || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            var normalizedRemoteEndPoint = Normalize(remoteEndPoint);
            var key = normalizedRemoteEndPoint.ToString();
            lock (playerIdentityGate)
            {
                playerIdsByPeer[key] = playerId;
                canonicalPeersByPlayerId[playerId] = normalizedRemoteEndPoint;
                if (!peerKeysByPlayerId.TryGetValue(playerId, out var peerKeys))
                {
                    peerKeys = new HashSet<string>(StringComparer.Ordinal);
                    peerKeysByPlayerId[playerId] = peerKeys;
                }

                peerKeys.Add(key);
            }
        }

        private bool TryGetKnownPlayerId(IPEndPoint remoteEndPoint, out string playerId)
        {
            playerId = null;
            if (remoteEndPoint == null)
            {
                return false;
            }

            var key = Normalize(remoteEndPoint).ToString();
            lock (playerIdentityGate)
            {
                return playerIdsByPeer.TryGetValue(key, out playerId);
            }
        }

        private IReadOnlyList<IPEndPoint> GetKnownPeerEndpointsForPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Array.Empty<IPEndPoint>();
            }

            lock (playerIdentityGate)
            {
                if (!peerKeysByPlayerId.TryGetValue(playerId, out var peerKeys))
                {
                    return Array.Empty<IPEndPoint>();
                }

                return peerKeys
                    .Select(ParseEndPoint)
                    .Where(static endpoint => endpoint != null)
                    .ToArray();
            }
        }

        private void ForgetPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            lock (playerIdentityGate)
            {
                if (peerKeysByPlayerId.TryGetValue(playerId, out var peerKeys))
                {
                    foreach (var peerKey in peerKeys)
                    {
                        playerIdsByPeer.Remove(peerKey);
                    }

                    peerKeysByPlayerId.Remove(playerId);
                }

                canonicalPeersByPlayerId.Remove(playerId);
            }
        }

        private void ForgetPeerIdentity(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                return;
            }

            var key = Normalize(remoteEndPoint).ToString();
            lock (playerIdentityGate)
            {
                if (!playerIdsByPeer.TryGetValue(key, out var playerId))
                {
                    return;
                }

                playerIdsByPeer.Remove(key);
                if (peerKeysByPlayerId.TryGetValue(playerId, out var peerKeys))
                {
                    peerKeys.Remove(key);
                    if (peerKeys.Count == 0)
                    {
                        peerKeysByPlayerId.Remove(playerId);
                        canonicalPeersByPlayerId.Remove(playerId);
                    }
                }
            }
        }

        private static IPEndPoint ParseEndPoint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var lastColonIndex = value.LastIndexOf(':');
            if (lastColonIndex <= 0 || lastColonIndex >= value.Length - 1)
            {
                return null;
            }

            var addressText = value.Substring(0, lastColonIndex);
            if (addressText.Length > 1 && addressText[0] == '[' && addressText[addressText.Length - 1] == ']')
            {
                addressText = addressText.Substring(1, addressText.Length - 2);
            }

            return IPAddress.TryParse(addressText, out var address) &&
                int.TryParse(value.Substring(lastColonIndex + 1), out var port)
                ? new IPEndPoint(address, port)
                : null;
        }

        private static IPEndPoint Normalize(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            return new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
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
