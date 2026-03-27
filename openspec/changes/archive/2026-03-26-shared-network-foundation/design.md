## Context

The project already has a reusable transport contract (`ITransport`), a KCP-based reliable transport (`KcpTransport`), and a message layer (`MessageManager`) that parses envelopes and routes handlers. However, the current runtime shape still hard-codes a Unity-oriented hosting model: `NetworkManager` is the only real host, `MessageManager` defaults to `MainThreadNetworkDispatcher`, and the main-thread pumping behavior is bundled into the same client-facing assembly that owns reusable transport code.

That coupling is now the main blocker to sharing one networking stack across client and server. The transport and protocol code itself is already environment-agnostic, but the hosting and dispatch assumptions are not. If a server is added without first separating those concerns, the codebase will either fork into client/server variants or introduce conditional logic in classes that should remain host-neutral.

## Goals / Non-Goals

**Goals:**
- Define a shared network core that both client and server hosts can use without depending on Unity runtime types.
- Introduce an explicit dispatcher abstraction so message execution policy is supplied by the host instead of being baked into `MessageManager`.
- Preserve KCP transport behavior and the existing envelope/handler programming model while separating reusable core from host-specific orchestration.
- Keep Unity main-thread dispatch as a supported client strategy rather than regressing stage four.

**Non-Goals:**
- Build the full dedicated server application, deployment pipeline, or gameplay authority model.
- Redesign connection/login/heartbeat state machines from stage five.
- Change protocol formats or replace KCP with a different transport.

## Decisions

### 1. Split networking into shared core vs host-specific adapters
The refactor will treat transport/session/message-routing code as a shared foundation and move runtime bootstrapping into host adapters. The shared layer owns `ITransport`, `KcpTransport`, envelope parsing, handler registration, and dispatch abstractions. The client host retains Unity frame-loop integration and gameplay/UI handlers; the future server host will provide its own startup and ticking model.

Alternative considered: keep one assembly and rely on naming conventions only. Rejected because soft boundaries will erode quickly once server-specific code starts landing.

### 2. Replace hard-coded main-thread dispatch with an injected dispatcher contract
`MessageManager` should depend on an interface such as `INetworkMessageDispatcher` that can enqueue and execute handler work according to host policy. The Unity client can implement it with a queued main-thread dispatcher; a single-threaded server can implement it with immediate execution or a dedicated server loop. This keeps message parsing shared while making execution policy explicit.

Alternative considered: let `MessageManager` keep constructing `MainThreadNetworkDispatcher` by default and override only on the server. Rejected because a Unity default still leaks client assumptions into shared code and makes tests less honest.

### 3. Keep Unity main-thread dispatch as a client-host requirement, not a shared-core requirement
Stage four's thread-safety guarantee remains valid, but it belongs to the Unity client host rather than the shared message layer. The shared capability will state that hosts provide a dispatch strategy; the existing Unity dispatch capability will be narrowed to define how the client host pumps a main-thread dispatcher implementation.

Alternative considered: remove the `network-main-thread-dispatch` capability entirely and fold everything into the shared-core spec. Rejected because Unity-specific frame-loop guarantees are still valuable and testable on their own.

## Risks / Trade-offs

- [Boundary churn across files and assemblies] -> Move in small slices and keep tests running after each structural step.
- [Client regressions while introducing host abstraction] -> Preserve current Unity behavior behind a client-specific dispatcher adapter and verify with existing edit mode tests.
- [Server host semantics chosen too early] -> Specify only the abstraction and one minimal non-Unity host path; leave richer server lifecycle work for a later change.
- [Over-generalizing the dispatcher contract] -> Keep the interface minimal: register work, drain or execute work, and expose only what shared message routing actually needs.

## Migration Plan

1. Introduce shared host-dispatch abstractions and move message routing to depend on them.
2. Re-home or reorganize reusable network core code so it no longer depends on Unity host classes.
3. Rebuild the Unity client host on top of the shared core plus a main-thread dispatcher adapter.
4. Add a minimal non-Unity host path or tests that prove the same core can run without Unity-specific pumping.
5. Update docs and TODO status once the shared foundation is in place.

## Open Questions

- Whether the shared core should be split by folder only or by asmdef/project boundary in the first pass.
- Whether the initial server-facing host should use immediate dispatch or a queued single-thread loop for parity with future lifecycle work.
