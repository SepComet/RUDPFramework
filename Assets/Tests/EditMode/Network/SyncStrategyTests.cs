using System.Collections.Generic;
using System.Net;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkHost;
using Network.NetworkTransport;
using NUnit.Framework;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Tests.EditMode.Network
{
    public class SyncStrategyTests
    {
        [Test]
        public void ClientGameplayInputFlow_StopTransition_EmitsSingleZeroVectorMoveInput()
        {
            var released = ClientGameplayInputFlow.TryCreateMoveInput(
                "player-1",
                8,
                Vector3.zero,
                true,
                out var stopInput);
            var continuedIdle = ClientGameplayInputFlow.TryCreateMoveInput(
                "player-1",
                9,
                Vector3.zero,
                false,
                out var idleInput);

            Assert.That(released, Is.True);
            Assert.That(stopInput, Is.Not.Null);
            Assert.That(stopInput.PlayerId, Is.EqualTo("player-1"));
            Assert.That(stopInput.Tick, Is.EqualTo(8));
            Assert.That(stopInput.TurnInput, Is.EqualTo(0f));
            Assert.That(stopInput.ThrottleInput, Is.EqualTo(0f));
            Assert.That(continuedIdle, Is.False);
            Assert.That(idleInput, Is.Null);
        }

        [Test]
        public void ClientGameplayInputFlow_CreateShootInput_UsesSplitShootMessageFields()
        {
            var shootInput = ClientGameplayInputFlow.CreateShootInput(
                "player-1",
                21,
                new Vector3(2f, 0f, 0f));

            Assert.That(shootInput.PlayerId, Is.EqualTo("player-1"));
            Assert.That(shootInput.Tick, Is.EqualTo(21));
            Assert.That(shootInput.DirX, Is.EqualTo(1f));
            Assert.That(shootInput.DirY, Is.EqualTo(0f));
            Assert.That(shootInput.TargetId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ClientGameplayInputFlow_TryCreateShootInput_LocalFirePathKeepsTargetOptional()
        {
            var created = ClientGameplayInputFlow.TryCreateShootInput(
                "player-1",
                21,
                true,
                new Vector3(2f, 0f, 0f),
                out var shootInput);
            var ignored = ClientGameplayInputFlow.TryCreateShootInput(
                "player-1",
                22,
                false,
                Vector3.forward,
                out var ignoredShootInput);

            Assert.That(created, Is.True);
            Assert.That(shootInput, Is.Not.Null);
            Assert.That(shootInput.PlayerId, Is.EqualTo("player-1"));
            Assert.That(shootInput.Tick, Is.EqualTo(21));
            Assert.That(shootInput.DirX, Is.EqualTo(1f));
            Assert.That(shootInput.DirY, Is.EqualTo(0f));
            Assert.That(shootInput.TargetId, Is.EqualTo(string.Empty));
            Assert.That(ignored, Is.False);
            Assert.That(ignoredShootInput, Is.Null);
        }

        [Test]
        public void ClientPredictionBuffer_AuthoritativeState_PrunesAcknowledgedMoveInputs()
        {
            var buffer = new ClientPredictionBuffer();
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 10, ThrottleInput = 1f });
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 11, ThrottleInput = 1f });
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 12, ThrottleInput = 1f });

            var accepted = buffer.TryApplyAuthoritativeState(
                new PlayerState { PlayerId = "player-1", Tick = 11 },
                out var replayInputs);

            Assert.That(accepted, Is.True);
            Assert.That(buffer.LastAuthoritativeTick, Is.EqualTo(11));
            Assert.That(replayInputs.Count, Is.EqualTo(1));
            Assert.That(replayInputs[0].Input.Tick, Is.EqualTo(12));
            Assert.That(replayInputs[0].SimulatedDurationSeconds, Is.EqualTo(0f));
            Assert.That(buffer.PendingInputs.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClientPredictionBuffer_StaleAuthoritativeState_IsIgnored()
        {
            var buffer = new ClientPredictionBuffer();
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 10, ThrottleInput = 1f });
            buffer.TryApplyAuthoritativeState(new PlayerState { PlayerId = "player-1", Tick = 10 }, out _);

            var accepted = buffer.TryApplyAuthoritativeState(
                new PlayerState { PlayerId = "player-1", Tick = 9 },
                out var replayInputs);

            Assert.That(accepted, Is.False);
            Assert.That(replayInputs, Is.Empty);
            Assert.That(buffer.LastAuthoritativeTick, Is.EqualTo(10));
        }

        [Test]
        public void ClientAuthoritativePlayerState_NewerSnapshot_ReplacesOwnedStateAndPreservesFields()
        {
            var owner = new ClientAuthoritativePlayerState();
            var accepted = owner.TryAccept(
                new PlayerState
                {
                    PlayerId = "player-1",
                    Tick = 14,
                    Position = new global::Network.Defines.Vector3 { X = 5f, Y = 0f, Z = -3f },
                    Velocity = new global::Network.Defines.Vector3 { X = 1.5f, Y = 0f, Z = 0.25f },
                    Rotation = 90f,
                    Hp = 73
                },
                out var snapshot);

            Assert.That(accepted, Is.True);
            Assert.That(owner.Current, Is.SameAs(snapshot));
            Assert.That(snapshot.PlayerId, Is.EqualTo("player-1"));
            Assert.That(snapshot.Tick, Is.EqualTo(14));
            Assert.That(snapshot.Position, Is.EqualTo(new Vector3(5f, 0f, -3f)));
            Assert.That(snapshot.Velocity, Is.EqualTo(new Vector3(1.5f, 0f, 0.25f)));
            Assert.That(snapshot.Rotation, Is.EqualTo(90f));
            Assert.That(snapshot.RotationQuaternion.eulerAngles.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(snapshot.Hp, Is.EqualTo(73));
        }

        [Test]
        public void ClientAuthoritativePlayerState_StaleSnapshot_IsRejected()
        {
            var owner = new ClientAuthoritativePlayerState();
            owner.TryAccept(new PlayerState { PlayerId = "player-1", Tick = 10, Hp = 95 }, out var current);

            var accepted = owner.TryAccept(
                new PlayerState { PlayerId = "player-1", Tick = 9, Hp = 10 },
                out var staleResult);

            Assert.That(accepted, Is.False);
            Assert.That(owner.Current, Is.SameAs(current));
            Assert.That(staleResult, Is.SameAs(current));
            Assert.That(owner.Current.Hp, Is.EqualTo(95));
        }

        [Test]
        public void ClientAuthoritativePlayerStateSnapshot_ClonesSourceMessage()
        {
            var source = new PlayerState
            {
                PlayerId = "player-1",
                Tick = 3,
                Position = new global::Network.Defines.Vector3 { X = 1f, Y = 0f, Z = 2f },
                Rotation = 45f,
                Hp = 88
            };

            var snapshot = new ClientAuthoritativePlayerStateSnapshot(source);
            source.Tick = 4;
            source.Hp = 10;
            source.Position.X = 99f;

            Assert.That(snapshot.Tick, Is.EqualTo(3));
            Assert.That(snapshot.Hp, Is.EqualTo(88));
            Assert.That(snapshot.Position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(snapshot.SourceState.Tick, Is.EqualTo(3));
            Assert.That(snapshot.SourceState.Hp, Is.EqualTo(88));
        }

        [Test]
        public void ClientCombatEventRouting_DamageEvent_RoutesToTargetPlayer()
        {
            var routed = ClientCombatEventRouting.TryGetAffectedPlayerId(
                new CombatEvent
                {
                    EventType = CombatEventType.DamageApplied,
                    AttackerId = "player-a",
                    TargetId = "player-b",
                    Damage = 15
                },
                out var playerId);

            Assert.That(routed, Is.True);
            Assert.That(playerId, Is.EqualTo("player-b"));
        }

        [Test]
        public void ClientCombatEventRouting_ShootRejected_RoutesToAttackerPlayer()
        {
            var routed = ClientCombatEventRouting.TryGetAffectedPlayerId(
                new CombatEvent
                {
                    EventType = CombatEventType.ShootRejected,
                    AttackerId = "player-a",
                    TargetId = "player-b"
                },
                out var playerId);

            Assert.That(routed, Is.True);
            Assert.That(playerId, Is.EqualTo("player-a"));
        }

        [Test]
        public void ClientAuthoritativePlayerState_DamageCombatEvent_ReducesHpAndRecordsCombatResult()
        {
            var owner = new ClientAuthoritativePlayerState();
            owner.TryAccept(new PlayerState { PlayerId = "player-1", Tick = 10, Hp = 100 }, out _);

            var applied = owner.TryApplyCombatEvent(
                new CombatEvent
                {
                    Tick = 11,
                    EventType = CombatEventType.DamageApplied,
                    AttackerId = "player-2",
                    TargetId = "player-1",
                    Damage = 30
                },
                "player-1",
                out var snapshot,
                out var combatSnapshot);

            Assert.That(applied, Is.True);
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.Hp, Is.EqualTo(70));
            Assert.That(owner.Current.Hp, Is.EqualTo(70));
            Assert.That(combatSnapshot.HasLastEvent, Is.True);
            Assert.That(combatSnapshot.LastEventType, Is.EqualTo(CombatEventType.DamageApplied));
            Assert.That(combatSnapshot.LastDamage, Is.EqualTo(30));
            Assert.That(combatSnapshot.IsDead, Is.False);
        }

        [Test]
        public void ClientAuthoritativePlayerState_ShootRejected_LeavesHpUnchangedAndRecordsVisibility()
        {
            var owner = new ClientAuthoritativePlayerState();
            owner.TryAccept(new PlayerState { PlayerId = "player-1", Tick = 10, Hp = 90 }, out _);

            var applied = owner.TryApplyCombatEvent(
                new CombatEvent
                {
                    Tick = 12,
                    EventType = CombatEventType.ShootRejected,
                    AttackerId = "player-1",
                    TargetId = "player-2"
                },
                "player-1",
                out var snapshot,
                out var combatSnapshot);

            Assert.That(applied, Is.True);
            Assert.That(snapshot.Hp, Is.EqualTo(90));
            Assert.That(combatSnapshot.LastEventType, Is.EqualTo(CombatEventType.ShootRejected));
            Assert.That(combatSnapshot.LastDamage, Is.EqualTo(0));
            Assert.That(combatSnapshot.IsDead, Is.False);
        }

        [Test]
        public void ClientAuthoritativePlayerState_DeathCombatEvent_MarksPlayerDeadAndAllowsLaterSnapshotRefresh()
        {
            var owner = new ClientAuthoritativePlayerState();
            owner.TryAccept(new PlayerState { PlayerId = "player-1", Tick = 10, Hp = 20 }, out _);
            owner.TryApplyCombatEvent(
                new CombatEvent
                {
                    Tick = 11,
                    EventType = CombatEventType.Death,
                    AttackerId = "player-2",
                    TargetId = "player-1"
                },
                "player-1",
                out var deathSnapshot,
                out var deathCombatSnapshot);

            var accepted = owner.TryAccept(new PlayerState { PlayerId = "player-1", Tick = 12, Hp = 55 }, out var refreshedSnapshot);

            Assert.That(deathSnapshot.Hp, Is.EqualTo(0));
            Assert.That(deathCombatSnapshot.IsDead, Is.True);
            Assert.That(accepted, Is.True);
            Assert.That(refreshedSnapshot.Hp, Is.EqualTo(55));
            Assert.That(owner.CombatPresentation.IsDead, Is.False);
        }

        [Test]
        public void RemotePlayerSnapshotInterpolator_StaleOrDuplicateSnapshots_AreRejected()
        {
            var interpolator = new RemotePlayerSnapshotInterpolator();
            var firstAccepted = interpolator.TryAddSnapshot(CreateSnapshot(10, new Vector3(1f, 0f, 0f)), 1f);
            var duplicateAccepted = interpolator.TryAddSnapshot(CreateSnapshot(10, new Vector3(2f, 0f, 0f)), 1.1f);
            var staleAccepted = interpolator.TryAddSnapshot(CreateSnapshot(9, new Vector3(3f, 0f, 0f)), 1.2f);

            Assert.That(firstAccepted, Is.True);
            Assert.That(duplicateAccepted, Is.False);
            Assert.That(staleAccepted, Is.False);
            Assert.That(interpolator.BufferedSnapshotCount, Is.EqualTo(1));
            Assert.That(interpolator.LatestBufferedTick, Is.EqualTo(10));
        }

        [Test]
        public void RemotePlayerSnapshotInterpolator_BufferOverflow_TrimsOldestSnapshots()
        {
            var interpolator = new RemotePlayerSnapshotInterpolator(maxBufferedSnapshots: 3);

            interpolator.TryAddSnapshot(CreateSnapshot(1, new Vector3(1f, 0f, 0f)), 0f);
            interpolator.TryAddSnapshot(CreateSnapshot(2, new Vector3(2f, 0f, 0f)), 0.05f);
            interpolator.TryAddSnapshot(CreateSnapshot(3, new Vector3(3f, 0f, 0f)), 0.1f);
            interpolator.TryAddSnapshot(CreateSnapshot(4, new Vector3(4f, 0f, 0f)), 0.15f);

            Assert.That(interpolator.BufferedSnapshotCount, Is.EqualTo(3));
            Assert.That(interpolator.LatestBufferedTick, Is.EqualTo(4));

            var sample = interpolator.Sample(0.3f);

            Assert.That(interpolator.BufferedSnapshotCount, Is.EqualTo(1));
            Assert.That(sample.LatestSnapshot.Tick, Is.EqualTo(4));
            Assert.That(sample.UsedInterpolation, Is.False);
        }

        [Test]
        public void RemotePlayerSnapshotInterpolator_BracketedRenderTime_InterpolatesBetweenSnapshots()
        {
            var interpolator = new RemotePlayerSnapshotInterpolator();
            interpolator.TryAddSnapshot(CreateSnapshot(10, new Vector3(0f, 0f, 0f), 0f), 0f);
            interpolator.TryAddSnapshot(CreateSnapshot(11, new Vector3(10f, 0f, 0f), 90f), 0.05f);

            var sample = interpolator.Sample(0.125f);

            Assert.That(sample.HasValue, Is.True);
            Assert.That(sample.UsedInterpolation, Is.True);
            Assert.That(sample.Position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(sample.Alpha, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(sample.Rotation.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));
            Assert.That(sample.LatestSnapshot.Tick, Is.EqualTo(11));
        }

        [Test]
        public void RemotePlayerSnapshotInterpolator_WithoutUsableBracket_ClampsToLatestSnapshot()
        {
            var interpolator = new RemotePlayerSnapshotInterpolator();
            interpolator.TryAddSnapshot(CreateSnapshot(12, new Vector3(2f, 0f, -1f), 15f), 0.2f);

            var sample = interpolator.Sample(0.35f);

            Assert.That(sample.HasValue, Is.True);
            Assert.That(sample.UsedInterpolation, Is.False);
            Assert.That(sample.Position, Is.EqualTo(new Vector3(2f, 0f, -1f)));
            Assert.That(sample.Rotation.eulerAngles.y, Is.EqualTo(15f).Within(0.01f));
            Assert.That(sample.LatestSnapshot.Tick, Is.EqualTo(12));
        }

        [Test]
        public void ClockSyncState_RejectsOlderSamples()
        {
            var clockSync = new ClockSyncState();

            var acceptedFirst = clockSync.ObserveSample(42);
            var acceptedSecond = clockSync.ObserveSample(41);

            Assert.That(acceptedFirst, Is.True);
            Assert.That(acceptedSecond, Is.False);
            Assert.That(clockSync.CurrentServerTick, Is.EqualTo(42));
        }

        [Test]
        public void SharedNetworkRuntime_AuthoritativeStateUpdatesClockWithoutChangingLifecycle()
        {
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher());

            runtime.StartAsync().GetAwaiter().GetResult();
            runtime.NotifyLoginStarted();
            runtime.NotifyLoginSucceeded();
            runtime.ObserveAuthoritativeState(88);

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.LoggedIn));
            Assert.That(runtime.ClockSync.CurrentServerTick, Is.EqualTo(88));
        }

        [Test]
        public void ServerNetworkHost_RejectsStaleMoveInputPerPeerWithoutCrossPeerInterference()
        {
            var transport = new FakeTransport();
            var host = new ServerNetworkHost(transport);
            var peerA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
            var peerB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5002);
            var handledTicksByPeer = new Dictionary<string, List<long>>();

            host.MessageManager.RegisterHandler(MessageType.MoveInput, (payload, sender) =>
            {
                var key = sender.ToString();
                if (!handledTicksByPeer.TryGetValue(key, out var ticks))
                {
                    ticks = new List<long>();
                    handledTicksByPeer.Add(key, ticks);
                }

                ticks.Add(MoveInput.Parser.ParseFrom(payload).Tick);
            });

            transport.EmitReceive(
                BuildEnvelope(MessageType.MoveInput, new MoveInput { PlayerId = "player-a", Tick = 5, ThrottleInput = 1f }),
                peerA);
            transport.EmitReceive(
                BuildEnvelope(MessageType.MoveInput, new MoveInput { PlayerId = "player-a", Tick = 4, ThrottleInput = -1f }),
                peerA);
            transport.EmitReceive(
                BuildEnvelope(MessageType.MoveInput, new MoveInput { PlayerId = "player-b", Tick = 4, TurnInput = 1f }),
                peerB);

            Assert.That(handledTicksByPeer[peerA.ToString()], Is.EqualTo(new long[] { 5 }));
            Assert.That(handledTicksByPeer[peerB.ToString()], Is.EqualTo(new long[] { 4 }));
        }

        private static byte[] BuildEnvelope(MessageType type, IMessage payload)
        {
            return new Envelope
            {
                Type = (int)type,
                Payload = payload.ToByteString()
            }.ToByteArray();
        }

        private static ClientAuthoritativePlayerStateSnapshot CreateSnapshot(long tick, Vector3 position, float rotation = 0f)
        {
            return new ClientAuthoritativePlayerStateSnapshot(new PlayerState
            {
                PlayerId = "player-1",
                Tick = tick,
                Position = new global::Network.Defines.Vector3 { X = position.x, Y = position.y, Z = position.z },
                Velocity = new global::Network.Defines.Vector3 { X = 0f, Y = 0f, Z = 0f },
                Rotation = rotation,
                Hp = 100
            });
        }

        private sealed class FakeTransport : ITransport
        {
            public event System.Action<byte[], IPEndPoint> OnReceive;

            public System.Threading.Tasks.Task StartAsync()
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public void Stop()
            {
            }

            public void Send(byte[] data)
            {
            }

            public void SendTo(byte[] data, IPEndPoint target)
            {
            }

            public void SendToAll(byte[] data)
            {
            }

            public void EmitReceive(byte[] data, IPEndPoint sender)
            {
                OnReceive?.Invoke(data, sender);
            }
        }
    }
}
