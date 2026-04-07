using System;
using System.Collections.Generic;
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
    public class ServerAuthoritativeMovementTests
    {
        private static readonly IPEndPoint PeerA = new(IPAddress.Loopback, 9101);
        private static readonly IPEndPoint PeerB = new(IPAddress.Loopback, 9102);

        [Test]
        public void UpdateAuthoritativeMovement_UsesConfiguredSimulationCadence_AndExposesItOnRuntime()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 4f,
                    SimulationInterval = TimeSpan.FromMilliseconds(50),
                    BroadcastInterval = TimeSpan.FromMilliseconds(100)
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            runtime.Host.NotifyLoginStarted(PeerA);
            runtime.Host.NotifyLoginSucceeded(PeerA, "player-a");
            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-a",
                Tick = 1,
                TurnInput = 0f,
                ThrottleInput = 1f
            }), PeerA);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(49));

            Assert.That(runtime.AuthoritativeMovementCadence, Is.EqualTo(TimeSpan.FromMilliseconds(50)));
            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerA, out var stateBeforeCadence), Is.True);
            Assert.That(stateBeforeCadence.PositionX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(createdTransports[9000].BroadcastMessages.Count, Is.EqualTo(0));

            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(1));

            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerA, out var stateAfterFirstStep), Is.True);
            Assert.That(stateAfterFirstStep.PositionZ, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(createdTransports[9000].BroadcastMessages.Count, Is.EqualTo(0));

            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerA, out var stateAfterSecondStep), Is.True);
            Assert.That(stateAfterSecondStep.PositionZ, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(createdTransports[9000].BroadcastMessages.Count, Is.EqualTo(1));
        }

        [Test]
        public void UpdateAuthoritativeMovement_AcceptsLatestTickPerPeer_AndKeepsStaleFilteringIndependent()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 4f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50)
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            runtime.Host.NotifyLoginStarted(PeerA);
            runtime.Host.NotifyLoginSucceeded(PeerA, "player-a");
            runtime.Host.NotifyLoginStarted(PeerB);
            runtime.Host.NotifyLoginSucceeded(PeerB, "player-b");

            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-a",
                Tick = 10,
                TurnInput = 0f,
                ThrottleInput = 1f
            }), PeerA);
            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-a",
                Tick = 8,
                TurnInput = 1f,
                ThrottleInput = 0f
            }), PeerA);
            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-b",
                Tick = 3,
                TurnInput = 0f,
                ThrottleInput = -1f
            }), PeerB);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerA, out var stateA), Is.True);
            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerB, out var stateB), Is.True);
            Assert.That(stateA.PlayerId, Is.EqualTo("player-a"));
            Assert.That(stateA.LastAcceptedMoveTick, Is.EqualTo(10));
            Assert.That(stateA.PositionZ, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(stateA.PositionX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(stateB.PlayerId, Is.EqualTo("player-b"));
            Assert.That(stateB.LastAcceptedMoveTick, Is.EqualTo(3));
            Assert.That(stateB.PositionZ, Is.EqualTo(-0.2f).Within(0.0001f));
            Assert.That(stateB.PositionX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(createdTransports[9000].BroadcastMessages.Count, Is.EqualTo(2));
        }

        [Test]
        public void UpdateAuthoritativeMovement_BroadcastsPlayerStateOnSyncLane_AndZeroVectorStopsMovement()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 10f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(100)
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            runtime.Host.NotifyLoginStarted(PeerA);
            runtime.Host.NotifyLoginSucceeded(PeerA, "player-a");

            createdTransports[9001].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-a",
                Tick = 1,
                TurnInput = 0f,
                ThrottleInput = 1f
            }), PeerA);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(90));

            Assert.That(createdTransports[9001].BroadcastMessages.Count, Is.EqualTo(0));

            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(10));

            Assert.That(createdTransports[9000].BroadcastMessages.Count, Is.EqualTo(0));
            Assert.That(createdTransports[9001].BroadcastMessages.Count, Is.EqualTo(1));

            var firstBroadcast = ParsePlayerState(createdTransports[9001].BroadcastMessages[0]);
            Assert.That(firstBroadcast.PlayerId, Is.EqualTo("player-a"));
            Assert.That(firstBroadcast.Tick, Is.EqualTo(1));
            Assert.That(firstBroadcast.AcknowledgedMoveTick, Is.EqualTo(1));
            Assert.That(firstBroadcast.Position.Z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(firstBroadcast.Velocity.Z, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(firstBroadcast.Velocity.X, Is.EqualTo(0f).Within(0.0001f));

            createdTransports[9001].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-a",
                Tick = 2,
                TurnInput = 0f,
                ThrottleInput = 0f
            }), PeerA);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(100));

            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerA, out var state), Is.True);
            Assert.That(state.LastAcceptedMoveTick, Is.EqualTo(2));
            Assert.That(state.VelocityX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.VelocityZ, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(createdTransports[9001].BroadcastMessages.Count, Is.EqualTo(2));

            var secondBroadcast = ParsePlayerState(createdTransports[9001].BroadcastMessages[1]);
            Assert.That(secondBroadcast.Tick, Is.EqualTo(2));
            Assert.That(secondBroadcast.AcknowledgedMoveTick, Is.EqualTo(2));
            Assert.That(secondBroadcast.Position.Z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(secondBroadcast.Velocity.Z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(secondBroadcast.Velocity.X, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void NotifyLoginSucceeded_CreatesIdleAuthoritativeState_AndBroadcastsPlayerStateWithoutMoveInput()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 10f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50),
                    DefaultHp = 100
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.LoginRequest, new LoginRequest
            {
                PlayerId = "player-a",
                Speed = 5
            }), PeerA);

            runtime.Host.NotifyLoginStarted(PeerA);
            runtime.Host.NotifyLoginSucceeded(PeerA, "player-a");
            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            Assert.That(runtime.TryGetAuthoritativeMovementState(PeerA, out var state), Is.True);
            Assert.That(state.PlayerId, Is.EqualTo("player-a"));
            Assert.That(state.LastAcceptedMoveTick, Is.EqualTo(0));
            Assert.That(state.PositionX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.PositionZ, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.Hp, Is.EqualTo(100));
            Assert.That(createdTransports[9001].BroadcastMessages.Count, Is.EqualTo(1));

            var broadcast = ParsePlayerState(createdTransports[9001].BroadcastMessages[0]);
            Assert.That(broadcast.PlayerId, Is.EqualTo("player-a"));
            Assert.That(broadcast.Tick, Is.EqualTo(1));
            Assert.That(broadcast.AcknowledgedMoveTick, Is.EqualTo(0));
            Assert.That(broadcast.Position.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(broadcast.Position.Z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(broadcast.Velocity.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(broadcast.Velocity.Z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(broadcast.Hp, Is.EqualTo(100));
        }

        [Test]
        public void UpdateAuthoritativeMovement_UsesReliableLaneWhenSyncTransportIsUnavailable()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 6f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50)
                }
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            runtime.Host.NotifyLoginStarted(PeerA);
            runtime.Host.NotifyLoginSucceeded(PeerA, "player-a");

            createdTransports[9000].EmitReceive(BuildEnvelope(MessageType.MoveInput, new MoveInput
            {
                PlayerId = "player-a",
                Tick = 5,
                TurnInput = 0f,
                ThrottleInput = -1f
            }), PeerA);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            runtime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            Assert.That(createdTransports[9000].BroadcastMessages.Count, Is.EqualTo(1));

            var broadcast = ParsePlayerState(createdTransports[9000].BroadcastMessages[0]);
            Assert.That(broadcast.Tick, Is.EqualTo(1));
            Assert.That(broadcast.AcknowledgedMoveTick, Is.EqualTo(5));
            Assert.That(broadcast.Position.Z, Is.EqualTo(-0.3f).Within(0.0001f));
            Assert.That(broadcast.Position.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(broadcast.Velocity.Z, Is.EqualTo(-6f).Within(0.0001f));
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
