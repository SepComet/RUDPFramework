## 1. Protocol And Spec Alignment

- [x] 1.1 Update the shared gameplay message schema and generated code so `PlayerState` carries an explicit acknowledged movement-input tick.
- [x] 1.2 Align OpenSpec-linked message construction and parsing paths with the new `PlayerState` field semantics.
- [x] 1.3 Define or wire the server-confirmed movement bootstrap data used by the controlled client after login succeeds.

## 2. Authoritative Movement Runtime

- [x] 2.1 Update the server authoritative movement state and broadcast builder so each `PlayerState` includes both snapshot tick and last accepted `MoveInput.Tick`.
- [x] 2.2 Update client reconciliation and prediction-buffer pruning to use the acknowledged movement-input tick instead of `PlayerState.Tick`.
- [x] 2.3 Switch controlled-client steady-state prediction parameters to the server-confirmed authoritative movement values.

## 3. Regression Coverage

- [x] 3.1 Add or update edit-mode tests that prove snapshot tick and acknowledged movement-input tick remain distinct in authoritative movement broadcasts.
- [x] 3.2 Add or update client reconciliation tests so only inputs at or before the acknowledged tick are pruned.
- [x] 3.3 Add or update gameplay-flow round-trip coverage for server-confirmed movement bootstrap and authoritative movement convergence.
