## 1. Controlled Correction State

- [x] 1.1 Introduce an explicit controlled-player visual correction state that stays separate from authoritative gameplay truth.
- [x] 1.2 Route `MovementComponent` reconciliation so accepted authoritative snapshots update or clear the visual correction state instead of restarting ad hoc per-frame correction.

## 2. Consecutive Snapshot Policy

- [x] 2.1 Implement deterministic rules for folding, replacing, or snapping active bounded correction when newer authoritative snapshots arrive before convergence completes.
- [x] 2.2 Add convergence-budget and snap-escalation handling so repeated non-convergent small corrections cannot accumulate indefinitely.

## 3. Regression Coverage

- [x] 3.1 Add sync-strategy unit tests that cover repeated small corrections updating the active correction state.
- [x] 3.2 Add gameplay-flow regression coverage for multi-snapshot controlled-player convergence and snap escalation after failed convergence.
- [x] 3.3 Run the edit-mode network regression suite, or document the blocking environment issue if the runtime remains unavailable.
