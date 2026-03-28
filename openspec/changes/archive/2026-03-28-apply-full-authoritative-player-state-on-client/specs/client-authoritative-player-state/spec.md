## ADDED Requirements

### Requirement: Client keeps one owned authoritative player-state snapshot per player
The client SHALL keep one explicit owned authoritative `PlayerState` snapshot for each known player instead of spreading authoritative field ownership across unrelated presentation components. The owned snapshot MUST be the source of truth for authoritative `position`, `rotation`, `hp`, and optional `velocity` on the client.

#### Scenario: Incoming authoritative state replaces the owned snapshot
- **WHEN** the client accepts a newer `PlayerState` for a player
- **THEN** the latest accepted packet becomes that player's owned authoritative snapshot on the client
- **THEN** presentation and diagnostics read authoritative `position`, `rotation`, `hp`, and optional `velocity` from that owned snapshot

### Requirement: Local player reconciliation applies the full authoritative state by tick
The controlled client SHALL continue reconciling local prediction from authoritative `PlayerState.Tick`, and that reconciliation MUST apply the accepted authoritative `position` and `rotation` while keeping authoritative HP and optional velocity synchronized with the owned player-state snapshot.

#### Scenario: Local authoritative state corrects predicted presentation
- **WHEN** the controlled player accepts an authoritative `PlayerState` for tick `N`
- **THEN** local reconciliation prunes or replays predicted movement using tick `N` according to the sync strategy
- **THEN** the local player's visible transform is corrected toward authoritative `position` and `rotation`
- **THEN** the local player's authoritative HP on the client matches the accepted `PlayerState`

### Requirement: Remote players apply authoritative state without inventing gameplay truth
Remote player presentation SHALL consume the latest accepted authoritative player-state snapshot and MUST NOT invent HP or final gameplay state locally. Stale remote `PlayerState` packets that are older than the latest accepted authoritative tick for that player MUST NOT overwrite the owned snapshot.

#### Scenario: Remote authoritative state updates presentation and rejects stale packets
- **WHEN** a remote player receives a newer authoritative `PlayerState`
- **THEN** the client's owned snapshot for that remote player updates to the newer authoritative state
- **THEN** remote presentation uses authoritative `position`, `rotation`, and `hp` from that snapshot
- **THEN** an older later-arriving `PlayerState` for that remote player does not overwrite the newer authoritative snapshot

### Requirement: Authoritative HP and state changes are observable during MVP development
The client SHALL expose authoritative HP or comparable authoritative state information through lightweight UI or diagnostics so developers can observe server-truth changes during MVP playtests.

#### Scenario: Development UI reflects authoritative HP
- **WHEN** the client accepts a `PlayerState` whose authoritative HP differs from the previously accepted snapshot
- **THEN** the relevant UI or diagnostics update to show the new authoritative HP value
- **THEN** the displayed value comes from authoritative `PlayerState` data rather than speculative local gameplay logic
