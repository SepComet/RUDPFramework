# client-authoritative-player-state Specification

## Purpose
Define how the Unity client owns, applies, and exposes authoritative `PlayerState` snapshots for local and remote players.

## Requirements
### Requirement: Client keeps one owned authoritative player-state snapshot per player
The client SHALL keep one explicit owned authoritative `PlayerState` snapshot for each known player instead of spreading authoritative field ownership across unrelated presentation components. The owned snapshot MUST be the source of truth for authoritative `position`, `rotation`, `hp`, and optional `velocity` on the client.

#### Scenario: Incoming authoritative state replaces the owned snapshot
- **WHEN** the client accepts a newer `PlayerState` for a player
- **THEN** the latest accepted packet becomes that player's owned authoritative snapshot on the client
- **THEN** presentation and diagnostics read authoritative `position`, `rotation`, `hp`, and optional `velocity` from that owned snapshot

### Requirement: Local player reconciliation applies the full authoritative state by tick
The controlled client SHALL continue reconciling local prediction from authoritative `PlayerState` snapshots while keeping authoritative HP and optional velocity synchronized with the owned player-state snapshot. Reconciliation MUST use the acknowledged movement-input tick defined by the sync strategy, and the visible controlled-player transform MUST keep authoritative gameplay truth separate from short-lived visual correction state. **Replay of pending inputs during reconciliation MUST use fixed-step substeps matching the server authoritative movement cadence, producing identical trajectory to live prediction for the same input sequence.** Small divergence after replay MUST converge through explicit bounded correction state, while large divergence or failed convergence MUST still snap immediately to authoritative `position` and `rotation`.

#### Scenario: Local authoritative state corrects predicted presentation
- **WHEN** the controlled player accepts an authoritative `PlayerState` whose acknowledged movement-input tick is `N`
- **THEN** local reconciliation prunes or replays predicted movement using tick `N` according to the sync strategy
- **THEN** the replay uses fixed-step substeps matching the server authoritative movement cadence
- **THEN** the controlled player's authoritative gameplay state updates immediately to the accepted `position`, `rotation`, HP, and optional velocity
- **THEN** the local player's visible transform may temporarily differ only through bounded visual correction state that converges back to the authoritative baseline

#### Scenario: Consecutive small corrections replace or fold into active visual correction
- **WHEN** the controlled player accepts a newer authoritative `PlayerState` while a bounded visual correction is still active and the new residual error remains inside the configured bounded-correction limits
- **THEN** the client updates the active visual correction state according to the sync strategy instead of preserving stale correction targets indefinitely
- **THEN** the controlled player's authoritative gameplay state still reflects only the newest accepted `PlayerState`

#### Scenario: Large local divergence bypasses bounded correction
- **WHEN** the controlled player accepts an authoritative `PlayerState` and the remaining transform error exceeds the configured snap threshold or the active bounded correction can no longer converge within its budget
- **THEN** the controlled player's visible transform snaps immediately to authoritative `position` and `rotation`
- **THEN** any temporary visual correction state is cleared before later local prediction resumes from that authoritative baseline

#### Scenario: Replay produces identical trajectory to live prediction
- **WHEN** the controlled player replays pending inputs after accepting authoritative `PlayerState`
- **THEN** the replay applies inputs in fixed-duration substeps equal to the server authoritative movement cadence
- **THEN** the final predicted pose equals what live `FixedUpdate` prediction would produce for the same input sequence
- **THEN** the result is stable across multiple replays of the same input sequence

### Requirement: Remote players apply authoritative state without inventing gameplay truth
Remote player presentation SHALL consume the accepted authoritative player-state snapshot owned by the client and MUST NOT invent HP or final gameplay state locally. Remote movement presentation MUST smooth authoritative position and rotation through a small buffered snapshot interpolation path instead of applying only the latest snapshot directly. Stale remote `PlayerState` packets that are older than the latest accepted authoritative tick for that player MUST NOT overwrite the owned snapshot or enter the interpolation buffer.

#### Scenario: Remote authoritative state updates interpolation input and rejects stale packets
- **WHEN** a remote player receives a newer authoritative `PlayerState`
- **THEN** the client's owned snapshot for that remote player updates to the newer authoritative state
- **THEN** remote presentation adds that accepted authoritative sample to the interpolation buffer for position and rotation smoothing
- **THEN** an older later-arriving `PlayerState` for that remote player does not overwrite the newer authoritative snapshot or affect interpolation

### Requirement: Authoritative HP and state changes are observable during MVP development
The client SHALL expose authoritative HP or comparable authoritative state information through lightweight UI or diagnostics so developers can observe server-truth changes during MVP playtests.

#### Scenario: Development UI reflects authoritative HP
- **WHEN** the client accepts a `PlayerState` whose authoritative HP differs from the previously accepted snapshot
- **THEN** the relevant UI or diagnostics update to show the new authoritative HP value
- **THEN** the displayed value comes from authoritative `PlayerState` data rather than speculative local gameplay logic

### Requirement: Client-owned authoritative player presentation can consume authoritative combat-result deltas
The client-owned authoritative player presentation model SHALL accept authoritative combat-result updates in addition to full `PlayerState` snapshots. Applying an authoritative `CombatEvent` for a player MUST be able to adjust the client-owned HP, death state, or related combat presentation truth for that player until a newer authoritative `PlayerState` snapshot refreshes the full state.

#### Scenario: Authoritative combat event updates owned player presentation state
- **WHEN** the client applies a `CombatEvent` that targets or otherwise affects a known player
- **THEN** that player's owned authoritative presentation state updates to reflect the authoritative combat result
- **THEN** a later accepted `PlayerState` snapshot remains allowed to refresh the full authoritative state for that player
