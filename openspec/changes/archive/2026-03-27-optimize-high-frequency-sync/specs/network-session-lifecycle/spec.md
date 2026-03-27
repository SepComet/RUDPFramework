## MODIFIED Requirements

### Requirement: Heartbeat is limited to liveness, RTT, and time sync
The shared session lifecycle SHALL treat heartbeat traffic as infrastructure input for liveness detection and round-trip-time measurement only. Clock-synchronization samples MUST be forwarded to a separate sync-strategy component rather than being owned by `SessionManager`, and heartbeat processing MUST NOT itself own login success, login failure, or reconnect policy decisions.

#### Scenario: Heartbeat updates liveness and RTT while forwarding clock samples
- **WHEN** a heartbeat response is received for an active session
- **THEN** the session manager updates last-seen or timeout bookkeeping and RTT data
- **THEN** any server-tick sample is forwarded to the clock-sync strategy without making heartbeat the owner of login state

#### Scenario: Missing heartbeat triggers timeout state
- **WHEN** the configured heartbeat timeout elapses without a required heartbeat or other liveness signal
- **THEN** the session lifecycle transitions the session into a timed-out state
- **THEN** reconnect handling is delegated to the lifecycle reconnect policy rather than hidden inside the heartbeat handler itself