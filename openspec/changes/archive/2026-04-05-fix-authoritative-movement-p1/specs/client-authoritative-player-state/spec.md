## MODIFIED Requirements

### Requirement: Local player reconciliation applies the full authoritative state by tick
The controlled client SHALL continue reconciling local prediction from authoritative `PlayerState` snapshots while keeping authoritative HP and optional velocity synchronized with the owned player-state snapshot. Reconciliation MUST use the acknowledged movement-input tick defined by the sync strategy, and the visible controlled-player transform MUST apply cadence-aware bounded correction for small divergence while preserving immediate authoritative snap for large divergence.

#### Scenario: Local authoritative state corrects predicted presentation
- **WHEN** the controlled player accepts an authoritative `PlayerState` whose acknowledged movement-input tick is `N`
- **THEN** local reconciliation prunes or replays predicted movement using tick `N` according to the sync strategy
- **THEN** the local player's visible transform converges toward authoritative `position` and `rotation` through cadence-aware correction when the remaining error is small
- **THEN** the local player's authoritative HP on the client matches the accepted `PlayerState`

#### Scenario: Large local divergence bypasses bounded correction
- **WHEN** the controlled player accepts an authoritative `PlayerState` and the remaining transform error exceeds the configured snap threshold
- **THEN** the controlled player's visible transform snaps immediately to authoritative `position` and `rotation`
- **THEN** later local prediction resumes from that authoritative baseline instead of continuing from stale local presentation
