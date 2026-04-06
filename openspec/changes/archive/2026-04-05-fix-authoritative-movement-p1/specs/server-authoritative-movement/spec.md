## MODIFIED Requirements

### Requirement: Server owns authoritative movement resolution
The shared server networking path SHALL own the final movement state for each managed peer, including position, rotation, velocity, and stop state. Zero-vector movement input MUST stop authoritative movement rather than leaving the peer in its previous moving state. The authoritative movement integrator MUST advance using the runtime's configured authoritative movement cadence so that movement resolution and later `PlayerState` snapshots are produced from the same server-side stepping contract.

#### Scenario: Non-zero input advances authoritative movement state
- **WHEN** the server processes an accepted non-zero `MoveInput` for a managed peer during an authority update step
- **THEN** the server updates that peer's authoritative position, rotation, and velocity from server-side movement resolution using the configured authoritative movement cadence
- **THEN** the resulting state becomes the source of truth for later `PlayerState` broadcast

#### Scenario: Zero-vector input stops authoritative movement
- **WHEN** the server processes an accepted zero-vector `MoveInput` for a managed peer
- **THEN** the peer's authoritative velocity becomes zero
- **THEN** subsequent authoritative state snapshots reflect that stopped state until a newer movement input is accepted

### Requirement: Server broadcasts authoritative `PlayerState` snapshots on the sync cadence
The shared server networking path SHALL emit authoritative `PlayerState` snapshots for managed peers at a fixed cadence using the existing sync-lane message contract. Each snapshot MUST be derived from the server-owned authoritative player state and include the authoritative tick for client reconciliation and interpolation. Authoritative HP changes produced by server-side combat resolution MUST be reflected in later snapshots for the affected peer.

#### Scenario: Authority update step emits sync-lane player snapshots
- **WHEN** the server reaches the configured authoritative movement cadence while one or more managed peers have authoritative player state
- **THEN** it sends `PlayerState` snapshots using the sync-lane delivery policy when a distinct sync transport exists
- **THEN** each snapshot includes the authoritative position, rotation, velocity, HP, and tick from server-owned state

#### Scenario: Combat-driven HP changes appear in later player snapshots
- **WHEN** the server applies authoritative combat damage or death to a managed peer
- **THEN** later `PlayerState` snapshots for that peer carry the updated authoritative HP value
- **THEN** clients do not need to invent or persist a separate HP truth outside authoritative server snapshots

#### Scenario: Reliable transport remains fallback when no sync transport exists
- **WHEN** the server broadcasts authoritative `PlayerState` snapshots without a dedicated sync transport
- **THEN** the shared routing path still emits `PlayerState` through the existing fallback lane behavior
- **THEN** the authoritative snapshot contract remains unchanged
