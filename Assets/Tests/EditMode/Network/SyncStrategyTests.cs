using System.Collections.Generic;
using System.Net;
using System.Reflection;
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
                new PlayerState { PlayerId = "player-1", Tick = 11, AcknowledgedMoveTick = 11 },
                0f,
                out var replayInputs);

            Assert.That(accepted, Is.True);
            Assert.That(buffer.LastAuthoritativeTick, Is.EqualTo(11));
            Assert.That(buffer.LastAcknowledgedMoveTick, Is.EqualTo(11));
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
            buffer.TryApplyAuthoritativeState(new PlayerState { PlayerId = "player-1", Tick = 10, AcknowledgedMoveTick = 10 }, 0f, out _);

            var accepted = buffer.TryApplyAuthoritativeState(
                new PlayerState { PlayerId = "player-1", Tick = 9, AcknowledgedMoveTick = 9 },
                0f,
                out var replayInputs);

            Assert.That(accepted, Is.False);
            Assert.That(replayInputs, Is.Empty);
            Assert.That(buffer.LastAuthoritativeTick, Is.EqualTo(10));
            Assert.That(buffer.LastAcknowledgedMoveTick, Is.EqualTo(10));
        }

        [Test]
        public void ClientMovementBootstrap_LoginResponse_UsesServerConfirmedMovementParameters()
        {
            var bootstrap = ClientMovementBootstrap.FromLoginResponse(new LoginResponse
            {
                Speed = 12,
                ServerTick = 34
            });

            Assert.That(bootstrap.AuthoritativeMoveSpeed, Is.EqualTo(12));
            Assert.That(bootstrap.ServerTick, Is.EqualTo(34));
        }

        [Test]
        public void ControlledPlayerCorrection_SmallError_UsesBoundedCorrection()
        {
            var result = ControlledPlayerCorrection.Resolve(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0.75f, 0f, 0f),
                Quaternion.Euler(0f, 15f, 0f),
                new ControlledPlayerCorrectionSettings(0.05f, 10f, 180f));

            Assert.That(result.UsedHardSnap, Is.False);
            Assert.That(result.Position, Is.EqualTo(new Vector3(0.5f, 0f, 0f)));
            Assert.That(result.Rotation.eulerAngles.y, Is.EqualTo(9f).Within(0.01f));
        }

        [Test]
        public void ControlledPlayerCorrection_LargeError_UsesHardSnap()
        {
            var targetRotation = Quaternion.Euler(0f, 40f, 0f);
            var result = ControlledPlayerCorrection.Resolve(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(2f, 0f, 0f),
                targetRotation,
                new ControlledPlayerCorrectionSettings(0.05f, 10f, 180f));

            Assert.That(result.UsedHardSnap, Is.True);
            Assert.That(result.Position, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(result.Rotation.eulerAngles.y, Is.EqualTo(targetRotation.eulerAngles.y).Within(0.01f));
            Assert.That(result.NextState.IsActive, Is.False);
        }

        [Test]
        public void ControlledPlayerCorrection_RepeatedSmallCorrections_UpdateActiveCorrectionState()
        {
            var settings = new ControlledPlayerCorrectionSettings(0.05f, 10f, 180f);
            var first = ControlledPlayerCorrection.Resolve(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0.75f, 0f, 0f),
                Quaternion.identity,
                settings);
            var second = ControlledPlayerCorrection.Resolve(
                first.Position,
                first.Rotation,
                new Vector3(1f, 0f, 0f),
                Quaternion.identity,
                settings,
                first.NextState);

            Assert.That(first.UsedHardSnap, Is.False);
            Assert.That(first.NextState.IsActive, Is.True);
            Assert.That(first.NextState.RemainingStepBudget, Is.EqualTo(2));
            Assert.That(second.UsedHardSnap, Is.False);
            Assert.That(second.Position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(second.NextState.IsActive, Is.False);
        }

        [Test]
        public void ControlledPlayerCorrection_RepeatedNonConvergentSmallCorrections_EventuallyUseHardSnap()
        {
            var settings = new ControlledPlayerCorrectionSettings(0.05f, 10f, 180f);
            var first = ControlledPlayerCorrection.Resolve(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0.75f, 0f, 0f),
                Quaternion.identity,
                settings);
            var second = ControlledPlayerCorrection.Resolve(
                first.Position,
                first.Rotation,
                new Vector3(1.25f, 0f, 0f),
                Quaternion.identity,
                settings,
                first.NextState);
            var third = ControlledPlayerCorrection.Resolve(
                second.Position,
                second.Rotation,
                new Vector3(1.75f, 0f, 0f),
                Quaternion.identity,
                settings,
                second.NextState);

            Assert.That(first.UsedHardSnap, Is.False);
            Assert.That(second.UsedHardSnap, Is.False);
            Assert.That(second.NextState.IsActive, Is.True);
            Assert.That(second.NextState.RemainingStepBudget, Is.EqualTo(1));
            Assert.That(third.UsedHardSnap, Is.True);
            Assert.That(third.Position.x, Is.EqualTo(1.75f).Within(0.0001f));
            Assert.That(third.NextState.IsActive, Is.False);
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
                    Hp = 73,
                    AcknowledgedMoveTick = 9
                },
                out var snapshot);

            Assert.That(accepted, Is.True);
            Assert.That(owner.Current, Is.SameAs(snapshot));
            Assert.That(snapshot.PlayerId, Is.EqualTo("player-1"));
            Assert.That(snapshot.Tick, Is.EqualTo(14));
            Assert.That(snapshot.AcknowledgedMoveTick, Is.EqualTo(9));
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
            Assert.That(sample.Rotation.eulerAngles.y, Is.EqualTo(75f).Within(0.01f));
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
        public void ReplayPendingInputs_StepByStepMatchesAccumulated_ForZeroTurnInput()
        {
            // Arrange: set up MovementComponent with initial state.
            var gameObject = new GameObject("replay-test");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                var turnInput = 0f;
                var throttleInput = 1f;
                var stepDuration = 0.05f;
                var totalDuration = stepDuration * 3;  // 0.15s

                // Act — step-by-step path (live prediction shape).
                ApplyTankMovementStepByStep(movement, turnInput, throttleInput, stepDuration, steps: 3);
                var stepByStepPosition = rigidbody.position;
                var stepByStepRotation = rigidbody.rotation;

                // Reset to initial state.
                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                // Act — accumulated replay shape.
                var accumulatedReplayInputs = new List<PredictedMoveStep>
                {
                    new PredictedMoveStep(
                        new MoveInput { PlayerId = "player-1", Tick = 1, TurnInput = turnInput, ThrottleInput = throttleInput },
                        totalDuration)
                };
                InvokeReplayPendingInputs(movement, accumulatedReplayInputs);
                var accumulatedPosition = rigidbody.position;
                var accumulatedRotation = rigidbody.rotation;

                // Assert: for straight movement (turn=0), both paths should be identical.
                Assert.That(Vector3.Distance(accumulatedPosition, stepByStepPosition), Is.LessThan(0.0001f),
                    "Accumulated replay produced a different position than step-by-step for straight movement.");
                Assert.That(Quaternion.Angle(accumulatedRotation, stepByStepRotation), Is.LessThan(0.01f),
                    "Accumulated replay produced a different rotation than step-by-step for straight movement.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ReplayPendingInputs_StepByStepDiffersFromAccumulated_ForNonZeroTurnInput()
        {
            // Arrange: use many small steps and a large turn input to make non-linearity visible.
            var gameObject = new GameObject("replay-test");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                // Use 20 substeps of 0.05s (1 second total) with full turn.
                // This amplifies the non-linearity so the one-shot and step-by-step diverge.
                var turnInput = 1f;
                var throttleInput = 1f;
                var stepDuration = 0.05f;
                var steps = 20;
                var totalDuration = stepDuration * steps;

                // Act — step-by-step (correct approach).
                ApplyTankMovementStepByStep(movement, turnInput, throttleInput, stepDuration, steps);
                var stepByStepPosition = rigidbody.position;

                // Reset.
                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                // Act — ONE big step simulating the old buggy accumulated behavior.
                ApplyTankMovementStepByStep(movement, turnInput, throttleInput, totalDuration, steps: 1);
                var oneShotPosition = rigidbody.position;

                // Assert: for non-zero turn with many steps, the old one-shot and correct step-by-step MUST differ.
                Assert.That(Vector3.Distance(oneShotPosition, stepByStepPosition), Is.GreaterThan(0.001f),
                    "One-shot accumulated and step-by-step produced the same result for turn input — " +
                    "the non-linearity should cause a visible divergence with many steps.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ReplayPendingInputs_NonZeroTurn_MatchesLivePrediction()
        {
            // Arrange: verify that live step-by-step prediction and ReplayPendingInputs
            // produce identical trajectories for non-zero turn input (turn=0.5, throttle=1, 0.10s).
            var gameObject = new GameObject("replay-test");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                var turnInput = 0.5f;
                var throttleInput = 1f;
                var stepDuration = 0.05f;
                var steps = 2;  // 0.10s total

                // Act — live prediction: step-by-step ApplyTankMovement (correct shape).
                ApplyTankMovementStepByStep(movement, turnInput, throttleInput, stepDuration, steps);
                var livePosition = rigidbody.position;
                var liveRotation = rigidbody.rotation;

                // Reset.
                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                // Act — replay path: ReplayPendingInputs with same total duration.
                var replayInputs = new List<PredictedMoveStep>
                {
                    new PredictedMoveStep(
                        new MoveInput { PlayerId = "player-1", Tick = 1, TurnInput = turnInput, ThrottleInput = throttleInput },
                        stepDuration * steps)
                };
                InvokeReplayPendingInputs(movement, replayInputs);
                var replayPosition = rigidbody.position;
                var replayRotation = rigidbody.rotation;

                // Assert: both paths must produce identical trajectories.
                Assert.That(Vector3.Distance(replayPosition, livePosition), Is.LessThan(0.0001f),
                    "Replay produced a different position than live prediction for non-zero turn input.");
                Assert.That(Quaternion.Angle(replayRotation, liveRotation), Is.LessThan(0.01f),
                    "Replay produced a different rotation than live prediction for non-zero turn input.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClientPredictionBuffer_LastAcknowledgedMoveTick_IsExposed()
        {
            // Arrange: buffer with inputs at ticks 10, 11, 12.
            var buffer = new ClientPredictionBuffer();
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 10, ThrottleInput = 1f });
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 11, ThrottleInput = 1f });
            buffer.Record(new MoveInput { PlayerId = "player-1", Tick = 12, ThrottleInput = 1f });

            // Act: apply authoritative state acknowledging tick 11.
            buffer.TryApplyAuthoritativeState(
                new PlayerState { PlayerId = "player-1", Tick = 11, AcknowledgedMoveTick = 11 },
                0f,
                out _);

            // Assert: LastAcknowledgedMoveTick is correctly exposed.
            Assert.That(buffer.LastAcknowledgedMoveTick, Is.EqualTo(11),
                "LastAcknowledgedMoveTick was not correctly set after authoritative state application.");
        }

        [Test]
        public void ControlledPlayerCorrection_CorrectionMagnitude_IsExposed()
        {
            // Arrange: small position and rotation error.
            var currentPos = Vector3.zero;
            var currentRot = Quaternion.identity;
            var targetPos = new Vector3(0.5f, 0f, 0f);
            var targetRot = Quaternion.Euler(0f, 10f, 0f);
            var settings = new ControlledPlayerCorrectionSettings(0.05f, 10f, 180f);

            // Act.
            var result = ControlledPlayerCorrection.Resolve(currentPos, currentRot, targetPos, targetRot, settings);

            // Assert: PositionError and RotationErrorDegrees are exposed and meaningful.
            Assert.That(result.PositionError, Is.GreaterThan(0f),
                "PositionError should be greater than zero for non-zero position divergence.");
            Assert.That(result.RotationErrorDegrees, Is.GreaterThan(0f),
                "RotationErrorDegrees should be greater than zero for non-zero rotation divergence.");
            Assert.That(result.PositionError, Is.EqualTo(Vector3.Distance(currentPos, targetPos)).Within(0.0001f),
                "PositionError should equal the distance between current and target positions.");
            Assert.That(result.RotationErrorDegrees, Is.EqualTo(Quaternion.Angle(currentRot, targetRot)).Within(0.01f),
                "RotationErrorDegrees should equal the angle between current and target rotations.");
        }

        [Test]
        public void MovementComponent_SetServerTick_DoesNotOscillateWithinDeadBand()
        {
            // Arrange: set up MovementComponent and initialize controlled state.
            var gameObject = new GameObject("send-interval-test");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.interpolation = RigidbodyInterpolation.None;
                var movement = gameObject.AddComponent<MovementComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                var sendIntervalField = typeof(MovementComponent)
                    .GetField("_sendInterval", BindingFlags.Instance | BindingFlags.NonPublic);

                // Set an initial interval as baseline.
                sendIntervalField.SetValue(movement, 0.05f);

                // Act/Assert: offsets within [-2, +2] dead-band do not change the interval.
                // Simulate offset hovering around zero.
                for (var i = -2; i <= 2; i++)
                {
                    movement.SetServerTick(i);  // Tick=0, so offset = i - 0 - 0 = i
                    var interval = (float)sendIntervalField.GetValue(movement);
                    Assert.That(interval, Is.EqualTo(0.05f).Within(0.0001f),
                        $"Offset {i} should not trigger send interval correction within dead-band.");
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ReplayPendingInputs_NonMultipleOfCadence_HandlesRemainingDuration()
        {
            // Arrange: simulate 0.12s — not a multiple of 50ms.
            var gameObject = new GameObject("replay-test");
            try
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                var movement = gameObject.AddComponent<MovementComponent>();
                typeof(MovementComponent)
                    .GetField("_rigid", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(movement, rigidbody);
                movement.Init(true, master: null, speed: 10, serverTick: 0);

                ResetMovementState(rigidbody, Vector3.zero, Quaternion.identity);

                var turnInput = 0f;
                var throttleInput = 1f;
                var totalDuration = 0.12f;  // 0.05 + 0.05 + 0.02

                var replayInputs = new List<PredictedMoveStep>
                {
                    new PredictedMoveStep(
                        new MoveInput { PlayerId = "player-1", Tick = 1, TurnInput = turnInput, ThrottleInput = throttleInput },
                        totalDuration)
                };
                InvokeReplayPendingInputs(movement, replayInputs);
                var finalPosition = rigidbody.position;

                // Expected: 0.12s at speed=10 → 1.2 units forward.
                var expectedPosition = new Vector3(0f, 0f, 1.2f);
                Assert.That(finalPosition.z, Is.EqualTo(expectedPosition.z).Within(0.0001f),
                    "Non-multiple of cadence (0.12s) had remaining duration lost or misapplied.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void ApplyTankMovementStepByStep(MovementComponent movement, float turnInput, float throttleInput, float stepDuration, int steps)
        {
            var method = typeof(MovementComponent)
                .GetMethod("ApplyTankMovement", BindingFlags.Instance | BindingFlags.NonPublic);
            for (var i = 0; i < steps; i++)
            {
                method.Invoke(movement, new object[] { turnInput, throttleInput, stepDuration });
            }
        }

        private static void ResetMovementState(Rigidbody rigidbody, Vector3 position, Quaternion rotation)
        {
            rigidbody.position = position;
            rigidbody.rotation = rotation;
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        private static void InvokeReplayPendingInputs(MovementComponent movement, IReadOnlyList<PredictedMoveStep> inputs)
        {
            typeof(MovementComponent)
                .GetMethod("ReplayPendingInputs", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(movement, new object[] { inputs });
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
