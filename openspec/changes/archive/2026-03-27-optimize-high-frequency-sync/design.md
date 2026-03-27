## Context

The networking stack now has a stable shared foundation: `KcpTransport` is the only reliable transport, message dispatch is host-injected, and session lifecycle is modeled explicitly for single-session clients and multi-session servers. However, `MessageManager` still sends every business message through one `ITransport`, `MovementComponent` still predicts and reconciles against authoritative state that arrives on the same reliable ordered lane as login and heartbeat traffic, and `SessionManager` still owns the last server-tick sample that prediction code reads indirectly through heartbeat handling.

Stage 6 needs a cross-cutting design because the current bottleneck is no longer transport correctness, but policy coupling. `PlayerInput` and `PlayerState` are high-frequency streams where newer data is usually more valuable than guaranteed delivery of older data. Keeping them on the same reliable ordered KCP lane as control-plane messages creates head-of-line blocking under packet loss or jitter. At the same time, time synchronization now serves prediction and reconciliation more than lifecycle ownership, so it should stop living inside the heartbeat/session state machine.

## Goals / Non-Goals

**Goals:**
- Introduce a host-agnostic delivery-policy layer that separates reliable control traffic from high-frequency gameplay synchronization traffic.
- Define latest-wins sequencing rules for `PlayerInput` and `PlayerState` so stale updates can be rejected deterministically.
- Extract clock-synchronization state from `SessionManager` into a dedicated sync-policy component that prediction and reconciliation code can consume directly.
- Preserve the existing client single-session composition and server multi-session composition while evolving shared networking behavior.
- Keep the envelope/message-type contract stable across the shared networking stack.

**Non-Goals:**
- Replace `KcpTransport` as the project's reliable control transport.
- Redesign login, logout, authentication, or reconnect semantics introduced in earlier stages.
- Deliver stage 7 metrics/logging work in the same change.
- Rewrite gameplay authority rules or build a full deterministic rollback system beyond the networking-facing prediction buffer changes needed here.

## Decisions

### Introduce delivery-policy routing above transport implementations
The shared runtime will add a policy-selection layer that resolves a delivery profile from `MessageType` before a message is sent or accepted. Reliable control messages continue to use the existing `ITransport` and `MessageManager` path, while high-frequency sync messages use a dedicated sync lane abstraction chosen by the host. This keeps transport choice centralized and prevents gameplay code from hard-coding which transport instance to call.

Alternative considered: add QoS flags or transport parameters to every `SendMessage` call.
Rejected because it spreads policy decisions across handlers and host code, making the routing contract harder to audit and easier to misuse.

### Model `PlayerInput` and `PlayerState` as sequenced latest-wins streams
The new sync strategy will treat `PlayerInput` and `PlayerState` as streams that carry monotonic ordering data, using the existing tick fields and allowing an explicit sequence field if the implementation needs one later. Receivers accept only the newest update for a given player/entity stream and drop older arrivals. This removes the main user-visible problem of reliable ordered delivery for movement: outdated packets blocking fresher state.

Alternative considered: keep both message types on reliable KCP and reduce send frequency.
Rejected because it preserves head-of-line blocking and only hides the symptom by lowering update density.

Alternative considered: send sync traffic unreliably without any ordering metadata.
Rejected because the receiver would have no deterministic way to reject stale state or reconcile prediction buffers safely.

### Extract clock sync into a dedicated strategy component
`SessionManager` should continue owning transport/login/liveness/timeout/reconnect semantics, but it should stop being the long-term owner of server-clock samples. A dedicated clock-sync component can consume server tick samples from heartbeat responses and authoritative gameplay updates, smooth them as needed, and expose the current estimate to prediction/reconciliation code without mutating lifecycle state. This matches the real ownership boundary: clock sync informs simulation alignment, not session health.

Alternative considered: keep `LastServerTick` inside `SessionManager` and let gameplay code keep reading it there.
Rejected because it couples sync tuning to lifecycle policy and makes later sampling changes look like session-state changes.

### Preserve explicit client and server host composition
The Unity client should keep composing a main-thread dispatcher, a single-session lifecycle path, and local prediction code, while the server host keeps explicit multi-session routing. The new sync abstractions should be shared, but host adapters remain responsible for how they drive ticking, buffering, and per-peer identity. This avoids forcing Unity frame-loop concerns or server peer-collection concerns into one universal runtime type.

Alternative considered: hide sync routing inside `KcpTransport` or `SessionManager`.
Rejected because both types already have narrower ownership boundaries, and embedding sync policy there would recreate the coupling earlier stages removed.

## Risks / Trade-offs

- [Two delivery lanes increase routing complexity] -> Mitigation: keep one central message-type-to-policy map and cover it with explicit routing tests.
- [Dropped input packets can momentarily reduce simulation fidelity] -> Mitigation: define latest-wins semantics around ticked input snapshots and allow the sender to keep publishing the newest state at a steady cadence.
- [Prediction corrections can become more visible if clock smoothing is noisy] -> Mitigation: isolate clock-sync state behind a dedicated component with deterministic tests for sample acceptance and smoothing behavior.
- [Client and server integration can drift if abstractions are too host-specific] -> Mitigation: keep the policy contracts in shared networking code and verify client single-session and server multi-session behavior in edit mode tests.

## Migration Plan

1. Introduce shared delivery-policy abstractions and a default policy map while leaving all traffic on the existing reliable path as a safe starting point.
2. Add the sync strategy lane and move `PlayerInput` and `PlayerState` routing onto it, while login/logout/heartbeat and other control traffic remain on KCP.
3. Move server-tick ownership out of `SessionManager` and into a dedicated clock-sync state object consumed by prediction/reconciliation code.
4. Update client reconciliation and server acceptance rules to use stale-drop/latest-wins semantics keyed by authoritative tick or sequence.
5. Add regression tests for routing, stale packet rejection, reconciliation buffer pruning, and clock-sync sampling. If rollback is needed, the policy map can route all message types back to the reliable KCP path without undoing earlier lifecycle work.

## Open Questions

- Should the first implementation of the sync lane use a dedicated `UdpClient`-backed transport, or should it start behind an abstract lane that can be backed by KCP tuning or raw UDP later?
- Do remote-player `PlayerState` updates need an explicit sync sequence separate from simulation tick for interpolation-heavy actors?
- Should the client send only the latest input snapshot each interval, or opportunistically bundle the newest few inputs to soften brief loss bursts without restoring head-of-line blocking?