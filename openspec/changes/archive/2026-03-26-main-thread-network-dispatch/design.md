## Context

`KcpTransport` already keeps socket receive and KCP update work on background tasks, but `MessageManager` subscribes directly to `ITransport.OnReceive` and immediately parses and dispatches handlers on whichever thread raised the callback. In the current project, several registered handlers mutate Unity-facing state through `MasterManager` and UI objects inside `NetworkManager`, so the absence of an explicit main-thread handoff is the main architecture gap left after stages two and three.

The project already has a Unity lifecycle entry point in `Assets/Scripts/NetworkManager.cs`, and `CodeX-TODO.md` explicitly recommends adding `Assets/Scripts/Network/NetworkApplication/MainThreadNetworkDispatcher.cs`. Stage four should therefore formalize a queueing boundary without changing the reliable transport contract or mixing in later connection-state concerns.

## Goals / Non-Goals

**Goals:**
- Ensure transport receive callbacks never execute message handlers inline on background threads.
- Introduce a thread-safe queue between transport receive and business handler execution.
- Make Unity main thread code explicitly responsible for draining queued network messages and invoking handlers.
- Preserve the existing `IMessageHandler` / `MessageManager.RegisterHandler` programming model so stage four remains a structural refactor rather than a gameplay rewrite.

**Non-Goals:**
- Redesign KCP session management, heartbeats, reconnection, or login state handling.
- Introduce QoS splitting for `PlayerInput` / `PlayerState`.
- Replace the current handler registration model with a larger event bus or ECS messaging framework.

## Decisions

### 1. Add a dedicated main-thread dispatcher abstraction in the network application layer
The change will introduce a small dispatcher component, expected at `Assets/Scripts/Network/NetworkApplication/MainThreadNetworkDispatcher.cs`, that owns a thread-safe queue of received transport payloads and exposes a drain method for the Unity main thread. This keeps thread-boundary code out of `KcpTransport` and avoids coupling transport code to Unity APIs.

Alternative considered: enqueue directly inside `NetworkManager` with ad-hoc delegates. Rejected because it would bury the threading contract in one scene component and make edit mode testing harder.

### 2. `MessageManager` becomes a queueing bridge, not the final execution site for transport callbacks
`MessageManager` will still subscribe to `ITransport.OnReceive`, parse envelopes, and resolve registered handlers, but the transport callback path will stop awaiting handlers inline. Instead it will enqueue a dispatch work item that can later be executed on the main thread. This preserves message type routing in one place while moving handler invocation to the correct thread boundary.

Alternative considered: push raw bytes into the dispatcher and parse envelopes later on the main thread. Rejected because malformed payload handling and message-type routing belong with the network message layer, not with the Unity host component.

### 3. `NetworkManager` pumps queued network work during Unity's frame loop
The existing `NetworkManager` MonoBehaviour is the narrowest place to guarantee execution on the Unity main thread. It should own or receive the dispatcher and call its drain method from `Update`, with an optional per-frame drain limit to avoid one spike starving a frame. This keeps stage four focused and avoids introducing a second always-on host object unless later stages need it.

Alternative considered: capture `SynchronizationContext` and post handler work directly. Rejected because a dedicated drain step is easier to test deterministically and makes backpressure visible.

## Risks / Trade-offs

- [Queue growth under burst traffic] -> Add a bounded per-frame drain count and log queue length when it exceeds an expected threshold.
- [One extra frame of dispatch latency] -> Acceptable for stage four because the goal is thread safety; later QoS work can tune batching and frame budget.
- [Partial migration where some code still dispatches inline] -> Cover the new contract with tests that assert handlers are not run during the transport callback itself and only run after an explicit drain.
- [Unity lifecycle coupling] -> Keep the dispatcher itself Unity-agnostic so only `NetworkManager` depends on `Update`.

## Migration Plan

1. Introduce the dispatcher abstraction and message work-item representation.
2. Refactor `MessageManager` so transport callbacks enqueue dispatch work instead of invoking handlers immediately.
3. Integrate dispatcher draining into `NetworkManager.Update`.
4. Add or update edit mode tests for deferred dispatch, FIFO ordering, and invalid payload isolation.
5. Run edit mode tests and update `CodeX-TODO.md` when implementation lands.

## Open Questions

- Whether stage four should enforce a hard queue capacity or only expose queue depth for diagnostics.
- Whether login/bootstrap messages need an explicit early drain during startup before the first regular `Update`.
