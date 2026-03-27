## 1. Dispatcher Foundation

- [x] 1.1 Add a `MainThreadNetworkDispatcher` in `Assets/Scripts/Network/NetworkApplication/` that stores queued network work items in a thread-safe FIFO structure.
- [x] 1.2 Define the dispatcher API needed by runtime code, including enqueueing from transport callbacks and draining from the Unity main thread.

## 2. Message Pipeline Refactor

- [x] 2.1 Refactor `MessageManager` so `ITransport.OnReceive` parses envelopes and enqueues dispatch work instead of invoking registered handlers inline.
- [x] 2.2 Preserve current handler registration and invalid-payload handling while moving actual handler execution into the dispatcher drain path.

## 3. Unity Runtime Integration

- [x] 3.1 Integrate the dispatcher into `Assets/Scripts/NetworkManager.cs` so queued network messages are drained from the Unity frame loop.
- [x] 3.2 Ensure transport-side responsibilities remain limited to receive, KCP processing, and enqueue/error handling, with Unity object mutation occurring only after main-thread drain.

## 4. Verification

- [x] 4.1 Add or update edit mode tests to verify receive callbacks defer handler execution until an explicit drain step and preserve FIFO ordering.
- [x] 4.2 Run the relevant network edit mode tests/build and update `CodeX-TODO.md` to reflect stage four progress once the implementation is complete.
