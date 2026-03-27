## 1. Shared Multi-Session Lifecycle Core

- [x] 1.1 Introduce a shared multi-session lifecycle coordinator that owns per-session `SessionManager` instances keyed by remote identity.
- [x] 1.2 Add APIs for per-session lookup, enumeration, lifecycle event observation, and explicit session removal without changing the existing session state vocabulary.
- [x] 1.3 Route transport activity, login results, heartbeat updates, timeout evaluation, and reconnect bookkeeping through the coordinator on a per-session basis.

## 2. Host Composition

- [x] 2.1 Rework `ServerNetworkHost` to use the shared multi-session coordinator instead of exposing only one runtime-level `SessionManager`.
- [x] 2.2 Preserve the current client-side single-session composition path so `NetworkManager` and `SharedNetworkRuntime` remain valid for one-server connectivity.
- [x] 2.3 Define how remote identity is mapped into session keys and ensure session cleanup does not disturb unrelated peers.

## 3. Verification

- [x] 3.1 Add edit-mode tests that verify two or more server-side sessions can progress through login, timeout, and disconnect independently.
- [x] 3.2 Add regression tests that confirm the client-side single-session lifecycle path still behaves as before.
- [x] 3.3 Build the edit-mode test project and run the network-related test suite to confirm no lifecycle regressions remain.

## 4. Documentation

- [x] 4.1 Update `CodeX-TODO.md` to reflect that stage 5 lifecycle support now covers server-side multi-session management.
- [x] 4.2 Document the new shared multi-session entry points and server-observable session states in change-related docs or code comments where needed.

