## Context

Local loopback testing shows controlled-player jitter. One root cause is `ReplayPendingInputs()` applying each `PredictedMoveStep` as a single accumulated-duration integration, while live prediction uses `FixedUpdate` with fixed substeps. This mismatch in integration shape causes trajectory divergence even for identical input sequences.

Tank movement kinematics: `heading(t+dt) = heading(t) + turnInput * turnSpeed * dt`, `position(t+dt) = position(t) + forward(heading(t+dt)) * throttleSpeed * dt`. Step-by-step and one-shot integration diverge at larger dt values because each step's heading affects the next step's forward direction.

## Goals / Non-Goals

**Goals:**
- `ReplayPendingInputs()` uses fixed-step accumulation matching server authoritative cadence
- Replay produces identical trajectory to live prediction for the same input sequence
- No external API changes, only internal integration method modification
- Add regression test for replay vs live prediction parity
- Add diagnostics for acknowledged move tick, predicted pose, authoritative pose, and correction magnitude

**Non-Goals:**
- Do not modify server 50ms cadence
- Do not fix send-interval oscillation (TODO Step 3)
- Do not modify visual correction logic (TODO Step 4)

## Decisions

### Decision: Use server SimulationInterval (50ms) as replay substep size

**Choice**: Replay in 50ms fixed substeps.

**Rationale**:
- Server integrates at 50ms cadence to produce authoritative state; client replay must match to eliminate偏差
- Client FixedUpdate at 20ms is render/physics step, not server simulation granularity
- Each `PredictedMoveStep.SimulatedDurationSeconds` may be 50ms, 100ms, etc.; stepping at 50ms handles all cases

**Alternatives**:
- 20ms step: matches client FixedUpdate but not server, still causes偏差
- Use `SimulatedDurationSeconds` as single step: current behavior, causes non-linear divergence

### Decision: Substep within ReplayPendingInputs loop without new state

**Implementation**:
```csharp
private void ReplayPendingInputs(IReadOnlyList<PredictedMoveStep> replayInputs)
{
    const float serverStepSeconds = 0.05f;  // 50ms server SimulationInterval
    foreach (var replayInput in replayInputs)
    {
        var remaining = replayInput.SimulatedDurationSeconds;
        while (remaining > 0f)
        {
            var step = Mathf.Min(remaining, serverStepSeconds);
            ApplyTankMovementToPredictedState(
                replayInput.Input.TurnInput,
                replayInput.Input.ThrottleInput,
                step);
            remaining -= step;
        }
    }
}
```

**Rationale**:
- Does not change `PredictedMoveStep` struct interface
- No new temporary state variables needed
- Integration shape identical to live prediction path

## Risks / Trade-offs

- **[Risk]** Floating-point accumulation error could cause loop to run one step too many or too few
  - **Mitigation**: Use `Mathf.Min(remaining, serverStepSeconds)` guard; final step naturally truncates
- **[Risk]** 50ms step adds one extra function call for very short inputs
  - **Acceptable**: Negligible overhead
