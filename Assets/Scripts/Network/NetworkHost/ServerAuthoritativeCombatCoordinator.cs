using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Network.Defines;
using Network.NetworkApplication;

namespace Network.NetworkHost
{
    internal sealed class ServerAuthoritativeCombatCoordinator
    {
        private readonly object gate = new();
        private readonly ServerNetworkHost host;
        private readonly MessageManager messageManager;
        private readonly ServerAuthoritativeMovementCoordinator movementCoordinator;
        private readonly Dictionary<string, ServerAuthoritativeCombatState> statesByPeer = new();
        private readonly ServerAuthoritativeCombatConfiguration configuration;

        public ServerAuthoritativeCombatCoordinator(
            ServerNetworkHost host,
            MessageManager messageManager,
            ServerAuthoritativeMovementCoordinator movementCoordinator,
            ServerAuthoritativeCombatConfiguration configuration)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.messageManager = messageManager ?? throw new ArgumentNullException(nameof(messageManager));
            this.movementCoordinator = movementCoordinator ?? throw new ArgumentNullException(nameof(movementCoordinator));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public IReadOnlyList<ServerAuthoritativeCombatState> States
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

        public Task HandleShootInputAsync(byte[] payload, IPEndPoint sender)
        {
            if (payload == null || sender == null)
            {
                return Task.CompletedTask;
            }

            ShootInput input;
            try
            {
                input = ShootInput.Parser.ParseFrom(payload);
            }
            catch
            {
                return Task.CompletedTask;
            }

            if (!TryValidateAcceptedShot(input, sender, out var acceptedPeer, out var attackerState, out var targetState))
            {
                BroadcastRejectedShot(input);
                return Task.CompletedTask;
            }

            movementCoordinator.TryUpdateState(acceptedPeer, state =>
            {
                state.LastAcceptedShootTick = input.Tick;
            }, out attackerState);

            var wasTargetDead = targetState.IsDead;
            movementCoordinator.TryUpdateStateByPlayerId(targetState.PlayerId, state =>
            {
                state.Hp = Math.Max(0, state.Hp - configuration.DamagePerShot);
                state.IsDead = state.Hp <= 0;
            }, out var updatedTargetState);
            targetState = updatedTargetState;

            UpdateCombatState(attackerState, input.Tick, input.Tick);
            UpdateCombatState(targetState, targetState.LastAcceptedShootTick, input.Tick);

            var hitPosition = new Vector3
            {
                X = targetState.PositionX,
                Y = targetState.PositionY,
                Z = targetState.PositionZ
            };

            // Keep the coordinator as the only gameplay-relevant authoritative combat-result emitter.
            messageManager.BroadcastMessage(new CombatEvent
            {
                Tick = input.Tick,
                EventType = CombatEventType.Hit,
                AttackerId = attackerState.PlayerId,
                TargetId = targetState.PlayerId,
                Damage = 0,
                HitPosition = hitPosition
            }, MessageType.CombatEvent);

            messageManager.BroadcastMessage(new CombatEvent
            {
                Tick = input.Tick,
                EventType = CombatEventType.DamageApplied,
                AttackerId = attackerState.PlayerId,
                TargetId = targetState.PlayerId,
                Damage = configuration.DamagePerShot,
                HitPosition = hitPosition
            }, MessageType.CombatEvent);

            if (!wasTargetDead && targetState.IsDead)
            {
                messageManager.BroadcastMessage(new CombatEvent
                {
                    Tick = input.Tick,
                    EventType = CombatEventType.Death,
                    AttackerId = attackerState.PlayerId,
                    TargetId = targetState.PlayerId,
                    Damage = 0,
                    HitPosition = hitPosition
                }, MessageType.CombatEvent);
            }

            return Task.CompletedTask;
        }

        public bool TryGetState(IPEndPoint remoteEndPoint, out ServerAuthoritativeCombatState state)
        {
            var key = Normalize(remoteEndPoint).ToString();
            lock (gate)
            {
                if (statesByPeer.TryGetValue(key, out var existingState))
                {
                    state = CloneState(existingState);
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
            }
        }

        private bool TryValidateAcceptedShot(
            ShootInput input,
            IPEndPoint sender,
            out IPEndPoint acceptedPeer,
            out ServerAuthoritativeMovementState attackerState,
            out ServerAuthoritativeMovementState targetState)
        {
            acceptedPeer = null;
            attackerState = null;
            targetState = null;

            if (input == null ||
                string.IsNullOrWhiteSpace(input.PlayerId) ||
                !IsFinite(input.DirX) ||
                !IsFinite(input.DirY) ||
                !host.TryResolveAcceptedPeer(sender, input.PlayerId, out acceptedPeer))
            {
                return false;
            }

            var lengthSquared = (input.DirX * input.DirX) + (input.DirY * input.DirY);
            if (lengthSquared <= 0f)
            {
                return false;
            }

            if (!movementCoordinator.TryGetState(acceptedPeer, out attackerState) &&
                !movementCoordinator.EnsureState(acceptedPeer, input.PlayerId, null, out attackerState))
            {
                return false;
            }

            if (!string.Equals(attackerState.PlayerId, input.PlayerId, StringComparison.Ordinal) ||
                attackerState.IsDead ||
                attackerState.Hp <= 0 ||
                input.Tick <= attackerState.LastAcceptedShootTick)
            {
                return false;
            }

            if (!TryResolveTargetState(input, attackerState, out targetState))
            {
                return false;
            }

            return host.TryRefreshAcceptedGameplayActivity(sender, input.PlayerId);
        }

        private bool TryResolveTargetState(
            ShootInput input,
            ServerAuthoritativeMovementState attackerState,
            out ServerAuthoritativeMovementState targetState)
        {
            if (!string.IsNullOrWhiteSpace(input.TargetId))
            {
                if (string.Equals(attackerState.PlayerId, input.TargetId, StringComparison.Ordinal))
                {
                    targetState = null;
                    return false;
                }

                if (!movementCoordinator.TryGetStateByPlayerId(input.TargetId, out targetState) ||
                    targetState.IsDead ||
                    targetState.Hp <= 0)
                {
                    targetState = null;
                    return false;
                }

                return true;
            }

            return TryResolveTargetStateFromAim(input, attackerState, out targetState);
        }

        private bool TryResolveTargetStateFromAim(
            ShootInput input,
            ServerAuthoritativeMovementState attackerState,
            out ServerAuthoritativeMovementState targetState)
        {
            targetState = null;

            var aimLength = MathF.Sqrt((input.DirX * input.DirX) + (input.DirY * input.DirY));
            if (aimLength <= 0f)
            {
                return false;
            }

            var aimX = input.DirX / aimLength;
            var aimY = input.DirY / aimLength;
            var bestAlignment = float.NegativeInfinity;
            var bestDistanceSquared = float.PositiveInfinity;

            foreach (var candidate in movementCoordinator.States)
            {
                if (candidate == null ||
                    string.Equals(candidate.PlayerId, attackerState.PlayerId, StringComparison.Ordinal) ||
                    candidate.IsDead ||
                    candidate.Hp <= 0)
                {
                    continue;
                }

                var offsetX = candidate.PositionX - attackerState.PositionX;
                var offsetY = candidate.PositionZ - attackerState.PositionZ;
                var distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
                if (distanceSquared <= 0f)
                {
                    continue;
                }

                var inverseDistance = 1f / MathF.Sqrt(distanceSquared);
                var alignment = (offsetX * inverseDistance * aimX) + (offsetY * inverseDistance * aimY);
                if (alignment <= 0f)
                {
                    continue;
                }

                if (targetState == null ||
                    alignment > bestAlignment + 0.0001f ||
                    (MathF.Abs(alignment - bestAlignment) <= 0.0001f &&
                        (distanceSquared < bestDistanceSquared - 0.0001f ||
                         (MathF.Abs(distanceSquared - bestDistanceSquared) <= 0.0001f &&
                          string.CompareOrdinal(candidate.PlayerId, targetState.PlayerId) < 0))))
                {
                    targetState = candidate;
                    bestAlignment = alignment;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return targetState != null;
        }

        private void BroadcastRejectedShot(ShootInput input)
        {
            if (input == null)
            {
                return;
            }

            messageManager.BroadcastMessage(new CombatEvent
            {
                Tick = input.Tick,
                EventType = CombatEventType.ShootRejected,
                AttackerId = input.PlayerId ?? string.Empty,
                TargetId = input.TargetId ?? string.Empty,
                Damage = 0,
                HitPosition = new Vector3()
            }, MessageType.CombatEvent);
        }

        private void UpdateCombatState(ServerAuthoritativeMovementState movementState, long acceptedShootTick, long resolvedCombatTick)
        {
            if (movementState == null)
            {
                return;
            }

            var key = Normalize(movementState.RemoteEndPoint).ToString();
            lock (gate)
            {
                if (!statesByPeer.TryGetValue(key, out var combatState))
                {
                    combatState = new ServerAuthoritativeCombatState(
                        Normalize(movementState.RemoteEndPoint),
                        movementState.PlayerId,
                        movementState.Hp,
                        movementState.IsDead);
                    statesByPeer.Add(key, combatState);
                }

                combatState.LastAcceptedShootTick = Math.Max(combatState.LastAcceptedShootTick, acceptedShootTick);
                combatState.LastResolvedCombatTick = Math.Max(combatState.LastResolvedCombatTick, resolvedCombatTick);
                combatState.Hp = movementState.Hp;
                combatState.IsDead = movementState.IsDead;
            }
        }

        private static ServerAuthoritativeCombatState CloneState(ServerAuthoritativeCombatState state)
        {
            return new ServerAuthoritativeCombatState(state.RemoteEndPoint, state.PlayerId, state.Hp, state.IsDead)
            {
                LastAcceptedShootTick = state.LastAcceptedShootTick,
                LastResolvedCombatTick = state.LastResolvedCombatTick
            };
        }

        private static IPEndPoint Normalize(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            return new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
