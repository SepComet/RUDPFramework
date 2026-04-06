## Why

Steps 1-3 fixed the core timing issues but jitter persists. Step 5 adds deterministic regression coverage so the remaining jitter path has verifiable, reproducible tests — making future debugging faster and preventing regressions.

## What Changes

- Add a regression test confirming live prediction and replay produce identical trajectories for non-zero turn input (fills gap between existing zero-turn test and the spec requirement).
- Add regression test for `ClientPredictionBuffer` acknowledged-move-tick exposure per `client-prediction-diagnostics` spec.
- Add regression test confirming the MainUI diagnostic handlers receive correct correction magnitude values.
- All new tests are in `SyncStrategyTests.cs` alongside existing prediction tests.

## Capabilities

### New Capabilities
- (none — this is a test coverage step)

### Modified Capabilities
- (none)

## Impact

- `Assets/Tests/EditMode/Network/SyncStrategyTests.cs` — new test methods added
- No production code changes
