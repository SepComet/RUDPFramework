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
        private readonly Dictionary<string, ServerAuthoritativeMovementState> statesByPeer = new();
        private long nextBroadcastTick = 1;
        private TimeSpan accumulatedBroadcastTime;

        public ServerAuthoritativeMovementCoordinator(
            ServerNetworkHost host,
            MessageManager messageManager,
            ServerAuthoritativeMovementConfiguration configuration)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.messageManager = messageManager ?? throw new ArgumentNullException(nameof(messageManager));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

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
                !IsFinite(input.MoveX) ||
                !IsFinite(input.MoveY))
            {
                return Task.CompletedTask;
            }

            var normalizedSender = Normalize(sender);
            var key = normalizedSender.ToString();

            lock (gate)
            {
                if (statesByPeer.TryGetValue(key, out var existingState))
                {
                    if (!string.Equals(existingState.PlayerId, input.PlayerId, StringComparison.Ordinal) ||
                        input.Tick <= existingState.LastAcceptedMoveTick)
                    {
                        return Task.CompletedTask;
                    }

                    ApplyInput(existingState, input);
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
                foreach (var state in statesByPeer.Values)
                {
                    IntegrateState(state, elapsed);
                }

                accumulatedBroadcastTime += elapsed;
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

            if (pendingBroadcasts == null)
            {
                return;
            }

            foreach (var pendingBroadcast in pendingBroadcasts)
            {
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
                LastBroadcastTick = state.LastBroadcastTick,
                PositionX = state.PositionX,
                PositionY = state.PositionY,
                PositionZ = state.PositionZ,
                VelocityX = state.VelocityX,
                VelocityY = state.VelocityY,
                VelocityZ = state.VelocityZ,
                Rotation = state.Rotation,
                InputX = state.InputX,
                InputY = state.InputY
            };
        }

        private static void ApplyInput(ServerAuthoritativeMovementState state, MoveInput input)
        {
            state.LastAcceptedMoveTick = input.Tick;
            state.InputX = input.MoveX;
            state.InputY = input.MoveY;

            if (input.MoveX == 0f && input.MoveY == 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            var length = MathF.Sqrt((input.MoveX * input.MoveX) + (input.MoveY * input.MoveY));
            if (length <= 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            state.Rotation = MathF.Atan2(input.MoveY, input.MoveX) * (180f / MathF.PI);
        }

        private void IntegrateState(ServerAuthoritativeMovementState state, TimeSpan elapsed)
        {
            if (state.InputX == 0f && state.InputY == 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            var length = MathF.Sqrt((state.InputX * state.InputX) + (state.InputY * state.InputY));
            if (length <= 0f)
            {
                state.VelocityX = 0f;
                state.VelocityY = 0f;
                state.VelocityZ = 0f;
                return;
            }

            var normalizedX = state.InputX / length;
            var normalizedY = state.InputY / length;
            state.VelocityX = normalizedX * configuration.MoveSpeed;
            state.VelocityY = 0f;
            state.VelocityZ = normalizedY * configuration.MoveSpeed;

            var deltaSeconds = (float)elapsed.TotalSeconds;
            state.PositionX += state.VelocityX * deltaSeconds;
            state.PositionY += state.VelocityY * deltaSeconds;
            state.PositionZ += state.VelocityZ * deltaSeconds;
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
                Hp = state.Hp
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
