## Context

The current networking stack already shares transport, message routing, and session lifecycle vocabulary between client and server hosts. However, the shared runtime still owns exactly one `SessionManager`, which matches the client model but does not match a server that needs to track many remote peers concurrently while preserving the same login, timeout, heartbeat, and reconnect semantics.

The main constraint is backward compatibility for the client host. The Unity client should keep its simple single-session composition, while the server host gains an explicit multi-session orchestration layer instead of embedding per-peer lifecycle logic into handlers or transport internals.

## Goals / Non-Goals

**Goals:**
- Preserve `SessionManager` as the per-session finite state machine and lifecycle vocabulary owner.
- Introduce a shared multi-session orchestration layer that can create, look up, evaluate, and remove many session managers keyed by remote identity.
- Keep client composition simple by allowing the existing single-session runtime path to remain valid.
- Make server-facing APIs explicit about per-session lookup, enumeration, lifecycle events, and cleanup triggers.

**Non-Goals:**
- Redesign KCP transport session isolation or introduce a new wire protocol.
- Change the meaning of existing connection states or heartbeat semantics.
- Solve stage 6 QoS or synchronization policy work in the same change.
- Move gameplay admission, authority, or player-object ownership into the shared lifecycle layer.

## Decisions

### Keep `SessionManager` as a per-session state machine
`SessionManager` already models one connection lifecycle correctly. Replacing it with a collection-aware type would force client and server concerns into the same API surface. The change will keep `SessionManager` focused on a single session and add a higher-level multi-session coordinator for hosts that manage many peers.

Alternative considered: make `SessionManager` itself collection-aware.
Rejected because it would either expose server-only concepts to the client or create a type with dual responsibilities that is harder to test and reason about.

### Add a keyed multi-session coordinator above transport callbacks
The new orchestration layer should own a mapping from remote identity to per-session `SessionManager` instances. Transport delivery, login results, inbound activity, heartbeat updates, timeout evaluation, and reconnect bookkeeping should be routed through this keyed coordinator rather than being inferred in message handlers.

Alternative considered: keep session dictionaries inside `ServerNetworkHost` only.
Rejected because it would fork lifecycle orchestration away from the shared core and make server behavior harder to test without the host adapter.

### Preserve separate host adapters for client and server
The Unity client should continue composing a single-session runtime with its main-thread dispatcher. The server host should compose the shared transport and message layer with the new multi-session orchestration path, exposing per-session inspection and events without inheriting Unity-specific assumptions.

Alternative considered: replace `SharedNetworkRuntime` with one universal runtime abstraction.
Rejected because the client and server have materially different composition shapes; forcing one runtime abstraction would hide important ownership boundaries.

## Risks / Trade-offs

- [More lifecycle objects] → Mitigation: keep `SessionManager` unchanged and centralize multi-session behavior in one coordinator with focused tests.
- [Remote identity choice may leak transport details] → Mitigation: define a narrow session-key abstraction or standardize on the existing remote endpoint identity used by transport callbacks.
- [Server cleanup bugs can leave stale sessions behind] → Mitigation: require explicit disconnect/removal scenarios and evaluation tests for session expiry.
- [Client API drift during refactor] → Mitigation: keep the single-session runtime path as a first-class supported composition and verify it with regression tests.

## Migration Plan

1. Introduce the multi-session coordinator and per-session observation API in the shared network layer.
2. Rewire `ServerNetworkHost` to use the coordinator for per-peer lifecycle bookkeeping.
3. Leave the client host on the existing single-session runtime path, adding only compatibility glue if necessary.
4. Add tests that cover client single-session regressions and server multi-session behavior side by side.

## Open Questions

- Whether the server session key should be the transport remote endpoint directly or a narrower shared abstraction.
- Whether reconnect scheduling is meaningful for the server side, or should remain configurable per host/session policy.
- How much session enumeration should be exposed publicly versus kept internal with event-based observation.
