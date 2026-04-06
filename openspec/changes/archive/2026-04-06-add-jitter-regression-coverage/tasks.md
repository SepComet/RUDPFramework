## 1. Add regression tests to SyncStrategyTests.cs

- [x] 1.1 Add `ReplayPendingInputs_NonZeroTurn_MatchesLivePrediction` — verifies live prediction and replay produce identical trajectories for turn=0.5, throttle=1, duration=0.10s
- [x] 1.2 Add `ClientPredictionBuffer_LastAcknowledgedMoveTick_IsExposed` — verifies LastAcknowledgedMoveTick is correctly set after authoritative state
- [x] 1.3 Add `ControlledPlayerCorrection_CorrectionMagnitude_IsExposed` — verifies PositionError and RotationErrorDegrees are exposed from ControlledPlayerCorrectionResult

## 2. Verify tests pass

- [x] 2.1 Run Unity Test Runner and confirm all tests pass

## 3. Complete

- [x] 3.1 Mark TODO.md Step 5 as complete
