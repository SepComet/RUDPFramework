## Why

`MessageManager` currently handles transport receive callbacks directly on the transport's background thread, which leaves message dispatch and downstream game state updates one refactor away from touching Unity objects off the main thread. Stage four is the point where the project needs an explicit thread boundary so later connection-state and sync work can build on a safe dispatch model.

## What Changes

- Add a main-thread network dispatch capability that queues decoded transport payloads for processing on Unity's main thread.
- Define that transport background threads are limited to socket receive, KCP session input/update, and basic transport error handling.
- Define that message dispatch, handler execution, game object mutation, and UI-facing reactions run only when the main-thread dispatcher drains queued messages.
- Cover the new threading boundary with architecture-focused tests and document the runtime path expected after stage four.

## Capabilities

### New Capabilities
- `network-main-thread-dispatch`: Defines the queueing and main-thread draining rules between transport receive callbacks and message handler execution.

### Modified Capabilities
- None.

## Impact

- Affected code: `Assets/Scripts/Network/NetworkApplication/MessageManager.cs`, new dispatcher code under `Assets/Scripts/Network/NetworkApplication/`, and related edit mode tests.
- Affected runtime behavior: transport callbacks stop invoking business handlers inline and instead enqueue work for a main-thread pump.
- Dependencies: no new external packages; uses in-process thread-safe queueing and Unity-side update integration.
