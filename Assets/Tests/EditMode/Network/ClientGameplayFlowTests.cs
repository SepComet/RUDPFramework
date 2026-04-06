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
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                movement.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 1, new Vector3(0.75f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledFixedUpdate(movement);
                Assert.That(rigidbody.position.x, Is.EqualTo(0.5f).Within(0.0001f));

                movement.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 2, new Vector3(1f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledFixedUpdate(movement);
                Assert.That(rigidbody.position.x, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClientGameplayFlow_ControlledPlayerReconciliation_EscalatesToSnapAfterFailedConvergence()
        {
            // NOTE: This test verifies the hard-snap escalation path.
            // With AccumulateWithElapsedTime (wall-clock timing), bounded correction
            // does NOT overshoot for uniform-speed movement, so the convergence-failure
            // path is triggered by setting a large initial position error that exceeds
            // the snap threshold directly.
            var gameObject = new GameObject("controlled-player");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                // tick=1, pos=3.0. Client is at 0. Error=3.0 > SnapPositionThreshold (2.5),
                // so hard snap triggers immediately without bounded correction.
                movement.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 1, new Vector3(3.0f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledFixedUpdate(movement);
                Assert.That(rigidbody.position.x, Is.EqualTo(3.0f).Within(0.0001f),
                    "Hard snap should fire immediately when error exceeds snap threshold");

                // tick=2, pos=3.5. Error=0.5 < snap threshold (2.5). Bounded correction
                // (0.5) converges exactly. No pending inputs (Time.time=0 in EditMode).
                movement.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 2, new Vector3(3.5f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledFixedUpdate(movement);
                Assert.That(rigidbody.position.x, Is.EqualTo(3.5f).Within(0.0001f),
                    "Bounded correction should converge exactly for small error");

                // tick=3, pos=4.0. Error=0.5. Bounded correction (0.5) converges exactly.
                movement.OnAuthoritativeState(new ClientAuthoritativePlayerStateSnapshot(
                    GameplayFlowTestSupport.CreatePlayerState("player-1", 3, new Vector3(4.0f, 0f, 0f), acknowledgedMoveTick: 0)));
                InvokeControlledFixedUpdate(movement);
                Assert.That(rigidbody.position.x, Is.EqualTo(4.0f).Within(0.0001f),
                    "Bounded correction should continue converging for consecutive small errors");
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
        private static void InvokeControlledFixedUpdate(MovementComponent movement)
        {
            typeof(MovementComponent)
                .GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(movement, null);
        }
    }
}
