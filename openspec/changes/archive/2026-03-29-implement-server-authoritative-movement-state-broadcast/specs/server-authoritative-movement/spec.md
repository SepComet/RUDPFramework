## ADDED Requirements

### Requirement: Server registers and validates `MoveInput` per peer
The shared server networking path SHALL register `MoveInput` handling through the server host/runtime composition and SHALL validate inbound movement input against the sending peer before mutating authoritative state. Validation MUST reject stale ticks for that peer, malformed numeric values, and payloads that do not map to the sender's managed movement state.

#### Scenario: Accepted `MoveInput` updates the sender's authoritative movement intent
- **WHEN** a managed peer sends a well-formed `MoveInput` with a tick newer than the last accepted movement tick for that peer
- **THEN** the server accepts the input for that peer only
- **THEN** the sender's authoritative movement intent and last accepted movement tick are updated

#### Scenario: Stale `MoveInput` is rejected without affecting other peers
- **WHEN** one managed peer sends a `MoveInput` whose tick is older than the last accepted movement tick for that same peer
- **THEN** the server rejects that input for that peer
- **THEN** authoritative movement state for other managed peers remains unchanged

### Requirement: Server owns authoritative movement resolution
The shared server networking path SHALL own the final movement state for each managed peer, including position, rotation, velocity, and stop state. Zero-vector movement input MUST stop authoritative movement rather than leaving the peer in its previous moving state.

#### Scenario: Non-zero input advances authoritative movement state
- **WHEN** the server processes an accepted non-zero `MoveInput` for a managed peer during an authority update step
- **THEN** the server updates that peer's authoritative position, rotation, and velocity from server-side movement resolution
- **THEN** the resulting state becomes the source of truth for later `PlayerState` broadcast

#### Scenario: Zero-vector input stops authoritative movement
- **WHEN** the server processes an accepted zero-vector `MoveInput` for a managed peer
- **THEN** the peer's authoritative velocity becomes zero
- **THEN** subsequent authoritative state snapshots reflect that stopped state until a newer movement input is accepted

### Requirement: Server broadcasts authoritative `PlayerState` snapshots on the sync cadence
The shared server networking path SHALL emit authoritative `PlayerState` snapshots for managed peers at a fixed cadence using the existing sync-lane message contract. Each snapshot MUST be derived from the server-owned movement state and include the authoritative tick for client reconciliation and interpolation.

#### Scenario: Authority update step emits sync-lane player snapshots
- **WHEN** the server reaches a configured authority broadcast cadence while one or more managed peers have authoritative movement state
- **THEN** it sends `PlayerState` snapshots using the sync-lane delivery policy when a distinct sync transport exists
- **THEN** each snapshot includes the authoritative position, rotation, velocity, and tick from server-owned state

#### Scenario: Reliable transport remains fallback when no sync transport exists
- **WHEN** the server broadcasts authoritative `PlayerState` snapshots without a dedicated sync transport
- **THEN** the shared routing path still emits `PlayerState` through the existing fallback lane behavior
- **THEN** the authoritative snapshot contract remains unchanged
