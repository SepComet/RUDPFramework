## MODIFIED Requirements

### Requirement: Authoritative correction prunes acknowledged prediction history
The client sync strategy SHALL reconcile local prediction against authoritative player-state updates by pruning acknowledged movement inputs at or before the acknowledged movement-input tick carried by the authoritative snapshot and only reapplying newer pending `MoveInput` messages. The snapshot tick used for stale rejection or remote interpolation MUST NOT be reused as the local prediction-acknowledgement boundary.

#### Scenario: Reconciliation removes already acknowledged movement inputs
- **WHEN** the client accepts an authoritative `PlayerState` update whose acknowledged movement-input tick is `N`
- **THEN** locally buffered predicted `MoveInput` messages with tick less than or equal to `N` are removed from the replay buffer
- **THEN** only `MoveInput` messages newer than `N` remain eligible for re-simulation
- **THEN** the client does not infer the acknowledgement boundary solely from the snapshot tick
