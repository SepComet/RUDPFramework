## Why

The MVP TODO requires a stable default mapping between gameplay message types and delivery lanes so split movement, state, shooting, and combat-result messages do not regress back onto one transport policy. This needs to be formalized now because the routing resolver is the contract that keeps the sync lane limited to latest-wins traffic while preserving reliable delivery for gameplay events.

## What Changes

- Define the default delivery-policy mapping for shared gameplay message routing in the networking runtime.
- Require `MoveInput` and `PlayerState` to resolve to `HighFrequencySync` in the default resolver used by `MessageManager`.
- Require `ShootInput` and `CombatEvent` to continue resolving through the reliable ordered default path instead of the sync lane.
- Add regression coverage that proves movement/state traffic uses the sync lane while shooting/combat-result traffic remains on the reliable lane.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `network-sync-strategy`: Clarify the default resolver mapping that sends `MoveInput` and `PlayerState` through the high-frequency sync lane while `ShootInput` and `CombatEvent` remain on the reliable ordered lane.

## Impact

- Affected code: `Assets/Scripts/Network/NetworkApplication/DefaultMessageDeliveryPolicyResolver.cs` and the `MessageManager` send path that consults the resolver.
- Affected behavior: the runtime keeps latest-wins movement/state traffic off the reliable lane by default, while shooting requests and combat results keep reliable ordered delivery semantics.
- Affected tests: edit-mode message-routing tests need explicit assertions for sync-lane and reliable-lane selection for the split MVP gameplay message types.
