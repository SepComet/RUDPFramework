## Why

P0 and P1 fixed the correctness side of controlled-player reconciliation: acknowledged movement tick is explicit, steady-state prediction uses server-confirmed movement parameters, and authoritative movement cadence is no longer accidental. The sample still has one remaining gap: the local controlled player can look busy or twitchy under repeated small corrections because the current bounded-correction path is only a first-pass clamp, not a fully specified visual convergence policy.

## What Changes

- Tighten the controlled-player reconciliation requirements so small authoritative corrections accumulate through an explicit visual-correction state instead of repeatedly restarting ad hoc per accepted snapshot.
- Require the local presentation path to separate authoritative gameplay truth from short-lived visual correction state, preserving hard snap only for material divergence.
- Extend the sync-strategy contract so bounded correction defines convergence behavior across consecutive snapshots instead of only classifying a single snapshot as small or large error.
- Extend regression coverage to prove multi-snapshot convergence, correction replacement rules, and hard-snap fallback still hold.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `client-authoritative-player-state`: Tighten the controlled-player presentation contract so authoritative truth and temporary visual correction state remain distinct during local convergence.
- `network-sync-strategy`: Tighten local reconciliation so bounded correction has explicit replacement, convergence, and snap-escalation rules across consecutive authoritative snapshots.
- `gameplay-flow-regression-coverage`: Require edit-mode regressions that cover multi-snapshot convergence and repeated local correction behavior for the controlled player.

## Impact

Affected areas include controlled-player reconciliation and presentation code in `MovementComponent` plus any helper types that own local correction state, along with edit-mode regression tests for sync strategy and gameplay-flow round trips. No transport, session-lifecycle, or server authoritative movement protocol changes are expected in this phase.
