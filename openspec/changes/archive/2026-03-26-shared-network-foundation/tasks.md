## 1. Shared Core Boundary

- [x] 1.1 Introduce a host-dispatch abstraction for the message layer so shared networking code no longer constructs `MainThreadNetworkDispatcher` internally.
- [x] 1.2 Reorganize the reusable transport/message-routing code into a shared client/server network core boundary that does not depend on Unity host classes.

## 2. Client Host Refactor

- [x] 2.1 Update the Unity client host to build the networking stack from the shared core and inject a Unity main-thread dispatcher implementation explicitly.
- [x] 2.2 Keep current client gameplay/UI handler behavior intact while moving Unity-specific frame-loop pumping and host lifecycle logic out of the shared core.

## 3. Server-Oriented Reuse Path

- [x] 3.1 Add a minimal non-Unity host path or test-only server host that constructs the same shared networking core with a non-Unity dispatcher strategy.
- [x] 3.2 Verify that the shared client/server path preserves the existing envelope-based protocol and `ITransport` contract without introducing a protocol fork.

## 4. Verification And Documentation

- [x] 4.1 Add or update tests to cover injected dispatcher behavior, Unity host pumping, and non-Unity host reuse of the same core.
- [x] 4.2 Run the relevant build/tests and update `CodeX-TODO.md` or related docs to reflect that the network foundation is now shared between client and server hosts.
