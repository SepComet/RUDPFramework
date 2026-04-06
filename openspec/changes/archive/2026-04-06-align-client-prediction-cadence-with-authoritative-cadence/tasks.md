## 1. Implementation

- [x] 1.1 Change `AccumulateLatest(Time.fixedDeltaTime)` to `AccumulateLatest(kServerSimulationStepSeconds)` in `MovementComponent.FixedUpdate()`

## 2. Verification

- [x] 2.1 Run all EditMode tests ensure no regression
- [x] 2.2 Local loopback validation — controlled-player loopback movement no longer shows jitter under steady turn-and-move input
