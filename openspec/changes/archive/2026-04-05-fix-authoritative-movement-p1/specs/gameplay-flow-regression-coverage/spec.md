## MODIFIED Requirements

### Requirement: Gameplay-flow regressions include a fake-transport authoritative round trip
The edit-mode regression suite SHALL include at least one deterministic fake-transport test that spans client send behavior, server-authoritative processing, and outgoing authoritative results. That round-trip regression MUST cover `MoveInput -> PlayerState` and `ShootInput -> CombatEvent` within the same MVP gameplay-flow suite, and it MUST assert that authoritative movement stepping follows the configured cadence contract.

#### Scenario: Fake-transport round trip preserves server authority across movement and combat
- **WHEN** an edit-mode regression test drives gameplay input through fake client/server transports and advances the server authority loop
- **THEN** the authoritative server path emits `PlayerState` snapshots in response to movement input using the configured authoritative movement cadence
- **THEN** the authoritative server path emits `CombatEvent` results in response to shooting input
- **THEN** the combined test protects both client single-session input flow and server multi-session authoritative behavior from regression

### Requirement: Gameplay-flow regressions cover controlled-player correction decisions
The edit-mode regression suite SHALL cover the controlled-player reconciliation path after authoritative movement replay, including bounded correction for small cadence-aligned error and hard snap fallback for large divergence.

#### Scenario: Controlled-player reconciliation uses bounded correction for small error
- **WHEN** an edit-mode regression test applies an authoritative local `PlayerState` that leaves only small post-replay divergence
- **THEN** the controlled-player path keeps authoritative ownership of the snapshot
- **THEN** visible correction converges without an immediate hard snap on the acceptance frame

#### Scenario: Controlled-player reconciliation snaps on large divergence
- **WHEN** an edit-mode regression test applies an authoritative local `PlayerState` that leaves divergence beyond the configured snap threshold
- **THEN** the controlled-player path immediately applies the authoritative transform state
- **THEN** later prediction resumes from that authoritative baseline
