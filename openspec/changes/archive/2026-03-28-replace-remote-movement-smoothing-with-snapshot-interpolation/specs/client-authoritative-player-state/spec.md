## MODIFIED Requirements

### Requirement: Remote players apply authoritative state without inventing gameplay truth
Remote player presentation SHALL consume the accepted authoritative player-state snapshot owned by the client and MUST NOT invent HP or final gameplay state locally. Remote movement presentation MUST smooth authoritative position and rotation through a small buffered snapshot interpolation path instead of applying only the latest snapshot directly. Stale remote `PlayerState` packets that are older than the latest accepted authoritative tick for that player MUST NOT overwrite the owned snapshot or enter the interpolation buffer.

#### Scenario: Remote authoritative state updates interpolation input and rejects stale packets
- **WHEN** a remote player receives a newer authoritative `PlayerState`
- **THEN** the client's owned snapshot for that remote player updates to the newer authoritative state
- **THEN** remote presentation adds that accepted authoritative sample to the interpolation buffer for position and rotation smoothing
- **THEN** an older later-arriving `PlayerState` for that remote player does not overwrite the newer authoritative snapshot or affect interpolation
