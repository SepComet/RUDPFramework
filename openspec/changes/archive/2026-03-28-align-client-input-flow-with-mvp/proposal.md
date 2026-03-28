## Why

The MVP protocol has already been split into `MoveInput` and `ShootInput`, but the client input loop still only emits non-zero movement updates and has no shooting send path. Step 2 is needed now so the client actually drives gameplay through the split message contract before later authoritative state and combat-result work depends on it.

## What Changes

- Define the MVP client gameplay-input flow for movement capture, stop signaling, local prediction, and shooting input capture.
- Require the controlled player to send an explicit zero-vector `MoveInput` when local movement input is released so authoritative movement can stop cleanly.
- Add a `NetworkManager.SendShootInput(...)` path and require client gameplay actions to be sent only as `MoveInput` or `ShootInput`.
- Preserve immediate local movement prediction for the controlled player while keeping local shooting presentation optional and purely cosmetic.
- Add regression coverage for the client-side input flow and the delivery-lane expectations it depends on.

## Capabilities

### New Capabilities
- `client-gameplay-input`: Defines how the MVP client captures movement and shooting intent, predicts local movement, and sends gameplay actions through split message types.

### Modified Capabilities
- `network-gameplay-message-types`: Tighten the gameplay message contract so client gameplay-action send paths use `MoveInput` and `ShootInput` directly instead of legacy broad messages such as `PlayerAction`.
- `network-sync-strategy`: Clarify that explicit zero-vector `MoveInput` updates remain valid sync-lane traffic while `ShootInput` continues to use the reliable lane from the client send path.

## Impact

- Affected Unity-side client code in `Assets/Scripts/MovementComponent.cs`, `Assets/Scripts/NetworkManager.cs`, and related local input/presentation scripts.
- Affected shared or integration expectations for gameplay message selection and delivery-lane behavior.
- Edit-mode regression tests under `Assets/Tests/EditMode/Network/`, plus any focused client-side tests needed for input-flow coverage.
