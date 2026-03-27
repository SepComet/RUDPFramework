## Why

Current networking distinguishes transport delivery from Unity threading, but it still does not distinguish transport connectivity, login success, heartbeat timeout, and reconnect flow as first-class session states. Stage 5 is needed now so client and server can share one coherent lifecycle model before QoS and sync optimization build on top of ambiguous state handling.

## What Changes

- Add a shared session lifecycle module that models disconnected, transport-connected, login-pending, logged-in, timeout, reconnecting, and login-failed states explicitly.
- Add a heartbeat policy that is limited to liveness checks, RTT measurement, and time synchronization, without owning login or reconnect decisions directly.
- Move timeout detection, disconnect transitions, and reconnect scheduling into a shared session manager instead of leaving them in message handlers or host-specific business code.
- Expose lifecycle state changes and session events so Unity client host and non-Unity server host can react without forking the underlying network core.

## Capabilities

### New Capabilities
- `network-session-lifecycle`: Shared connection, login, heartbeat, timeout, and reconnect state management for client and server hosts.

### Modified Capabilities
- `shared-network-foundation`: The shared runtime now includes host-agnostic session lifecycle management in addition to transport and message routing.

## Impact

- Affected code: `SharedNetworkRuntime`, `MessageManager`, `NetworkManager`, `ServerNetworkHost`, login/heartbeat handlers, and new session manager/state types.
- Affected behavior: login success no longer implies transport connect, heartbeat becomes a narrow infrastructure concern, and reconnect logic moves out of ad hoc business paths.
- Affected tests: edit mode tests need coverage for lifecycle transitions, timeout handling, login failure, and reconnect scheduling across shared client/server runtime paths.
