## MODIFIED Requirements

### Requirement: Server broadcasts authoritative `PlayerState` snapshots on the sync cadence
The shared server networking path SHALL emit authoritative `PlayerState` snapshots for managed peers at a fixed cadence using the existing sync-lane message contract. Each snapshot MUST be derived from the server-owned authoritative player state and include both the authoritative snapshot tick for client stale rejection or interpolation and the last acknowledged `MoveInput.Tick` for client reconciliation. Authoritative HP changes produced by server-side combat resolution MUST be reflected in later snapshots for the affected peer.

#### Scenario: Authority update step emits sync-lane player snapshots
- **WHEN** the server reaches a configured authority broadcast cadence while one or more managed peers have authoritative player state
- **THEN** it sends `PlayerState` snapshots using the sync-lane delivery policy when a distinct sync transport exists
- **THEN** each snapshot includes the authoritative position, rotation, velocity, HP, snapshot tick, and acknowledged movement-input tick from server-owned state

#### Scenario: Combat-driven HP changes appear in later player snapshots
- **WHEN** the server applies authoritative combat damage or death to a managed peer
- **THEN** later `PlayerState` snapshots for that peer carry the updated authoritative HP value
- **THEN** clients do not need to invent or persist a separate HP truth outside authoritative server snapshots

#### Scenario: Reliable transport remains fallback when no sync transport exists
- **WHEN** the server broadcasts authoritative `PlayerState` snapshots without a dedicated sync transport
- **THEN** the shared routing path still emits `PlayerState` through the existing fallback lane behavior
- **THEN** the authoritative snapshot contract remains unchanged
