## Context

Steps 1-3 implemented fixes for local controlled-player jitter:
1. Replay uses fixed-step substeps (not one-shot accumulated duration)
2. Forward prediction accumulation uses server cadence (50ms) instead of Time.fixedDeltaTime (20ms)
3. Send interval has hysteresis dead-band so it does not oscillate at near-zero offset

Step 4 is a manual validation step — run the game and observe whether the jitter is resolved.

## Goals / Non-Goals

**Goals:**
- Verify that loopback steady turn-and-move input no longer produces visible jitter after Steps 1-3.
- Use the MainUI diagnostics (校正：pos差=X rot差=Y°) to confirm corrections are consistently small.
- Confirm acknowledged move tick advances steadily without gaps.

**Non-Goals:**
- No code changes in this step.
- Do not tune remote player interpolation.
- Do not add new local smoothing or prediction heuristics.

## Decisions

This step follows an observational approach rather than implementing new code:
1. Run Unity Editor with loopback server + client.
2. Hold steady turn-and-move input for 10+ seconds.
3. Observe MainUI correction text — if pos差 < 0.01 and rot差 < 1° consistently, the fixes are working.
4. If jitter is still visible or corrections are large, document what is observed for Step 5.

## Risks / Trade-offs

- **Risk**: Loopback latency (near-zero) may not reflect real network conditions.
  - **Mitigation**: The jitter addressed was deterministic/timing-related, not latency-related, so loopback is appropriate for validation.
- **Risk**: Manual observation is subjective.
  - **Accepted**: The correction magnitude text provides objective data to complement visual observation.
