## MODIFIED Requirements

### Requirement: Gameplay-flow regressions cover controlled-player correction decisions
The edit-mode regression suite SHALL cover the controlled-player reconciliation path after authoritative movement replay, including bounded correction for small cadence-aligned error, correction replacement under consecutive authoritative snapshots, and hard snap fallback for large or non-convergent divergence.

#### Scenario: Controlled-player reconciliation uses bounded correction for small error
- **WHEN** an edit-mode regression test applies an authoritative local `PlayerState` that leaves only small post-replay divergence
- **THEN** the controlled-player path keeps authoritative ownership of the snapshot
- **THEN** visible correction converges without an immediate hard snap on the acceptance frame

#### Scenario: Controlled-player reconciliation updates active correction on repeated small snapshots
- **WHEN** an edit-mode regression test feeds multiple authoritative local `PlayerState` updates whose residual divergence remains inside bounded-correction limits while a prior correction is still active
- **THEN** the controlled-player path replaces or folds the active correction according to the sync strategy
- **THEN** the test proves the client does not accumulate multiple stale correction tails

#### Scenario: Controlled-player reconciliation snaps on large divergence
- **WHEN** an edit-mode regression test applies an authoritative local `PlayerState` that leaves divergence beyond the configured snap threshold
- **THEN** the controlled-player path immediately applies the authoritative transform state
- **THEN** later prediction resumes from that authoritative baseline

#### Scenario: Controlled-player reconciliation snaps after failed convergence
- **WHEN** an edit-mode regression test feeds consecutive authoritative local `PlayerState` updates that keep bounded correction from converging within the configured budget
- **THEN** the controlled-player path escalates to a hard snap
- **THEN** the active correction state is cleared before later local prediction continues
