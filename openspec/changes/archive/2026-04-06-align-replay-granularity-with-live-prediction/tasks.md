## 1. Implementation (Already Complete)

The fixed-step replay implementation in `MovementComponent.ReplayPendingInputs()` is already in place using `kServerSimulationStepSeconds` (50ms) as the substep size.

## 2. Regression Tests

> **Note**: Unity EditMode tests require Unity Editor to run.

- [ ] 2.1 Verify `ReplayPendingInputs_StepByStepMatchesAccumulated_ForZeroTurnInput` test passes
- [ ] 2.2 Verify `ReplayPendingInputs_StepByStepDiffersFromAccumulated_ForNonZeroTurnInput` test passes
- [ ] 2.3 Verify `ReplayPendingInputs_NonMultipleOfCadence_HandlesRemainingDuration` test passes

## 3. Diagnostics Capability

- [x] 3.1 Add diagnostics exposure for acknowledged move tick, predicted pose, authoritative pose, and correction magnitude
- [x] 3.2 Expose `LastAcknowledgedMoveTick` from `ClientPredictionBuffer` for diagnostics consumption

## 4. Verification

> **Note**: Unity EditMode tests require Unity Editor. Loopback validation requires PlayMode.

- [ ] 4.1 Run all EditMode tests ensure no regression
- [ ] 4.2 Local loopback validation — controlled-player loopback movement no longer shows repeated small pull-back under steady turn-and-move input
