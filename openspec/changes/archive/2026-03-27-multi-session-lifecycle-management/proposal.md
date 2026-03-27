## Why

The current shared networking foundation exposes a single `SessionManager` lifecycle per runtime, which is sufficient for a client connected to one server but not for a server handling multiple remote peers concurrently. Extending the lifecycle model now prevents the server host from forking transport, login, timeout, and reconnect logic away from the shared network core.

## What Changes

- Introduce shared multi-session lifecycle orchestration for hosts that manage more than one remote peer at a time.
- Preserve the current single-session client flow while adding a server-oriented session collection API keyed by remote identity.
- Define how transport events, login results, heartbeat liveness, timeout detection, and reconnect policy are applied per managed session instead of only per runtime.
- Clarify which responsibilities stay in the shared session orchestration layer versus host-specific admission, cleanup, and gameplay reactions.

## Capabilities

### New Capabilities
- `multi-session-lifecycle`: Shared orchestration and observation of multiple concurrent network sessions, especially for server hosts that manage many remote peers.

### Modified Capabilities
- `network-session-lifecycle`: The shared lifecycle vocabulary and heartbeat/reconnect rules must apply to each managed session, not only to a singleton runtime session.
- `shared-network-foundation`: The shared runtime foundation must support both client-style single-session composition and server-style multi-session composition without introducing a protocol or transport fork.

## Impact

- Affected code: `SharedNetworkRuntime`, `SessionManager`, `ServerNetworkHost`, transport-to-session wiring, and lifecycle-related tests.
- New APIs will likely introduce server-facing session lookup, enumeration, and per-session event observation.
- Client-side runtime composition should remain compatible, but session orchestration responsibilities will be split more explicitly between single-session and multi-session hosts.
