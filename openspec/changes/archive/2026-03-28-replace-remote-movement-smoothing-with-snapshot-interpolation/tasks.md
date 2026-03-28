## 1. Remote Snapshot Buffer

- [x] 1.1 Add a focused client-side remote snapshot interpolation helper that stores accepted authoritative `PlayerState` samples in tick order with bounded buffer size and receive-time metadata.
- [x] 1.2 Ensure stale or duplicate remote `PlayerState` samples are rejected before they can enter the interpolation buffer, while keeping `ClientAuthoritativePlayerState` as the latest authoritative owner.
- [x] 1.3 Document the chosen interpolation delay and sample-selection strategy in code comments or adjacent docs where the helper is introduced.

## 2. Client Presentation Integration

- [x] 2.1 Update remote-player presentation in `MovementComponent` to read from the interpolation helper and interpolate authoritative position and rotation between buffered snapshots instead of lerping directly to the latest snapshot.
- [x] 2.2 Keep the local-player reconcile path unchanged and ensure remote fallback behavior clamps to the latest accepted authoritative snapshot when interpolation cannot bracket two samples.
- [x] 2.3 Keep remote players presentation-only by avoiding extrapolation or any new remote prediction path while continuing to consume authoritative ownership from `Player`.

## 3. Validation

- [x] 3.1 Add or extend edit-mode tests for remote snapshot buffering, stale-packet rejection, and bounded-buffer behavior.
- [x] 3.2 Add or extend regression tests for remote interpolation output or fallback-to-latest behavior when samples are insufficient or out of order.
- [x] 3.3 Run the relevant validation flow and confirm the new remote snapshot interpolation path works in editor or CLI testing.
