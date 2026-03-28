# network-sync-strategy Specification

## Purpose
Define how client and server route high-frequency gameplay synchronization traffic, reject stale updates, reconcile authoritative state, and process clock-sync samples independently of session lifecycle.

## Requirements
### Requirement: Hosts assign delivery policies to synchronization message types
The shared networking core SHALL allow hosts to map business message types to delivery policies. The default shared resolver used by `MessageManager` MUST map `MoveInput` and `PlayerState` to `HighFrequencySync`, while `ShootInput`, `CombatEvent`, and control-plane messages MUST resolve to `ReliableOrdered` unless a host intentionally supplies a different resolver.

#### Scenario: Default resolver sends movement and state traffic to the sync lane
- **WHEN** the runtime uses `DefaultMessageDeliveryPolicyResolver` to send `MoveInput` or `PlayerState`
- **THEN** the resolver returns `HighFrequencySync`
- **THEN** `MessageManager` sends that envelope through the sync transport lane when one is configured

#### Scenario: Default resolver keeps shooting and combat events on the reliable lane
- **WHEN** the runtime uses `DefaultMessageDeliveryPolicyResolver` to send `ShootInput` or `CombatEvent`
- **THEN** the resolver returns `ReliableOrdered`
- **THEN** `MessageManager` sends that envelope through the reliable transport lane

#### Scenario: Default resolver preserves reliable control traffic
- **WHEN** the runtime uses `DefaultMessageDeliveryPolicyResolver` to send login, logout, heartbeat, or other session-management messages
- **THEN** the resolver returns `ReliableOrdered`
- **THEN** those messages continue to use the reliable transport path

### Requirement: Sequenced sync receivers discard stale gameplay updates
The high-frequency sync strategy SHALL tag gameplay synchronization messages with monotonic sequencing information and MUST discard stale `MoveInput` or `PlayerState` updates that arrive older than the last accepted update for the same peer or entity stream. `ShootInput` and `CombatEvent` MUST NOT be discarded by the latest-wins stale filter.

#### Scenario: Older movement input is ignored
- **WHEN** the server receives a `MoveInput` update with a tick or sequence older than the latest accepted input for that player
- **THEN** the server drops that stale movement update
- **THEN** the newer accepted movement input remains authoritative for simulation

#### Scenario: Older player state does not rewind a client
- **WHEN** the client receives a `PlayerState` update with a tick or sequence older than the latest applied authoritative state for that player
- **THEN** the client ignores the stale state update
- **THEN** visible movement continues from the newer authoritative state without rewinding to older data

#### Scenario: Reliable gameplay events bypass stale-drop filtering
- **WHEN** the runtime receives a `ShootInput` or `CombatEvent` message
- **THEN** the latest-wins stale filter does not reject that message solely because of sync-sequence rules
- **THEN** reliable ordered handling remains responsible for preserving event delivery semantics

### Requirement: Authoritative correction prunes acknowledged prediction history
The client sync strategy SHALL reconcile local prediction against authoritative player-state updates by pruning acknowledged movement inputs at or before the authoritative tick and only reapplying newer pending `MoveInput` messages.

#### Scenario: Reconciliation removes already acknowledged movement inputs
- **WHEN** the client accepts an authoritative `PlayerState` update for tick `N`
- **THEN** locally buffered predicted `MoveInput` messages with tick less than or equal to `N` are removed from the replay buffer
- **THEN** only `MoveInput` messages newer than `N` remain eligible for re-simulation

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