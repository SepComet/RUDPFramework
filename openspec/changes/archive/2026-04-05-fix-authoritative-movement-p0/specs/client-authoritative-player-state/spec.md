## MODIFIED Requirements

### Requirement: Local player reconciliation applies the full authoritative state by tick
The controlled client SHALL continue reconciling local prediction from authoritative `PlayerState` updates, but it MUST distinguish between the authoritative snapshot tick and the acknowledged movement-input tick carried by that snapshot. Reconciliation MUST use the acknowledged movement-input tick to prune and replay predicted movement, while continuing to apply the accepted authoritative `position` and `rotation` and keeping authoritative HP and optional velocity synchronized with the owned player-state snapshot.

#### Scenario: Local authoritative state corrects predicted presentation
- **WHEN** the controlled player accepts an authoritative `PlayerState` snapshot with snapshot tick `S` and acknowledged movement-input tick `N`
- **THEN** local reconciliation prunes or replays predicted movement using acknowledged tick `N` according to the sync strategy
- **THEN** stale rejection or snapshot ordering for authoritative state continues to use snapshot tick `S`
- **THEN** the local player's visible transform is corrected toward authoritative `position` and `rotation`
- **THEN** the local player's authoritative HP on the client matches the accepted `PlayerState`
