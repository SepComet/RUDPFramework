# gameplay-flow-regression-coverage Specification

## Purpose
Define the required edit-mode regression coverage that protects the MVP gameplay flow from client gameplay input through authoritative server outputs and client-side application.

## Requirements
### Requirement: Gameplay-flow regressions cover client gameplay send and receive paths
The edit-mode regression suite SHALL cover the MVP client gameplay flow above the raw transport router, including `ShootInput` send routing and authoritative `CombatEvent` receive/apply behavior. Lane-policy assertions that belong to `MessageManager` MAY remain in `MessageManagerTests`, but gameplay-flow assertions MUST live in tests that exercise the client runtime or player-facing application path.

#### Scenario: Client fire intent regression proves dedicated `ShootInput` routing
- **WHEN** the controlled client gameplay path is exercised in an edit-mode regression test for a fire action
- **THEN** the test observes a `ShootInput` payload sent through the dedicated client shooting path
- **THEN** any lane-policy assertion in that flow remains limited to confirming the MVP reliable-lane contract rather than replacing broader gameplay-flow coverage

#### Scenario: Authoritative combat event regression proves client-side application
- **WHEN** an edit-mode regression test delivers an authoritative `CombatEvent` into the client gameplay receive path
- **THEN** the relevant player-owned authoritative state, presentation model, or diagnostics surface reflects the authoritative hit, damage, death, or rejection result
- **THEN** the test proves the outcome is applied from server truth rather than speculative local combat logic

### Requirement: Gameplay-flow regressions cover remote authoritative snapshot decisions
The edit-mode regression suite SHALL cover the client path that buffers and consumes remote authoritative `PlayerState` snapshots, including stale rejection and interpolation/clamp behavior where practical.

#### Scenario: Remote interpolation regression proves buffering and stale rejection
- **WHEN** an edit-mode regression test feeds ordered and stale remote `PlayerState` snapshots into the client remote-player path
- **THEN** the test observes that newer authoritative snapshots enter the remote buffer while stale snapshots do not replace newer accepted state
- **THEN** the test verifies the resulting interpolation or latest-snapshot clamp decision matches the MVP remote-presentation rules

### Requirement: Gameplay-flow regressions include a fake-transport authoritative round trip
The edit-mode regression suite SHALL include at least one deterministic fake-transport test that spans client send behavior, server-authoritative processing, and outgoing authoritative results. That round-trip regression MUST cover `MoveInput -> PlayerState` and `ShootInput -> CombatEvent` within the same MVP gameplay-flow suite, and it MUST assert that authoritative movement stepping follows the configured cadence contract.

#### Scenario: Fake-transport round trip preserves server authority across movement and combat
- **WHEN** an edit-mode regression test drives gameplay input through fake client/server transports and advances the server authority loop
- **THEN** the authoritative server path emits `PlayerState` snapshots in response to movement input using the configured authoritative movement cadence
- **THEN** the authoritative server path emits `CombatEvent` results in response to shooting input
- **THEN** the combined test protects both client single-session input flow and server multi-session authoritative behavior from regression

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
