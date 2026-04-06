## Why

`MovementComponent.SetServerTick(...)` toggles `_sendInterval` between 0.052f and 0.048f whenever `_currentTickOffset` crosses zero. When the offset hovers near zero due to minor clock drift, this causes frame-to-frame send-cadilla oscillation, which disrupts steady-rate input submission and adds unnecessary jitter to the prediction/reconciliation loop.

## What Changes

- Add hysteresis to the send interval adjustment so it does not flip-flop when `_currentTickOffset` oscillates around zero.
- The correction logic will use a dead-band threshold — only adjust `_sendInterval` when the absolute offset exceeds a meaningful threshold, not on every sign change.
- A small nominal send interval (50ms) remains the baseline; clock correction only applies when drift is substantial.

## Capabilities

### New Capabilities
- `client-send-interval-stabilization`: A contract specifying that the client's send interval does not oscillate due to minor server tick offset fluctuations near zero.

### Modified Capabilities
- `client-prediction-cadence`: Extend to explicitly cover that send interval correction is also bounded by hysteresis and does not toggle at near-zero offset.

## Impact

- `MovementComponent.SetServerTick(...)` — threshold-based hysteresis added to send interval correction logic
- No changes to network message formats, delivery policies, or prediction buffer behavior
