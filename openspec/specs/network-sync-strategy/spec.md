# network-sync-strategy Specification

## Purpose
Define how client and server route high-frequency gameplay synchronization traffic, reject stale updates, reconcile authoritative state, and process clock-sync samples independently of session lifecycle.

## Requirements
### Requirement: Hosts assign delivery policies to synchronization message types
The shared networking core SHALL allow hosts to map business message types to delivery policies. `PlayerInput` and `PlayerState` MUST be assignable to a high-frequency sync policy that is independent from the reliable ordered control policy used by login and lifecycle traffic.

#### Scenario: High-frequency sync messages use a dedicated policy
- **WHEN** the client or server sends `PlayerInput` or `PlayerState`
- **THEN** the runtime resolves a high-frequency sync delivery policy for that message type
- **THEN** the message is sent through the sync lane configured for that policy instead of defaulting to reliable ordered delivery

#### Scenario: Control traffic keeps reliable delivery
- **WHEN** the runtime sends login, logout, heartbeat, or other session-management messages
- **THEN** the runtime resolves the reliable ordered control policy
- **THEN** those messages continue to use the reliable transport path

### Requirement: Sequenced sync receivers discard stale gameplay updates
The high-frequency sync strategy SHALL tag gameplay synchronization messages with monotonic sequencing information and MUST discard stale `PlayerInput` or `PlayerState` updates that arrive older than the last accepted update for the same peer or entity stream.

#### Scenario: Older player input is ignored
- **WHEN** the server receives a `PlayerInput` update with a tick or sequence older than the latest accepted input for that player
- **THEN** the server drops that stale input update
- **THEN** the newer accepted input remains authoritative for simulation

#### Scenario: Older player state does not rewind a client
- **WHEN** the client receives a `PlayerState` update with a tick or sequence older than the latest applied authoritative state for that player
- **THEN** the client ignores the stale state update
- **THEN** visible movement continues from the newer authoritative state without rewinding to older data

### Requirement: Authoritative correction prunes acknowledged prediction history
The client sync strategy SHALL reconcile local prediction against authoritative player-state updates by pruning acknowledged inputs at or before the authoritative tick and only reapplying newer pending inputs.

#### Scenario: Reconciliation removes already acknowledged inputs
- **WHEN** the client accepts an authoritative `PlayerState` update for tick `N`
- **THEN** locally buffered predicted inputs with tick less than or equal to `N` are removed from the replay buffer
- **THEN** only inputs newer than `N` remain eligible for re-simulation

### Requirement: Clock synchronization is a separate sync-policy concern
The shared networking core SHALL process server-tick or clock-synchronization samples through a dedicated sync-policy component rather than storing clock-sync ownership inside `SessionManager`.

#### Scenario: Heartbeat response contributes a clock sample without mutating lifecycle
- **WHEN** a heartbeat or gameplay sync message carries a server-tick sample
- **THEN** the runtime forwards that sample to the clock-sync strategy
- **THEN** session lifecycle state remains unchanged except for liveness or RTT bookkeeping

#### Scenario: Hosts can consume smoothed clock data for prediction
- **WHEN** prediction or reconciliation code needs the current server-time estimate
- **THEN** it reads that estimate from the clock-sync strategy or state object
- **THEN** it does not query `SessionManager` for authoritative clock ownership