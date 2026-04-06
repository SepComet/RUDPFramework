## Context

P0 separated authoritative snapshot identity from movement-input acknowledgement, and P1 made server cadence explicit while introducing an initial bounded-correction path for controlled-player reconciliation. That leaves one remaining UX-focused gap: repeated small authoritative corrections can still look noisy because the client only decides "bounded correction or snap" at the moment a snapshot is accepted. The implementation does not yet define how bounded correction persists, gets replaced, or escalates when multiple authoritative snapshots arrive while the local player is still converging.

## Goals / Non-Goals

**Goals:**
- Define a stable controlled-player visual-correction policy that survives across multiple accepted authoritative snapshots.
- Keep authoritative gameplay truth separate from temporary visual smoothing state so local presentation can converge without weakening server authority.
- Specify when a new small correction replaces, merges with, or escalates an existing bounded correction.
- Add regression requirements that prove repeated small corrections converge and large divergence still snaps immediately.

**Non-Goals:**
- Do not change server-authoritative movement cadence, tick semantics, or movement parameter ownership.
- Do not modify remote-player interpolation rules.
- Do not introduce extrapolation, rollback beyond existing local replay, or a second gameplay-truth state on the client.
- Do not turn bounded correction into an unbounded smoothing layer that can hide persistent divergence.

## Decisions

### Decision: Represent local visual convergence as explicit correction state
The controlled-player path will keep authoritative transform truth separate from a short-lived visual correction state that tracks the remaining offset being paid down after reconciliation.

Why:
- Repeated small authoritative updates need continuity; otherwise each accepted snapshot effectively restarts smoothing from scratch.
- An explicit correction state makes the contract testable and prevents presentation code from quietly mixing visual offset with authoritative gameplay truth.

Alternative considered:
- Recompute a one-frame bounded correction on every accepted snapshot without storing correction state. Rejected because it does not define behavior across consecutive snapshots and tends to produce visible jitter under sustained updates.

### Decision: New authoritative snapshots update or replace active correction by policy
When the controlled player already has active bounded correction and another authoritative snapshot arrives, the client will either fold the new residual error into the active correction, replace it with a fresher target, or escalate to hard snap when the combined error breaches the snap threshold.

Why:
- The sample needs deterministic behavior when correction is still in flight and another snapshot arrives.
- Replacement rules are necessary to keep the visual path responsive to newer authoritative truth without accumulating stale offsets forever.

Alternative considered:
- Queue multiple corrections independently. Rejected because it increases latency and can create laggy visual tails after authority has already advanced.

### Decision: Bound correction by convergence budget, not by indefinite smoothing
Bounded correction will have an explicit convergence budget derived from cadence-aware limits so the visual path either settles quickly or escalates to snap when authority keeps diverging.

Why:
- P2 is intended to reduce visible twitching, not to hide desync.
- A bounded budget preserves the principle that authoritative truth must win quickly under sustained mismatch.

Alternative considered:
- Smooth indefinitely with a low-pass presentation filter. Rejected because it can mask real divergence and make the controlled player feel floaty.

## Risks / Trade-offs

- [Explicit correction state increases local presentation complexity] -> Keep the state narrow, owned by the controlled-player path only, and cover it with regression tests.
- [Aggressive replacement rules can reintroduce visible twitching] -> Define deterministic merge/replace thresholds and verify them with multi-snapshot regressions.
- [Overly permissive smoothing can hide divergence too long] -> Keep a hard snap threshold and convergence budget that force recovery to authoritative truth.
- [Unity update timing can still expose frame-rate-specific artifacts] -> Express requirements in terms of convergence behavior and authoritative ownership rather than exact frame counts.

## Migration Plan

1. Extend the controlled-player reconciliation contract to expose explicit visual correction state and replacement rules.
2. Update the local sync strategy requirements so consecutive authoritative snapshots interact predictably with bounded correction.
3. Add regression coverage for repeated small corrections, correction replacement, and snap escalation.
4. Implement and verify the new policy before archiving the change.

## Open Questions

- Whether the correction state should be fully encapsulated inside `MovementComponent` or extracted into a dedicated helper/state holder.
- The exact convergence budget values that feel stable in the sample scene without making the controlled player feel detached from input.
