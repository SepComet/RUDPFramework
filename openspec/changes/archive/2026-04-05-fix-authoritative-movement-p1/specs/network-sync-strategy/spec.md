## MODIFIED Requirements

### Requirement: Authoritative correction prunes acknowledged prediction history
The client sync strategy SHALL reconcile local prediction against authoritative player-state updates by pruning acknowledged movement inputs at or before the authoritative acknowledged movement tick and only reapplying newer pending `MoveInput` messages. For the controlled player, reconciliation MUST classify authoritative error after replay into a bounded-correction path for small cadence-aligned divergence and an immediate snap path for large divergence.

#### Scenario: Reconciliation removes already acknowledged movement inputs
- **WHEN** the client accepts an authoritative `PlayerState` update that acknowledges movement tick `N`
- **THEN** locally buffered predicted `MoveInput` messages with tick less than or equal to `N` are removed from the replay buffer
- **THEN** only `MoveInput` messages newer than `N` remain eligible for re-simulation

#### Scenario: Small post-replay error uses bounded correction
- **WHEN** the controlled client finishes replay after accepting an authoritative `PlayerState` and the remaining position or rotation error stays within the configured bounded-correction threshold
- **THEN** the client keeps authoritative ownership of the accepted snapshot
- **THEN** local presentation converges through bounded correction instead of an immediate hard snap on that frame

#### Scenario: Large divergence snaps immediately
- **WHEN** the controlled client finishes replay after accepting an authoritative `PlayerState` and the remaining error exceeds the configured snap threshold
- **THEN** the client immediately applies the authoritative transform state
- **THEN** later local prediction continues from that authoritative baseline
