using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Network.Defines;
using Network.NetworkApplication;

namespace Network.NetworkHost
{
    internal sealed class ServerAuthoritativeMovementCoordinator
    {
        private readonly object gate = new();
        private readonly MessageManager messageManager;
        private readonly ServerNetworkHost host;
        private readonly ServerAuthoritativeMovementConfiguration configuration;
        private readonly IAuthoritativeMovementWorldValidator worldValidator;
        private readonly Dictionary<string, ServerAuthoritativeMovementState> statesByPeer = new();
        private long nextBroadcastTick = 1;
        private TimeSpan accumulatedSimulationTime;
        private TimeSpan accumulatedBroadcastTime;

        public ServerAuthoritativeMovementCoordinator(
            ServerNetworkHost host,
            MessageManager messageManager,
            ServerAuthoritativeMovementConfiguration configuration,
            IAuthoritativeMovementWorldValidator worldValidator)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.messageManager = messageManager ?? throw new ArgumentNullException(nameof(messageManager));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.worldValidator = worldValidator ?? throw new ArgumentNullException(nameof(worldValidator));
        }

        public TimeSpan SimulationInterval => configuration.SimulationInterval;

        public float MoveSpeed => configuration.MoveSpeed;

        public IReadOnlyList<ServerAuthoritativeMovementState> States
        {
            get
            {
                lock (gate)
                {
                    return statesByPeer.Values
                        .Select(CloneState)
                        .ToArray();
                }
            }
        }

        public bool EnsureState(IPEndPoint remoteEndPoint, string playerId, float? speed, out ServerAuthoritativeMovementState state)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (string.IsNullOrWhiteSpace(playerId) || !host.IsAcceptedPlayer(remoteEndPoint, playerId))
            {
                state = null;
                return false;
            }

            var normalizedSender = Normalize(remoteEndPoint);
            var key = normalizedSender.ToString();

            lock (gate)
            {
                if (statesByPeer.TryGetValue(key, out var existingState))
                {
                    if (!string.Equals(existingState.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        state = null;
                        return false;
                    }

                    if (speed.HasValue)
                    {
                        existingState.Speed = speed.Value;
                    }

                    state = CloneState(existingState);
                    return true;
                }

                var resolvedSpeed = speed ?? configuration.MoveSpeed;
                var createdState = new ServerAuthoritativeMovementState(
                    normalizedSender,
                    playerId,
                    configuration.DefaultHp,
                    resolvedSpeed);
                statesByPeer.Add(key, createdState);
                state = CloneState(createdState);
                return true;
            }
        }

        public Task HandleMoveInputAsync(byte[] payload, IPEndPoint sender)
        {
            if (payload == null || sender == null)
            {
                return Task.CompletedTask;
            }

            MoveInput input;
            try
            {
                input = MoveInput.Parser.ParseFrom(payload);
            }
            catch
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(input.PlayerId) ||
                !IsFinite(input.TurnInput) ||
                !IsFinite(input.ThrottleInput) ||
                !host.TryResolveAcceptedPeer(sender, input.PlayerId, out var acceptedPeer))
            {
                return Task.CompletedTask;
            }

            var normalizedSender = Normalize(acceptedPeer);
            var key = normalizedSender.ToString();

            lock (gate)
            {
                if (statesByPeer.TryGetValue(key, out var existingState))
                {
                    if (!string.Equals(existingState.PlayerId, input.PlayerId, StringComparison.Ordinal) ||
                        input.Tick <= existingState.LastAcceptedMoveTick ||
                        !host.TryRefreshAcceptedGameplayActivity(sender, input.PlayerId))
                    {
                        return Task.CompletedTask;
                    }

                    ApplyInput(existingState, input);
                    return Task.CompletedTask;
                }

                if (!host.TryRefreshAcceptedGameplayActivity(sender, input.PlayerId))
                {
                    return Task.CompletedTask;
                }

                var state = new ServerAuthoritativeMovementState(
                    normalizedSender,
                    input.PlayerId,
                    configuration.DefaultHp);
                ApplyInput(state, input);
                statesByPeer.Add(key, state);
                return Task.CompletedTask;
            }
        }

        public void Update(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time must be non-negative.");
            }

            List<PendingBroadcast> pendingBroadcasts = null;

            lock (gate)
            {
                accumulatedSimulationTime += elapsed;
                while (accumulatedSimulationTime >= configuration.SimulationInterval)
                {
                    accumulatedSimulationTime -= configuration.SimulationInterval;
                    foreach (var state in statesByPeer.Values)
                    {
                        IntegrateState(state, configuration.SimulationInterval);
                    }

                    foreach (var state in statesByPeer.Values)
                    {
                        state.HasInputThisFrame = false;
                    }

                    accumulatedBroadcastTime += configuration.SimulationInterval;
                    while (accumulatedBroadcastTime >= configuration.BroadcastInterval)
                    {
                        accumulatedBroadcastTime -= configuration.BroadcastInterval;
                        pendingBroadcasts ??= new List<PendingBroadcast>();
                        foreach (var state in statesByPeer.Values)
                        {
                            state.LastBroadcastTick = nextBroadcastTick;
                            pendingBroadcasts.Add(new PendingBroadcast(
                                state.RemoteEndPoint,
                                BuildPlayerState(state, nextBroadcastTick)));
                        }

                        nextBroadcastTick++;
                    }
                }
            }

            if (pendingBroadcasts == null)
            {
                return;
            }

            foreach (var pendingBroadcast in pendingBroadcasts)
            {
                // Keep the coordinator as the only gameplay-relevant PlayerState broadcast path.
                messageManager.BroadcastMessage(pendingBroadcast.PlayerState, MessageType.PlayerState);
                host.ObserveAuthoritativeState(pendingBroadcast.RemoteEndPoint, pendingBroadcast.PlayerState.Tick);
            }
        }

        public bool TryGetState(IPEndPoint remoteEndPoint, out ServerAuthoritativeMovementState state)
        {
            var key = Normalize(remoteEndPoint).ToString();

            lock (gate)
            {
                if (statesByPeer.TryGetValue(key, out state))
                {
                    state = CloneState(state);
                    return true;
                }
            }

            state = null;
            return false;
        }

        public bool TryGetStateByPlayerId(string playerId, out ServerAuthoritativeMovementState state)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                state = null;
                return false;
            }

            lock (gate)
            {
                foreach (var candidate in statesByPeer.Values)
                {
                    if (!string.Equals(candidate.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    state = CloneState(candidate);
                    return true;
                }
            }

            state = null;
            return false;
        }

        public bool TryUpdateState(IPEndPoint remoteEndPoint, Action<ServerAuthoritativeMovementState> updater, out ServerAuthoritativeMovementState state)
        {
            if (updater == null)
            {
                throw new ArgumentNullException(nameof(updater));
            }

            var key = Normalize(remoteEndPoint).ToString();
            lock (gate)
            {
                if (!statesByPeer.TryGetValue(key, out var currentState))
                {
                    state = null;
                    return false;
                }

                updater(currentState);
                state = CloneState(currentState);
                return true;
            }
        }

        public bool TryUpdateStateByPlayerId(string playerId, Action<ServerAuthoritativeMovementState> updater, out ServerAuthoritativeMovementState state)
        {
            if (updater == null)
            {
                throw new ArgumentNullException(nameof(updater));
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                state = null;
                return false;
            }

            lock (gate)
            {
                foreach (var currentState in statesByPeer.Values)
                {
                    if (!string.Equals(currentState.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    updater(currentState);
                    state = CloneState(currentState);
                    return true;
                }
            }

            state = null;
            return false;
        }

        public void RemoveState(IPEndPoint remoteEndPoint)
        {
            var key = Normalize(remoteEndPoint).ToString();
            lock (gate)
            {
                statesByPeer.Remove(key);
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                statesByPeer.Clear();
                accumulatedSimulationTime = TimeSpan.Zero;
                accumulatedBroadcastTime = TimeSpan.Zero;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static IPEndPoint Normalize(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            return new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
        }

        private static ServerAuthoritativeMovementState CloneState(ServerAuthoritativeMovementState state)
        {
            return new ServerAuthoritativeMovementState(state.RemoteEndPoint, state.PlayerId, state.Hp)
            {
                LastAcceptedMoveTick = state.LastAcceptedMoveTick,
                LastAcceptedShootTick = state.LastAcceptedShootTick,
                LastBroadcastTick = state.LastBroadcastTick,
                PositionX = state.PositionX,
                PositionY = state.PositionY,
                PositionZ = state.PositionZ,
                VelocityX = state.VelocityX,
                VelocityY = state.VelocityY,
                VelocityZ = state.VelocityZ,
                Rotation = state.Rotation,
                IsDead = state.IsDead,
                InputX = state.InputX,
                InputY = state.InputY,
                Speed = state.Speed
            };
        }

        private static void ApplyInput(ServerAuthoritativeMovementState state, MoveInput input)
        {
            state.LastAcceptedMoveTick = input.Tick;
            state.InputX = ClampInput(input.TurnInput);
            state.InputY = ClampInput(input.ThrottleInput);
            state.HasInputThisFrame = true;

            if (state.InputY == 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
            }
        }

        private void IntegrateState(ServerAuthoritativeMovementState state, TimeSpan elapsed)
        {
            if (state.IsDead)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            var deltaSeconds = (float)elapsed.TotalSeconds;
            if (deltaSeconds <= 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            if (!state.HasInputThisFrame)
            {
                state.InputX = 0f;
                state.InputY = 0f;
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            var turnInput = ClampInput(state.InputX);
            var throttleInput = ClampInput(state.InputY);
            if (turnInput != 0f)
            {
                state.Rotation = NormalizeDegrees(state.Rotation + (turnInput * configuration.TurnSpeedDegreesPerSecond * deltaSeconds));
            }

            if (throttleInput == 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            var rotationRadians = state.Rotation * (MathF.PI / 180f);
            var forwardX = MathF.Sin(rotationRadians);
            var forwardZ = MathF.Cos(rotationRadians);
            state.VelocityX = forwardX * (throttleInput * state.Speed);
            state.VelocityY = 0f;
            state.VelocityZ = forwardZ * (throttleInput * state.Speed);

            var candidatePositionX = state.PositionX + (state.VelocityX * deltaSeconds);
            var candidatePositionY = state.PositionY + (state.VelocityY * deltaSeconds);
            var candidatePositionZ = state.PositionZ + (state.VelocityZ * deltaSeconds);
            var validationResult = worldValidator.Validate(new AuthoritativeMovementWorldValidationRequest(
                state.RemoteEndPoint,
                state.PlayerId,
                state.PositionX,
                state.PositionY,
                state.PositionZ,
                candidatePositionX,
                candidatePositionY,
                candidatePositionZ,
                state.VelocityX,
                state.VelocityY,
                state.VelocityZ));
            if (!validationResult.IsAllowed)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            state.PositionX = candidatePositionX;
            state.PositionY = candidatePositionY;
            state.PositionZ = candidatePositionZ;
        }

        private static float ClampInput(float value)
        {
            return MathF.Max(-1f, MathF.Min(1f, value));
        }

        private static float NormalizeDegrees(float degrees)
        {
            var normalized = degrees % 360f;
            if (normalized <= -180f)
            {
                normalized += 360f;
            }
            else if (normalized > 180f)
            {
                normalized -= 360f;
            }

            return normalized;
        }

        private static PlayerState BuildPlayerState(ServerAuthoritativeMovementState state, long tick)
        {
            return new PlayerState
            {
                PlayerId = state.PlayerId,
                Tick = tick,
                Position = new Vector3
                {
                    X = state.PositionX,
                    Y = state.PositionY,
                    Z = state.PositionZ
                },
                Velocity = new Vector3
                {
                    X = state.VelocityX,
                    Y = state.VelocityY,
                    Z = state.VelocityZ
                },
                Rotation = state.Rotation,
                Hp = state.Hp,
                AcknowledgedMoveTick = state.LastAcceptedMoveTick
            };
        }

        private sealed class PendingBroadcast
        {
            public PendingBroadcast(IPEndPoint remoteEndPoint, PlayerState playerState)
            {
                RemoteEndPoint = remoteEndPoint;
                PlayerState = playerState;
            }

            public IPEndPoint RemoteEndPoint { get; }

            public PlayerState PlayerState { get; }
        }
    }
}
