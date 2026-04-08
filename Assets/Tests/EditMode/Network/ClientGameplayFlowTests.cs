using System.Net;
using System.Reflection;
using Network.Defines;
using Network.NetworkApplication;
using NUnit.Framework;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Tests.EditMode.Network
{
    public class ClientGameplayFlowTests
    {
        private static readonly IPEndPoint Sender = new(IPAddress.Loopback, 9300);

        [Test]
        public void ClientGameplayInputFlow_SendShootInput_UsesDedicatedGameplayPathAndReliableLane()
        {
            var reliableTransport = new GameplayFlowFakeTransport();
            var syncTransport = new GameplayFlowFakeTransport();
            var manager = new MessageManager(
                reliableTransport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport);

            ClientGameplayInputFlow.SendShootInput(
                manager,
                "player-1",
                17,
                new Vector3(3f, 0f, 4f),
                "enemy-1");

            Assert.That(reliableTransport.SentMessages.Count, Is.EqualTo(1));
            Assert.That(syncTransport.SentMessages.Count, Is.EqualTo(0));

            var envelope = Envelope.Parser.ParseFrom(reliableTransport.SentMessages[0]);
            var shootInput = ShootInput.Parser.ParseFrom(envelope.Payload);
            Assert.That((MessageType)envelope.Type, Is.EqualTo(MessageType.ShootInput));
            Assert.That(shootInput.PlayerId, Is.EqualTo("player-1"));
            Assert.That(shootInput.Tick, Is.EqualTo(17));
            Assert.That(shootInput.DirX, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(shootInput.DirY, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(shootInput.TargetId, Is.EqualTo("enemy-1"));
        }

        [Test]
        public void SharedNetworkRuntime_CombatEventReceivePath_AppliesAuthoritativeDamageAndDeath()
        {
            var transport = new GameplayFlowFakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new MainThreadNetworkDispatcher());
            var harness = new ClientGameplayTestHarness("player-1");
            harness.Register(runtime.MessageManager);

            transport.EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.PlayerState,
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 10, Vector3.zero, hp: 100)),
                Sender);
            transport.EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.CombatEvent,
                    new CombatEvent
                    {
                        Tick = 11,
                        EventType = CombatEventType.DamageApplied,
                        AttackerId = "enemy-1",
                        TargetId = "player-1",
                        Damage = 35,
                        HitPosition = new global::Network.Defines.Vector3 { X = 2f, Y = 0f, Z = 1f }
                    }),
                Sender);
            transport.EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.CombatEvent,
                    new CombatEvent
                    {
                        Tick = 12,
                        EventType = CombatEventType.Death,
                        AttackerId = "enemy-1",
                        TargetId = "player-1"
                    }),
                Sender);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(harness.TryGetState("player-1", out var snapshot), Is.True);
            Assert.That(snapshot.Hp, Is.EqualTo(0));
            Assert.That(harness.TryGetCombatPresentation("player-1", out var combatPresentation), Is.True);
            Assert.That(combatPresentation.HasLastEvent, Is.True);
            Assert.That(combatPresentation.LastEventType, Is.EqualTo(CombatEventType.Death));
            Assert.That(combatPresentation.IsDead, Is.True);
        }

        [Test]
        public void SharedNetworkRuntime_CombatEventReceivePath_RoutesShootRejectedToAttackerDiagnostics()
        {
            var transport = new GameplayFlowFakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new MainThreadNetworkDispatcher());
            var harness = new ClientGameplayTestHarness("player-1");
            harness.Register(runtime.MessageManager);

            transport.EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.PlayerState,
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 20, Vector3.zero, hp: 90)),
                Sender);
            transport.EmitReceive(
                GameplayFlowTestSupport.BuildEnvelope(
                    MessageType.CombatEvent,
                    new CombatEvent
                    {
                        Tick = 21,
                        EventType = CombatEventType.ShootRejected,
                        AttackerId = "player-1",
                        TargetId = "enemy-2"
                    }),
                Sender);

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(harness.TryGetState("player-1", out var snapshot), Is.True);
            Assert.That(snapshot.Hp, Is.EqualTo(90));
            Assert.That(harness.TryGetCombatPresentation("player-1", out var combatPresentation), Is.True);
            Assert.That(combatPresentation.LastEventType, Is.EqualTo(CombatEventType.ShootRejected));
            Assert.That(combatPresentation.LastDamage, Is.EqualTo(0));
            Assert.That(combatPresentation.IsDead, Is.False);
        }

        [Test]
        public void ClientGameplayFlow_ControlledPlayerReconciliation_ReplacesActiveCorrectionForConsecutiveSmallSnapshots()
        {
            var gameObject = new GameObject("controlled-player");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                var resolver = gameObject.AddComponent<MovementResolverComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                typeof(MovementResolverComponent)
                    .GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(resolver, movement);
                resolver.Init(true, master: null, speed: 10, serverTick: 0);

                resolver.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 1, new Vector3(0.25f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledUpdate(movement);
                Assert.That(rigidbody.position.x, Is.EqualTo(0.0375f).Within(0.0001f));
                Assert.That(GetPrivateVector3(resolver, "_predictedPosition").x, Is.EqualTo(0.25f).Within(0.0001f));

                resolver.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 2, new Vector3(0.5f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledUpdate(movement);
                Assert.That(GetPrivateVector3(resolver, "_predictedPosition").x, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(movement.TargetPosition.x, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(rigidbody.position.x, Is.GreaterThan(0.0375f));
                Assert.That(rigidbody.position.x, Is.LessThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClientGameplayFlow_ControlledPlayerReconciliation_EscalatesToSnapForLargeDivergence()
        {
            var gameObject = new GameObject("controlled-player");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                var resolver = gameObject.AddComponent<MovementResolverComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                typeof(MovementResolverComponent)
                    .GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(resolver, movement);
                resolver.Init(true, master: null, speed: 10, serverTick: 0);

                resolver.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 1, new Vector3(3.0f, 0f, 0f), acknowledgedMoveTick: 0)));
                Assert.That(rigidbody.position.x, Is.EqualTo(3.0f).Within(0.0001f),
                    "Large divergence should snap the visible pose immediately to predicted pose.");
                Assert.That(GetPrivateVector3(resolver, "_predictedPosition").x, Is.EqualTo(3.0f).Within(0.0001f));
                Assert.That(movement.TargetPosition.x, Is.EqualTo(3.0f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClientGameplayFlow_ControlledPlayerReconciliation_RebuildsPredictionImmediatelyAndPreservesUnacknowledgedInputs()
        {
            var gameObject = new GameObject("controlled-player");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                var resolver = gameObject.AddComponent<MovementResolverComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                typeof(MovementResolverComponent)
                    .GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(resolver, movement);
                resolver.Init(true, master: null, speed: 10, serverTick: 0);

                var predictionBuffer = (ClientPredictionBuffer)typeof(MovementResolverComponent)
                    .GetField("_predictionBuffer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(resolver);
                predictionBuffer.Record(new MoveInput
                {
                    PlayerId = "player-1",
                    Tick = 1,
                    TurnInput = 0f,
                    ThrottleInput = 1f
                });
                predictionBuffer.Record(new MoveInput
                {
                    PlayerId = "player-1",
                    Tick = 2,
                    TurnInput = 0f,
                    ThrottleInput = 1f
                });
                predictionBuffer.MarkInputSimulated(1, 0.05f);
                predictionBuffer.MarkInputSimulated(2, 0.05f);

                resolver.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 1, Vector3.zero, acknowledgedMoveTick: 0)));

                Assert.That(GetPrivateVector3(resolver, "_predictedPosition").z, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(predictionBuffer.PendingInputs.Count, Is.EqualTo(2));
                Assert.That(predictionBuffer.PendingInputs[0].Input.Tick, Is.EqualTo(1));
                Assert.That(predictionBuffer.PendingInputs[1].Input.Tick, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClientGameplayHarness_RemotePlayerStateFlow_RejectsStaleSnapshots_AndUsesInterpolationOrLatestClamp()
        {
            var harness = new ClientGameplayTestHarness("local-player");

            var firstAccepted = harness.HandlePlayerState(
                GameplayFlowTestSupport.CreatePlayerState("remote-player", 10, new Vector3(0f, 0f, 0f), rotation: 0f),
                receivedAtSeconds: 0f);
            var secondAccepted = harness.HandlePlayerState(
                GameplayFlowTestSupport.CreatePlayerState("remote-player", 11, new Vector3(10f, 0f, 0f), rotation: 90f),
                receivedAtSeconds: 0.05f);
            var staleAccepted = harness.HandlePlayerState(
                GameplayFlowTestSupport.CreatePlayerState("remote-player", 9, new Vector3(99f, 0f, 0f), rotation: 180f),
                receivedAtSeconds: 0.06f);

            var interpolated = harness.SampleRemote("remote-player", 0.125f);
            var clamped = harness.SampleRemote("remote-player", 0.35f);

            Assert.That(firstAccepted, Is.True);
            Assert.That(secondAccepted, Is.True);
            Assert.That(staleAccepted, Is.False);
            Assert.That(harness.GetBufferedSnapshotCount("remote-player"), Is.EqualTo(1));
            Assert.That(harness.GetLatestBufferedTick("remote-player"), Is.EqualTo(11));
            Assert.That(interpolated.HasValue, Is.True);
            Assert.That(interpolated.UsedInterpolation, Is.True);
            Assert.That(interpolated.Position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(interpolated.Rotation.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));
            Assert.That(clamped.HasValue, Is.True);
            Assert.That(clamped.UsedInterpolation, Is.False);
            Assert.That(clamped.LatestSnapshot.Tick, Is.EqualTo(11));
            Assert.That(clamped.Position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
        }
        private static Vector3 GetPrivateVector3(MovementResolverComponent resolver, string fieldName)
        {
            return (Vector3)typeof(MovementResolverComponent)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(resolver);
        }

        private static void InvokeControlledUpdate(MovementComponent movement)
        {
            typeof(MovementComponent)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(movement, null);
        }
    }
}
