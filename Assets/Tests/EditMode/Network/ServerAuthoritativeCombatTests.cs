using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkHost;
using Network.NetworkTransport;
using NUnit.Framework;

namespace Tests.EditMode.Network
{
    public class ServerAuthoritativeCombatTests
    {
        private static readonly IPEndPoint PeerA = new(IPAddress.Loopback, 9201);
        private static readonly IPEndPoint PeerB = new(IPAddress.Loopback, 9202);

        [Test]
        public void DrainPendingMessages_AcceptsAndRejectsShootInputPerPeer_WithoutCrossPeerInterference()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 4f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50),
                    DefaultHp = 100
                },
                AuthoritativeCombat = new ServerAuthoritativeCombatConfiguration
                {
                    DamagePerShot = 30
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            PrimePlayer(runtime, PeerA, "player-a");
            PrimePlayer(runtime, PeerB, "player-b");
            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.ShootInput, new ShootInput
            {
                PlayerId = "player-a",
                Tick = 5,
                DirX = 1f,
                DirY = 0f,
                TargetId = "player-b"
            }), PeerA);
            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.ShootInput, new ShootInput
            {
                PlayerId = "player-b",
                Tick = 2,
                DirX = 0f,
                DirY = 0f,
                TargetId = "player-a"
            }), PeerB);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(runtime.TryGetAuthoritativeCombatState(PeerA, out var combatStateA), Is.True);
            Assert.That(combatStateA.PlayerId, Is.EqualTo("player-a"));
            Assert.That(combatStateA.LastAcceptedShootTick, Is.EqualTo(5));
            Assert.That(combatStateA.LastResolvedCombatTick, Is.EqualTo(5));
            Assert.That(combatStateA.Hp, Is.EqualTo(100));
            Assert.That(combatStateA.IsDead, Is.False);

            Assert.That(runtime.TryGetAuthoritativeCombatState(PeerB, out var combatStateB), Is.True);
            Assert.That(combatStateB.PlayerId, Is.EqualTo("player-b"));
            Assert.That(combatStateB.LastAcceptedShootTick, Is.EqualTo(0));
            Assert.That(combatStateB.LastResolvedCombatTick, Is.EqualTo(5));
            Assert.That(combatStateB.Hp, Is.EqualTo(70));
            Assert.That(combatStateB.IsDead, Is.False);

            var events = createdTransports[9000].BroadcastMessages.Select(ParseCombatEvent).ToArray();
            Assert.That(events.Select(evt => evt.EventType), Is.EqualTo(new[]
            {
                CombatEventType.Hit,
                CombatEventType.DamageApplied,
                CombatEventType.ShootRejected
            }));
            Assert.That(events[1].Damage, Is.EqualTo(30));
            Assert.That(events[2].AttackerId, Is.EqualTo("player-b"));
            Assert.That(events[2].TargetId, Is.EqualTo("player-a"));
        }

        [Test]
        public void DrainPendingMessages_LethalShot_BroadcastsDeath_AndHpCarriesIntoLaterPlayerStateSnapshots()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 5f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50),
                    DefaultHp = 100
                },
                AuthoritativeCombat = new ServerAuthoritativeCombatConfiguration
                {
                    DamagePerShot = 100
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            PrimePlayer(runtime, PeerA, "player-a");
            PrimePlayer(runtime, PeerB, "player-b");
            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.ShootInput, new ShootInput
            {
                PlayerId = "player-a",
                Tick = 10,
                DirX = 0f,
                DirY = 1f,
                TargetId = "player-b"
            }), PeerA);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            var combatEvents = createdTransports[9000].BroadcastMessages.Select(ParseCombatEvent).ToArray();
            Assert.That(combatEvents.Select(evt => evt.EventType), Is.EqualTo(new[]
            {
                CombatEventType.Hit,
                CombatEventType.DamageApplied,
                CombatEventType.Death
            }));
            Assert.That(combatEvents[1].Damage, Is.EqualTo(100));

            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerB, out var targetState), Is.True);
            Assert.That(targetState.Hp, Is.EqualTo(0));
            Assert.That(targetState.IsDead, Is.True);
            Assert.That(targetState.VelocityX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(targetState.VelocityZ, Is.EqualTo(0f).Within(0.0001f));

            var playerStates = createdTransports[9001].BroadcastMessages.Select(ParsePlayerState).OrderBy(state => state.PlayerId).ToArray();
            Assert.That(playerStates.Length, Is.EqualTo(2));
            Assert.That(playerStates[0].PlayerId, Is.EqualTo("player-a"));
            Assert.That(playerStates[0].Hp, Is.EqualTo(100));
            Assert.That(playerStates[1].PlayerId, Is.EqualTo("player-b"));
            Assert.That(playerStates[1].Hp, Is.EqualTo(0));
            Assert.That(playerStates[1].Tick, Is.EqualTo(1));
        }

        [Test]
        public void RemoveSessionAndStop_ClearAuthoritativeCombatStateWithoutResettingOtherPeers()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration(),
                AuthoritativeCombat = new ServerAuthoritativeCombatConfiguration()
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            PrimePlayer(runtime, PeerA, "player-a");
            PrimePlayer(runtime, PeerB, "player-b");
            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.ShootInput, new ShootInput
            {
                PlayerId = "player-a",
                Tick = 3,
                DirX = 1f,
                DirY = 0f,
                TargetId = "player-b"
            }), PeerA);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(runtime.AuthoritativeCombatStates.Count, Is.EqualTo(2));
            Assert.That(runtime.Host.RemoveSession(PeerA), Is.True);
            Assert.That(runtime.TryGetAuthoritativeCombatState(PeerA, out _), Is.False);
            Assert.That(runtime.TryGetAuthoritativeCombatState(PeerB, out var remainingState), Is.True);
            Assert.That(remainingState.PlayerId, Is.EqualTo("player-b"));

            runtime.Stop();

            Assert.That(runtime.AuthoritativeCombatStates.Count, Is.EqualTo(0));
        }

        private static void PrimePlayer(ServerRuntimeHandle runtime, IPEndPoint peer, string playerId)
        {
            runtime.Host.NotifyLoginStarted(peer);
            runtime.Host.NotifyLoginSucceeded(peer, playerId);
        }

        private static FakeTransport CreateTransport(IDictionary<int, FakeTransport> createdTransports, int port)
        {
            var transport = new FakeTransport();
            createdTransports.Add(port, transport);
            return transport;
        }

        private static byte[] BuildEnvelope(MessageType type, IMessage payload)
        {
            return new Envelope
            {
                Type = (int)type,
                Payload = payload.ToByteString()
            }.ToByteArray();
        }

        private static CombatEvent ParseCombatEvent(byte[] envelopeBytes)
        {
            var envelope = Envelope.Parser.ParseFrom(envelopeBytes);
            Assert.That((MessageType)envelope.Type, Is.EqualTo(MessageType.CombatEvent));
            return CombatEvent.Parser.ParseFrom(envelope.Payload);
        }

        private static PlayerState ParsePlayerState(byte[] envelopeBytes)
        {
            var envelope = Envelope.Parser.ParseFrom(envelopeBytes);
            Assert.That((MessageType)envelope.Type, Is.EqualTo(MessageType.PlayerState));
            return PlayerState.Parser.ParseFrom(envelope.Payload);
        }

        private sealed class FakeTransport : ITransport
        {
            public List<byte[]> BroadcastMessages { get; } = new();

            public event Action<byte[], IPEndPoint> OnReceive;

            public Task StartAsync()
            {
                return Task.CompletedTask;
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
                BroadcastMessages.Add(Copy(data));
            }

            public void EmitReceive(byte[] data, IPEndPoint sender)
            {
                OnReceive?.Invoke(Copy(data), sender);
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
    }
}
