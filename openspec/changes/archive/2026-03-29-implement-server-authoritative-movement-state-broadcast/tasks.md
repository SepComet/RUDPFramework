## 1. Server Authority Core

- [x] 1.1 Add a dedicated server-authoritative movement coordinator and per-peer authoritative movement state model under `Assets/Scripts/Network/`.
- [x] 1.2 Register `MoveInput` handling through the server host/runtime composition path and validate sender-scoped movement payloads before accepting them.
- [x] 1.3 Keep stale movement tick acceptance independent per managed peer and ensure zero-vector input clears authoritative movement velocity.

## 2. Authoritative Snapshot Broadcast

- [x] 2.1 Add an explicit server authority update hook that advances authoritative movement resolution on a fixed cadence.
- [x] 2.2 Broadcast authoritative `PlayerState` snapshots through the existing `MessageManager` sync-lane contract, with reliable fallback when no sync transport exists.
- [x] 2.3 Expose the minimal runtime/host surface needed for host processes and tests to drive movement authority updates and inspect authoritative peer state.

## 3. Regression Coverage And Documentation

- [x] 3.1 Add edit-mode regression tests for accepted vs stale `MoveInput` handling across multiple peers.
- [x] 3.2 Add edit-mode regression tests for zero-vector movement stop and fixed-cadence `PlayerState` broadcasting on sync and fallback lanes.
- [x] 3.3 Update `TODO.md` and related change tracking/docs to reflect the completed server-authoritative movement/state broadcast work.
