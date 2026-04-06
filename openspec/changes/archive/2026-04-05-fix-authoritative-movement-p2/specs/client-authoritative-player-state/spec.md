## MODIFIED Requirements

### Requirement: Local player reconciliation applies the full authoritative state by tick
The controlled client SHALL continue reconciling local prediction from authoritative `PlayerState` snapshots while keeping authoritative HP and optional velocity synchronized with the owned player-state snapshot. Reconciliation MUST use the acknowledged movement-input tick defined by the sync strategy, and the visible controlled-player transform MUST keep authoritative gameplay truth separate from short-lived visual correction state. Small divergence after replay MUST converge through explicit bounded correction state, while large divergence or failed convergence MUST still snap immediately to authoritative `position` and `rotation`.

#### Scenario: Local authoritative state corrects predicted presentation
- **WHEN** the controlled player accepts an authoritative `PlayerState` whose acknowledged movement-input tick is `N`
- **THEN** local reconciliation prunes or replays predicted movement using tick `N` according to the sync strategy
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
