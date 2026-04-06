## Context

`MovementComponent.FixedUpdate()` currently calls `AccumulateLatest(Time.fixedDeltaTime)` to track pending input duration. `Time.fixedDeltaTime` is the Unity physics step (typically 20ms), but the server's authoritative movement uses a fixed 50ms cadence (`kServerSimulationStepSeconds`). This mismatch means prediction timing drifts from authoritative timing in reconciliation-sensitive paths.

The `Simulate()` method still uses `Time.fixedDeltaTime` for physics integration — this is intentionally preserved to keep Unity physics working correctly. The change only affects how `SimulatedDurationSeconds` is accumulated for the prediction buffer.

## Goals / Non-Goals

**Goals:**
- `AccumulateLatest()` uses the server's authoritative cadence (50ms) instead of `Time.fixedDeltaTime`
- Forward prediction accumulation timing aligns with authoritative timing
- No external API changes, no breaking changes to physics integration

**Non-Goals:**
- Do not change `Simulate()` physics integration — `Time.fixedDeltaTime` remains for Unity physics
- Do not change the replay substep size (already 50ms from Step 1)
- Do not address send-interval oscillation (TODO Step 3)

## Decisions

### Decision: Accumulate using server cadence, not `Time.fixedDeltaTime`

**Choice**: Change `AccumulateLatest(Time.fixedDeltaTime)` to `AccumulateLatest(kServerSimulationStepSeconds)`.

**Rationale**:
- `SimulatedDurationSeconds` represents server-time accumulated since input was recorded
- Server accumulates by 50ms per step; client should match
- `Time.fixedDeltaTime` is a render/physics loop variable, not a game-time unit
- After Step 1, replay already uses 50ms substeps; accumulation should match

**Alternatives considered**:
- Derive accumulation from real elapsed time: Still uses `Time.fixedDeltaTime` under the hood, same mismatch
- Decouple prediction from FixedUpdate entirely: Significant complexity, overkill for this issue

## Risks / Trade-offs

- **[Risk]** `AccumulateLatest` now accumulates 50ms per FixedUpdate even though real elapsed time is 20ms. The prediction buffer grows 2.5× faster in server-time than real time.
  - **Mitigation**: This is the intended behavior — `SimulatedDurationSeconds` is server-time, not real time. Replay consumes server-time at 50ms per step.
  - **Note**: Physics integration (`Simulate`) still uses `Time.fixedDeltaTime`, so visual movement remains correct. Only the prediction buffer's time accounting changes.

- **[Risk]** If FixedUpdate runs at non-20ms intervals (platform variation, frame drops), the mismatch between accumulated server-time and actual physics time grows.
  - **Mitigation**: The TODO identifies this as inherent to mixing cadences; the fix explicitly drives accumulation from the authoritative cadence rather than real time.
