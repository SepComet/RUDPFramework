## 1. Implement hysteresis dead-band in SetServerTick

- [x] 1.1 Add `private const int kTickOffsetThreshold = 2;` to MovementComponent
- [x] 1.2 Replace the dual `if (_currentTickOffset < 0 / > 0)` sign checks with a threshold-based dead-band: only adjust `_sendInterval` when `Mathf.Abs(_currentTickOffset) > kTickOffsetThreshold`

## 2. Add regression test for send interval stability

- [x] 2.1 Add a test in `ServerRuntimeEntryPointTests.cs` or a new test file verifying that `SetServerTick` does not oscillate `_sendInterval` when offset hovers near zero

## 3. Update TODO.md

- [x] 3.1 Mark TODO.md Step 3 as complete
