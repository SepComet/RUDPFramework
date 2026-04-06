## Context

Step 5 adds regression tests for the client prediction jitter path. Tests are placed in `SyncStrategyTests.cs` alongside existing prediction tests, following the same Arrange-Act-Assert pattern using Unity `GameObject` + `Rigidbody` + `MovementComponent` setup.

## Goals / Non-Goals

**Goals:**
- Test that live prediction and replay produce identical trajectories for non-zero turn input (the `client-prediction-replay` spec requires this).
- Test that `ClientPredictionBuffer` correctly exposes `LastAcknowledgedMoveTick` (the `client-prediction-diagnostics` spec requires this).
- Test that correction magnitude handlers receive valid values from `ControlledPlayerCorrection.Resolve`.

**Non-Goals:**
- No production code changes.
- No new specs — existing specs already define the requirements.

## Tests to Add

### Test 1: Replay trajectory matches live prediction for non-zero turn
```
ReplayPendingInputs_NonZeroTurn_MatchesLivePrediction
```
- Arrange: set up MovementComponent, turn=0.5, throttle=1, total duration=0.10s
- Act: run live step-by-step (ApplyTankMovement × 2 × 0.05s) vs replay (ReplayPendingInputs)
- Assert: positions and headings match within tolerance

### Test 2: ClientPredictionBuffer exposes LastAcknowledgedMoveTick
```
ClientPredictionBuffer_LastAcknowledgedMoveTick_IsExposed
```
- Arrange: buffer with recorded inputs at ticks 10, 11, 12
- Act: apply authoritative state acknowledging tick 11
- Assert: `LastAcknowledgedMoveTick == 11`

### Test 3: Correction magnitude propagates through Reconcile
```
ControlledPlayerCorrection_CorrectionMagnitude_IsComputable
```
- Arrange: predicted pose (0,0,0), authoritative (0.5,0,0), 10° heading diff
- Act: `ControlledPlayerCorrection.Resolve(...)`
- Assert: `result.PositionError > 0`, `result.RotationErrorDegrees > 0`
