## Context

The project already shares transport, envelope parsing, and message dispatch between Unity client and non-Unity server hosts. Stage 4 established that transport callbacks do not execute gameplay handlers inline, but session lifecycle behavior is still fragmented: transport establishment, login success, heartbeat timeout, disconnect, and reconnect intent are not modeled as separate states, and heartbeat logic is still entangled with business flow. Stage 5 needs a single host-agnostic lifecycle layer before later QoS and sync work build on unstable assumptions.

## Goals / Non-Goals

**Goals:**
- Introduce an explicit shared connection state model that distinguishes transport connection, authentication/login progress, steady-state session health, timeout, and reconnect intent.
- Centralize heartbeat timing, timeout detection, and reconnect scheduling into a shared session manager instead of distributing them across handlers and host code.
- Keep heartbeat infrastructure-focused: liveness detection, RTT measurement, and time sync only.
- Allow Unity client and non-Unity server hosts to observe the same lifecycle events while keeping host-specific reactions outside the shared core.

**Non-Goals:**
- Reworking message QoS or transport reliability semantics.
- Changing the envelope protocol or replacing KCP.
- Designing gameplay-specific reconnect UX, character resync, or rollback rules.
- Adding final production telemetry; detailed metrics belong to a later stage.

## Decisions

### Use an explicit shared connection state enum and session manager
A dedicated `SessionManager` backed by a `ConnectionState` model keeps lifecycle transitions in one place and makes transport-connected, login-pending, logged-in, timed-out, reconnecting, and login-failed states observable. This is better than inferring state from scattered booleans in handlers because later features such as reconnect backoff and QoS splitting need a stable state machine boundary.

Alternative considered: keep lifecycle flags in `NetworkManager` and server host adapters. Rejected because it would fork client/server behavior again right after the shared-network-foundation refactor.

### Treat heartbeat as infrastructure signals, not business state ownership
Heartbeat messages feed the session manager with liveness timestamps, RTT samples, and time-sync data, but they do not themselves declare login success or trigger reconnect policy. This keeps message handlers narrow and prevents hidden coupling where missing a heartbeat implicitly mutates business session state in unrelated code.

Alternative considered: let heartbeat handlers directly disconnect or reconnect. Rejected because it recreates the current layering problem and makes timeout policy hard to test.

### Make shared runtime own lifecycle orchestration, hosts own reactions
`SharedNetworkRuntime` should compose transport, message routing, and session lifecycle so both client and server use the same lifecycle rules. Unity `NetworkManager` and `ServerNetworkHost` consume lifecycle events and decide what to do next, such as UI updates, reconnect attempts, or server-side cleanup.

Alternative considered: introduce separate client/server session managers. Rejected because stage five explicitly aims to share the network底层 contract and keep host differences at the adapter layer.

### Represent reconnect as a scheduler policy, not an immediate side effect
Reconnect should be modeled as a transition into a reconnect-pending or reconnecting state with policy-owned timing, instead of directly restarting transport from inside a timeout callback. That makes backoff, disable/enable behavior, and tests deterministic.

Alternative considered: reconnect immediately inside timeout detection. Rejected because it couples timer evaluation to transport startup side effects and makes repeated failures difficult to reason about.

## Risks / Trade-offs

- [Risk] Introducing a state machine can expose ambiguities in existing login/heartbeat handlers. -> Mitigation: define explicit transition sources and add tests for login success, login failure, heartbeat timeout, disconnect, and reconnect scheduling.
- [Risk] Shared lifecycle ownership can blur the boundary between transport events and business authentication events. -> Mitigation: keep transport state inputs and login result inputs as separate session-manager APIs.
- [Risk] Reconnect policy may require host-specific decisions later. -> Mitigation: keep policy configuration injectable and host reactions event-driven rather than hardcoding Unity-only behavior.
- [Risk] Existing code may already assume that "connected" means "logged in". -> Mitigation: update host-facing APIs and TODO/documentation so callers consume explicit lifecycle states instead of legacy booleans.

## Migration Plan

1. Add shared lifecycle types (`ConnectionState`, session event model, heartbeat policy/session manager) without removing existing login/heartbeat flows yet.
2. Route transport-connect, login-result, heartbeat-received, and timeout inputs through the new session manager.
3. Update Unity client and server host adapters to observe lifecycle state changes from the shared runtime.
4. Remove or simplify duplicated timeout/reconnect logic from message handlers and host code.
5. Add edit mode tests that lock state transitions before beginning Stage 6 QoS work.

## Open Questions

- Whether the server host needs the exact same reconnect scheduling primitives as the client, or only the same state vocabulary.
- Whether login failure should transition to `Disconnected` immediately after reporting failure, or remain in a stable `LoginFailed` state until the host decides the next action.
- How much time-sync state should live in the session manager versus a separate clock-sync helper once Stage 6 begins.
