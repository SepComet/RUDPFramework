## MODIFIED Requirements

### Requirement: Gameplay-flow regressions include a fake-transport authoritative round trip
The edit-mode regression suite SHALL include at least one deterministic fake-transport test that spans client send behavior, server-authoritative processing, and outgoing authoritative results. That round-trip regression MUST cover `MoveInput -> PlayerState` and `ShootInput -> CombatEvent` within the same MVP gameplay-flow suite. Movement round-trip coverage MUST also prove that authoritative `PlayerState` snapshots preserve a distinct snapshot tick and acknowledged movement-input tick, and that controlled-client prediction bootstraps from server-confirmed movement parameters rather than a divergent local-only value.

#### Scenario: Fake-transport round trip preserves server authority across movement and combat
- **WHEN** an edit-mode regression test drives gameplay input through fake client/server transports and advances the server authority loop
- **THEN** the authoritative server path emits `PlayerState` snapshots in response to movement input
- **THEN** the authoritative server path emits `CombatEvent` results in response to shooting input
- **THEN** the movement assertions prove snapshot ordering and acknowledged-input reconciliation use distinct `PlayerState` fields
- **THEN** the combined test protects both client single-session input flow and server multi-session authoritative behavior from regression
