## ADDED Requirements

### Requirement: Multi-session hosts manage per-peer lifecycle state
The shared networking core SHALL provide a multi-session lifecycle coordinator for hosts that manage multiple concurrent remote peers. The coordinator MUST maintain distinct per-session lifecycle state keyed by remote identity rather than collapsing all peers into one runtime-level state.

#### Scenario: Server tracks two peers independently
- **WHEN** a server host accepts transport activity from two different remote peers
- **THEN** the multi-session coordinator creates or resolves two distinct managed sessions
- **THEN** lifecycle changes for one peer do not overwrite or hide the state of the other peer

### Requirement: Multi-session hosts can observe and evaluate each managed session
The multi-session lifecycle coordinator SHALL expose per-session lookup or enumeration and MUST evaluate timeout, heartbeat, login, and reconnect rules for each managed session independently using the shared session lifecycle vocabulary.

#### Scenario: Timeout affects only one managed session
- **WHEN** one managed session stops receiving liveness updates while another session continues receiving heartbeat or message activity
- **THEN** the timed-out session transitions through timeout or reconnect states according to policy
- **THEN** the active session remains in its current healthy state

#### Scenario: Host can inspect current managed sessions
- **WHEN** server-side code needs to inspect the current connection state of connected peers
- **THEN** it can look up or enumerate managed sessions through the multi-session coordinator
- **THEN** each entry exposes the shared session lifecycle state for that specific peer

### Requirement: Session removal is explicit and does not corrupt remaining peers
The multi-session lifecycle coordinator SHALL support explicit removal or disconnection handling for one managed session without resetting unrelated sessions that remain active.

#### Scenario: Disconnect removes one session only
- **WHEN** one remote peer disconnects or is evicted by the host
- **THEN** the coordinator updates or removes that peer's managed session
- **THEN** other managed sessions remain queryable and keep their own lifecycle state
