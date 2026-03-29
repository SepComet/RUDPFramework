using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkTransport;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Tests.EditMode.Network
{
    internal sealed class ClientGameplayTestHarness
    {
        private readonly string localPlayerId;
        private readonly Dictionary<string, ClientAuthoritativePlayerState> states = new();
        private readonly Dictionary<string, RemotePlayerSnapshotInterpolator> remoteInterpolators = new();

        public ClientGameplayTestHarness(string localPlayerId)
        {
            this.localPlayerId = localPlayerId ?? string.Empty;
        }

        public void Register(MessageManager messageManager)
        {
            messageManager.RegisterHandler(MessageType.PlayerState, (payload, sender) =>
            {
                HandlePlayerState(PlayerState.Parser.ParseFrom(payload), receivedAtSeconds: 0f);
            });
            messageManager.RegisterHandler(MessageType.CombatEvent, (payload, sender) =>
            {
                HandleCombatEvent(CombatEvent.Parser.ParseFrom(payload));
            });
        }

        public bool HandlePlayerState(PlayerState state, float receivedAtSeconds)
        {
            var owner = GetOrCreateOwner(state.PlayerId);
            var accepted = owner.TryAccept(state, out var snapshot);
            if (!accepted)
            {
                return false;
            }

            if (!IsLocalPlayer(state.PlayerId))
            {
                GetOrCreateInterpolator(state.PlayerId).TryAddSnapshot(snapshot, receivedAtSeconds);
            }

            return true;
        }

        public bool HandleCombatEvent(CombatEvent combatEvent)
        {
            if (!ClientCombatEventRouting.TryGetAffectedPlayerId(combatEvent, out var playerId))
            {
                return false;
            }

            var owner = GetOrCreateOwner(playerId);
            return owner.TryApplyCombatEvent(combatEvent, playerId, out _, out _);
        }

        public bool TryGetState(string playerId, out ClientAuthoritativePlayerStateSnapshot snapshot)
        {
            if (states.TryGetValue(playerId, out var owner) && owner.Current != null)
            {
                snapshot = owner.Current;
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetCombatPresentation(string playerId, out ClientCombatPresentationSnapshot snapshot)
        {
            if (states.TryGetValue(playerId, out var owner))
            {
                snapshot = owner.CombatPresentation;
                return true;
            }

            snapshot = ClientCombatPresentationSnapshot.Empty;
            return false;
        }

        public int GetBufferedSnapshotCount(string playerId)
        {
            return remoteInterpolators.TryGetValue(playerId, out var interpolator)
                ? interpolator.BufferedSnapshotCount
                : 0;
        }

        public long GetLatestBufferedTick(string playerId)
        {
            return remoteInterpolators.TryGetValue(playerId, out var interpolator)
                ? interpolator.LatestBufferedTick
                : -1;
        }

        public RemotePlayerInterpolationSample SampleRemote(string playerId, float nowSeconds)
        {
            return GetOrCreateInterpolator(playerId).Sample(nowSeconds);
        }

        private bool IsLocalPlayer(string playerId)
        {
            return string.Equals(playerId, localPlayerId, StringComparison.Ordinal);
        }

        private ClientAuthoritativePlayerState GetOrCreateOwner(string playerId)
        {
            if (!states.TryGetValue(playerId, out var owner))
            {
                owner = new ClientAuthoritativePlayerState();
                states.Add(playerId, owner);
            }

            return owner;
        }

        private RemotePlayerSnapshotInterpolator GetOrCreateInterpolator(string playerId)
        {
            if (!remoteInterpolators.TryGetValue(playerId, out var interpolator))
            {
                interpolator = new RemotePlayerSnapshotInterpolator();
                remoteInterpolators.Add(playerId, interpolator);
            }

            return interpolator;
        }
    }

    internal sealed class GameplayFlowFakeTransport : ITransport
    {
        private readonly List<byte[]> sentMessages = new();
        private readonly List<byte[]> targetMessages = new();
        private readonly List<(byte[] Data, IPEndPoint Target)> targetedSends = new();
        private readonly List<byte[]> broadcastMessages = new();

        public event Action<byte[], IPEndPoint> OnReceive;

        public IReadOnlyList<byte[]> SentMessages => sentMessages;

        public IReadOnlyList<byte[]> TargetMessages => targetMessages;

        public IReadOnlyList<(byte[] Data, IPEndPoint Target)> TargetedSends => targetedSends;

        public IReadOnlyList<byte[]> BroadcastMessages => broadcastMessages;

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
        }

        public void Send(byte[] data)
        {
            sentMessages.Add(Copy(data));
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            var copy = Copy(data);
            targetMessages.Add(copy);
            targetedSends.Add((copy, target));
        }

        public void SendToAll(byte[] data)
        {
            broadcastMessages.Add(Copy(data));
        }

        public void EmitReceive(byte[] data, IPEndPoint sender)
        {
            OnReceive?.Invoke(Copy(data), sender);
        }

        public void ClearOutgoing()
        {
            sentMessages.Clear();
            targetMessages.Clear();
            targetedSends.Clear();
            broadcastMessages.Clear();
        }

        private static byte[] Copy(byte[] data)
        {
            if (data == null)
            {
                return null;
            }

            var copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            return copy;
        }
    }

    internal static class GameplayFlowTestSupport
    {
        public static byte[] BuildEnvelope(MessageType type, IMessage payload)
        {
            return new Envelope
            {
                Type = (int)type,
                Payload = payload.ToByteString()
            }.ToByteArray();
        }

        public static PlayerState CreatePlayerState(string playerId, long tick, Vector3 position, int hp = 100, float rotation = 0f, Vector3? velocity = null)
        {
            var resolvedVelocity = velocity ?? Vector3.zero;
            return new PlayerState
            {
                PlayerId = playerId,
                Tick = tick,
                Position = new global::Network.Defines.Vector3 { X = position.x, Y = position.y, Z = position.z },
                Velocity = new global::Network.Defines.Vector3 { X = resolvedVelocity.x, Y = resolvedVelocity.y, Z = resolvedVelocity.z },
                Rotation = rotation,
                Hp = hp
            };
        }
    }
}
