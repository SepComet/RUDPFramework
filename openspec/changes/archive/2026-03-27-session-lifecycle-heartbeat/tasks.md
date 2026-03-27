## 1. Shared Lifecycle Model

- [x] 1.1 Add shared lifecycle types for connection state, session events, and heartbeat/reconnect policy configuration.
- [x] 1.2 Implement a host-agnostic session manager that consumes transport-connected, login-result, heartbeat, timeout, and disconnect inputs.

## 2. Runtime Integration

- [x] 2.1 Extend `SharedNetworkRuntime` to compose the session manager alongside transport and message routing.
- [x] 2.2 Update `NetworkManager` and `ServerNetworkHost` to observe explicit lifecycle state changes instead of inferring session health from ad hoc flags.

## 3. Heartbeat And Login Flow Cleanup

- [x] 3.1 Refactor login and heartbeat handlers so heartbeat only updates liveness, RTT, and time-sync state.
- [x] 3.2 Remove timeout and reconnect decisions from business handlers and route them through session-manager policy APIs.

## 4. Verification And Documentation

- [x] 4.1 Add edit mode tests for transport-connected vs logged-in distinction, login failure, heartbeat timeout, and reconnect scheduling.
- [x] 4.2 Update `CodeX-TODO.md` and related network docs to reflect the new lifecycle layering and stage-five completion criteria.
