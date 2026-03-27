## 1. Delivery Policy Infrastructure

- [x] 1.1 Introduce shared delivery-policy abstractions and a default message-type map for reliable control traffic versus high-frequency sync traffic.
- [x] 1.2 Extend `SharedNetworkRuntime`, `MessageManager`, and host composition points to route messages through the resolved policy without breaking the shared envelope contract.
- [x] 1.3 Add the first sync-lane backend and any supporting transport adapter types needed to keep client single-session and server multi-session composition explicit.

## 2. High-Frequency Sync Routing

- [x] 2.1 Route `PlayerInput` and `PlayerState` through the high-frequency sync policy while keeping login, logout, heartbeat, and other control messages on reliable KCP.
- [x] 2.2 Implement monotonic ordering tracking for sync streams and reject stale `PlayerInput` / `PlayerState` updates on the receiving side.
- [x] 2.3 Update server-side sync handling so each remote peer maintains independent latest-wins state instead of relying on reliable ordered delivery.

## 3. Clock Sync And Reconciliation

- [x] 3.1 Introduce a dedicated clock-sync strategy/state object and move authoritative server-tick ownership out of `SessionManager`.
- [x] 3.2 Refactor heartbeat and authoritative-state handlers so liveness/RTT updates stay in session lifecycle while clock samples flow through the sync strategy.
- [x] 3.3 Update client prediction and reconciliation code to prune acknowledged inputs, ignore stale authoritative state, and replay only newer pending inputs.

## 4. Verification And Documentation

- [x] 4.1 Add edit mode tests for delivery-policy routing, stale packet rejection, and clock-sync forwarding behavior.
- [x] 4.2 Add regression tests covering client prediction buffer pruning and server multi-session sync isolation under delayed or out-of-order updates.
- [x] 4.3 Update `CodeX-TODO.md` and related networking docs to reflect the phase 6 architecture and completion criteria.
