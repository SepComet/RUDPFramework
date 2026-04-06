## Context

`MovementComponent.SetServerTick(long serverTick)` drives input-send cadence by comparing server tick to local client tick. When `_currentTickOffset = serverTick - Tick - _startTickOffset` is negative, it sets `_sendInterval = 0.052f`; when positive, `_sendInterval = 0.048f`. When the offset hovers near zero (e.g., due to minor clock drift or network jitter), the sign flips each call, causing `_sendInterval` to toggle every frame between 0.048 and 0.052. This send-rate oscillation adds jitter to the input cadence.

## Goals / Non-Goals

**Goals:**
- Prevent send interval oscillation when server tick offset is near zero.
- Preserve meaningful clock correction when real drift exists (offset is consistently positive or negative).

**Non-Goals:**
- This is not a full clock synchronization protocol — only a local oscillation guard.
- Does not change the underlying tick offset computation.

## Decisions

### Decision: Dead-band hysteresis for send interval correction

Instead of toggling `_sendInterval` on every sign change of `_currentTickOffset`, apply a dead-band threshold. Only correct the send interval when the absolute offset exceeds a meaningful threshold (e.g., 1-2 ticks = 50-100ms of drift).

**Current code (problematic):**
```csharp
if (_currentTickOffset < 0)
    _sendInterval = 0.052f;
if (_currentTickOffset > 0)
    _sendInterval = 0.048f;
```

**Proposed replacement:**
```csharp
private const float kTickOffsetThreshold = 2; // ticks

if (_currentTickOffset < -kTickOffsetThreshold)
    _sendInterval = 0.052f;
else if (_currentTickOffset > kTickOffsetThreshold)
    _sendInterval = 0.048f;
// else: keep current interval (no correction within dead band)
```

**Alternatives considered:**
1. **Exponential moving average of offset** — smooths jitter but adds complexity and latency to correction.
2. **Remove correction entirely, use fixed 0.05s** — simpler but loses adaptive behavior when real drift exists.

The dead-band approach is the simplest that directly solves oscillation without adding state complexity.

## Risks / Trade-offs

- **Risk**: If `kTickOffsetThreshold` is too large, real drift may not be corrected fast enough.
  - **Mitigation**: Start with a conservative threshold (1-2 ticks). Adjust after measuring.
- **Risk**: The hysteresis introduces a zone where no correction is applied even when offset is slightly non-zero.
  - **Accepted**: This is the intended behavior — minor fluctuations near zero should not disturb steady-rate sending.
