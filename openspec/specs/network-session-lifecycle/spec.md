# network-session-lifecycle Specification

## Purpose
Define the shared session lifecycle model that separates transport connectivity, login state, heartbeat liveness, timeout detection, and reconnect scheduling for client and server hosts.

## Requirements
### Requirement: Session lifecycle distinguishes transport and login state
The shared networking core SHALL expose an explicit session lifecycle model that distinguishes transport connectivity from login/authentication success. Hosts MUST be able to observe at least disconnected, transport-connected, login-pending, logged-in, login-failed, timed-out, and reconnecting lifecycle states for each managed session without inferring them from unrelated message handlers.

#### Scenario: Transport connect does not imply login success
- **WHEN** the transport establishes a usable remote session but no login success message has been accepted yet
- **THEN** the shared lifecycle reports a transport-connected or login-pending state for that managed session
- **THEN** it does not report the session as logged in

#### Scenario: Login success advances lifecycle independently
- **WHEN** the client or server session manager receives a successful login/authentication result for an active transport session
- **THEN** the shared lifecycle transitions that managed session into the logged-in state
- **THEN** hosts can react to that state change without conflating it with transport establishment

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

### Requirement: Timeout and reconnect are session-manager responsibilities
The shared networking core SHALL manage timeout detection, disconnect transitions, and reconnect scheduling through session-manager components rather than implementing those decisions inside business message handlers. Hosts that manage multiple concurrent peers MUST apply these rules independently per managed session rather than collapsing timeout or reconnect state to the entire runtime.

#### Scenario: Timeout produces an observable reconnect transition
- **WHEN** a reconnect-capable host has a session that times out
- **THEN** the session manager emits a timeout-related lifecycle transition for that managed session
- **THEN** it can subsequently move the session into a reconnecting or reconnect-pending state according to configured policy

#### Scenario: Login failure is distinct from transport disconnect
- **WHEN** authentication or login fails while the transport session is still active
- **THEN** the shared lifecycle reports a login-failed state for that managed session
- **THEN** hosts can handle that failure separately from a transport disconnect or heartbeat timeout