## MODIFIED Requirements

### Requirement: Multi-session hosts can observe and evaluate each managed session
The multi-session lifecycle coordinator SHALL expose per-session lookup or enumeration and MUST evaluate timeout, heartbeat, login, reconnect, and authoritative movement tick rules for each managed session independently using the shared session lifecycle vocabulary. Server-side stale-input acceptance and authoritative movement tracking MUST remain scoped to the peer that produced the traffic.

#### Scenario: Timeout affects only one managed session
- **WHEN** one managed session stops receiving liveness updates while another session continues receiving heartbeat or message activity
- **THEN** the timed-out session transitions through timeout or reconnect states according to policy
- **THEN** the active session remains in its current healthy state

#### Scenario: Host can inspect current managed sessions
- **WHEN** server-side code needs to inspect the current connection state of connected peers
- **THEN** it can look up or enumerate managed sessions through the multi-session coordinator
- **THEN** each entry exposes the shared session lifecycle state for that specific peer

#### Scenario: Movement tick filtering remains peer-scoped
- **WHEN** two managed peers send `MoveInput` traffic with different tick progress or ordering
- **THEN** stale-input acceptance is evaluated independently for each managed peer
- **THEN** one peer's late or advanced movement input does not overwrite or suppress the other's authoritative movement state
