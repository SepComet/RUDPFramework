using System;
using System.Collections.Generic;
using System.Net;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkHost;
using NUnit.Framework;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Tests.EditMode.Network
{
    public class GameplayFlowRoundTripTests
    {
        private static readonly IPEndPoint ClientPeer = new(IPAddress.Loopback, 9401);
        private static readonly IPEndPoint RemotePeer = new(IPAddress.Loopback, 9402);
        private static readonly IPEndPoint ServerSender = new(IPAddress.Loopback, 9000);

        [Test]
        public void FakeTransportRoundTrip_MoveInputAndShootInput_ProduceAuthoritativePlayerStateAndCombatEvent()
        {
            var clientReliableTransport = new GameplayFlowFakeTransport();
            var clientSyncTransport = new GameplayFlowFakeTransport();
            var clientRuntime = new SharedNetworkRuntime(
                clientReliableTransport,
                new MainThreadNetworkDispatcher(),
                syncTransport: clientSyncTransport);
            var clientHarness = new ClientGameplayTestHarness("player-a");
            clientHarness.Register(clientRuntime.MessageManager);

            var serverTransports = new Dictionary<int, GameplayFlowFakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(serverTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 10f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50),
                    DefaultHp = 100
                },
                AuthoritativeCombat = new ServerAuthoritativeCombatConfiguration
                {
                    DamagePerShot = 30
                }
            };

            clientRuntime.StartAsync().GetAwaiter().GetResult();
            using var serverRuntime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            serverRuntime.Host.NotifyLoginStarted(ClientPeer);
            serverRuntime.Host.NotifyLoginSucceeded(ClientPeer, "player-a");
            serverRuntime.Host.NotifyLoginStarted(RemotePeer);
            serverRuntime.Host.NotifyLoginSucceeded(RemotePeer, "player-b");

            serverTransports[9001].EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.MoveInput,
                    new MoveInput
                    {
                        PlayerId = "player-b",
                        Tick = 1,
                        TurnInput = 0f,
                        ThrottleInput = 0f
                    }),
                RemotePeer);
            serverRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            serverTransports[9001].ClearOutgoing();
            serverTransports[9000].ClearOutgoing();

            clientRuntime.MessageManager.SendMessage(
                new MoveInput
                {
                    PlayerId = "player-a",
                    Tick = 1,
                    TurnInput = 0f,
                    ThrottleInput = 1f
                },
                MessageType.MoveInput);
            ClientGameplayInputFlow.SendShootInput(
                clientRuntime.MessageManager,
                "player-a",
                2,
                Vector3.right,
                "player-b");

            TransferSentMessages(clientSyncTransport, serverTransports[9001], ClientPeer);
            TransferSentMessages(clientReliableTransport, serverTransports[9000], ClientPeer);

            serverRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            serverRuntime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            TransferBroadcastMessages(serverTransports[9000], clientReliableTransport, ServerSender);
            clientRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            TransferBroadcastMessages(serverTransports[9001], clientSyncTransport, ServerSender);
            clientRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(serverRuntime.AuthoritativeMovementCadence, Is.EqualTo(TimeSpan.FromMilliseconds(50)));
            Assert.That(serverRuntime.TryGetAuthoritativeMovementState(ClientPeer, out var localServerState), Is.True);
            Assert.That(localServerState.PlayerId, Is.EqualTo("player-a"));
            Assert.That(localServerState.PositionX, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(serverRuntime.TryGetAuthoritativeCombatState(RemotePeer, out var remoteCombatState), Is.True);
            Assert.That(remoteCombatState.PlayerId, Is.EqualTo("player-b"));
            Assert.That(remoteCombatState.Hp, Is.EqualTo(70));

            Assert.That(clientHarness.TryGetState("player-a", out var localClientState), Is.True);
            Assert.That(localClientState.Tick, Is.EqualTo(1));
            Assert.That(localClientState.AcknowledgedMoveTick, Is.EqualTo(1));
            Assert.That(localClientState.Position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(clientHarness.TryGetState("player-b", out var remoteClientState), Is.True);
            Assert.That(remoteClientState.Hp, Is.EqualTo(70));
            Assert.That(clientHarness.TryGetCombatPresentation("player-b", out var remoteCombatPresentation), Is.True);
            Assert.That(remoteCombatPresentation.LastEventType, Is.EqualTo(CombatEventType.DamageApplied));
            Assert.That(remoteCombatPresentation.LastDamage, Is.EqualTo(30));
            Assert.That(remoteCombatPresentation.IsDead, Is.False);
        }

        [Test]
        public void FakeTransportRoundTrip_IdleLoggedInPlayer_ReceivesPlayerState_AndCanShootWithoutMoveInput()
        {
            var clientReliableTransport = new GameplayFlowFakeTransport();
            var clientSyncTransport = new GameplayFlowFakeTransport();
            var clientRuntime = new SharedNetworkRuntime(
                clientReliableTransport,
                new MainThreadNetworkDispatcher(),
                syncTransport: clientSyncTransport);
            var clientHarness = new ClientGameplayTestHarness("player-a");
            clientHarness.Register(clientRuntime.MessageManager);

            var serverTransports = new Dictionary<int, GameplayFlowFakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(serverTransports, port),
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = 10f,
                    BroadcastInterval = TimeSpan.FromMilliseconds(50),
                    DefaultHp = 100
                },
                AuthoritativeCombat = new ServerAuthoritativeCombatConfiguration
                {
                    DamagePerShot = 30
                }
            };

            clientRuntime.StartAsync().GetAwaiter().GetResult();
            using var serverRuntime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            serverTransports[9000].EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.LoginRequest,
                    new LoginRequest
                    {
                        PlayerId = "player-a",
                        Speed = 5
                    }),
                ClientPeer);
            serverTransports[9000].EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.LoginRequest,
                    new LoginRequest
                    {
                        PlayerId = "player-b",
                        Speed = 5
                    }),
                RemotePeer);

            serverRuntime.Host.NotifyLoginStarted(ClientPeer);
            serverRuntime.Host.NotifyLoginSucceeded(ClientPeer, "player-a");
            serverRuntime.Host.NotifyLoginStarted(RemotePeer);
            serverRuntime.Host.NotifyLoginSucceeded(RemotePeer, "player-b");
            serverRuntime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            TransferBroadcastMessages(serverTransports[9001], clientSyncTransport, ServerSender);
            clientRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(clientHarness.TryGetState("player-a", out var idleLocalState), Is.True);
            Assert.That(idleLocalState.Tick, Is.EqualTo(1));
            Assert.That(idleLocalState.AcknowledgedMoveTick, Is.EqualTo(0));
            Assert.That(idleLocalState.Position.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(idleLocalState.Position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(idleLocalState.Hp, Is.EqualTo(100));

            serverTransports[9001].ClearOutgoing();
            serverTransports[9000].ClearOutgoing();

            serverTransports[9001].EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.MoveInput,
                    new MoveInput
                    {
                        PlayerId = "player-b",
                        Tick = 1,
                        TurnInput = 0f,
                        ThrottleInput = 1f
                    }),
                RemotePeer);
            serverRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            serverRuntime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(100));
            serverTransports[9001].ClearOutgoing();
            serverTransports[9000].ClearOutgoing();

            ClientGameplayInputFlow.SendShootInput(
                clientRuntime.MessageManager,
                "player-a",
                3,
                Vector3.right);

            Assert.That(clientReliableTransport.SentMessages.Count, Is.EqualTo(1));
            var outboundEnvelope = Envelope.Parser.ParseFrom(clientReliableTransport.SentMessages[0]);
            var outboundShootInput = ShootInput.Parser.ParseFrom(outboundEnvelope.Payload);
            Assert.That((MessageType)outboundEnvelope.Type, Is.EqualTo(MessageType.ShootInput));
            Assert.That(outboundShootInput.TargetId, Is.EqualTo(string.Empty));

            TransferSentMessages(clientReliableTransport, serverTransports[9000], ClientPeer);

            serverRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            serverRuntime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            TransferBroadcastMessages(serverTransports[9000], clientReliableTransport, ServerSender);
            clientRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            TransferBroadcastMessages(serverTransports[9001], clientSyncTransport, ServerSender);
            clientRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(serverRuntime.TryGetAuthoritativeMovementState(ClientPeer, out var localMovementState), Is.True);
            Assert.That(localMovementState.PlayerId, Is.EqualTo("player-a"));
            Assert.That(localMovementState.LastAcceptedMoveTick, Is.EqualTo(0));
            Assert.That(localMovementState.PositionX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(localMovementState.PositionZ, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(serverRuntime.TryGetAuthoritativeCombatState(ClientPeer, out var localCombatState), Is.True);
            Assert.That(localCombatState.LastAcceptedShootTick, Is.EqualTo(3));
            Assert.That(serverRuntime.TryGetAuthoritativeCombatState(RemotePeer, out var remoteCombatState), Is.True);
            Assert.That(remoteCombatState.PlayerId, Is.EqualTo("player-b"));
            Assert.That(remoteCombatState.Hp, Is.EqualTo(70));

            Assert.That(clientHarness.TryGetState("player-b", out var remoteClientState), Is.True);
            Assert.That(remoteClientState.Hp, Is.EqualTo(70));
            Assert.That(clientHarness.TryGetCombatPresentation("player-b", out var remoteCombatPresentation), Is.True);
            Assert.That(remoteCombatPresentation.LastEventType, Is.EqualTo(CombatEventType.DamageApplied));
            Assert.That(remoteCombatPresentation.LastDamage, Is.EqualTo(30));
            Assert.That(remoteCombatPresentation.IsDead, Is.False);
        }

        private static GameplayFlowFakeTransport CreateTransport(IDictionary<int, GameplayFlowFakeTransport> serverTransports, int port)
        {
            var transport = new GameplayFlowFakeTransport();
            serverTransports.Add(port, transport);
            return transport;
        }

        private static void TransferSentMessages(GameplayFlowFakeTransport source, GameplayFlowFakeTransport destination, IPEndPoint sender)
        {
            foreach (var payload in source.SentMessages)
            {
                destination.EmitReceive(payload, sender);
            }

            source.ClearOutgoing();
        }

        private static void TransferBroadcastMessages(GameplayFlowFakeTransport source, GameplayFlowFakeTransport destination, IPEndPoint sender)
        {
            foreach (var payload in source.BroadcastMessages)
            {
                destination.EmitReceive(payload, sender);
            }

            source.ClearOutgoing();
        }
    }
}
