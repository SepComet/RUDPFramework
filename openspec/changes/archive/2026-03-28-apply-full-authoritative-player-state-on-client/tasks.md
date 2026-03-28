## 1. Authoritative State Ownership

- [x] 1.1 Add a client-side authoritative player-state snapshot model or owner on `Player` so position, rotation, HP, and optional velocity have one explicit owner per player.
- [x] 1.2 Update the `NetworkManager -> MasterManager -> Player` receive path so accepted `PlayerState` packets refresh that owned authoritative snapshot before presentation reads it.

## 2. Client Application

- [x] 2.1 Update local-player reconciliation so authoritative `PlayerState.Tick` still drives prediction replay while authoritative position and rotation are applied from the accepted snapshot.
- [x] 2.2 Update remote-player presentation to consume authoritative position, rotation, HP, and optional velocity from the owned snapshot without inventing gameplay truth.
- [x] 2.3 Expose authoritative HP or comparable authoritative-state diagnostics in the current MVP UI so server-truth changes are visible during development.

## 3. Verification

- [x] 3.1 Add or extend edit-mode tests for authoritative `PlayerState` ownership and stale-packet rejection on the client side where practical.
- [x] 3.2 Add or extend regression tests for local reconciliation and remote authoritative-state application behavior that this step changes.
- [x] 3.3 Run the relevant validation flow and confirm the client-side authoritative `PlayerState` path works in editor play/testing.
