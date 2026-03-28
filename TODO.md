# Network MVP TODO

## Goal

Implement the networking MVP described in [MobaSyncMVP.md](D:/Learn/GameLearn/UnityProjects/NetworkFW/MobaSyncMVP.md):

- Client sends only movement and shooting inputs
- Server is authoritative for gameplay state
- Server sends authoritative state and combat events
- Client performs local prediction for movement and interpolation/reconciliation for presentation

## Checklist

### 1. Split Network Message Types

- [x] Add `MoveInput`, `ShootInput`, and `CombatEvent` to [`Assets/Scripts/Network/Defines/MessageType.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/Defines/MessageType.cs)
- [x] Add matching protobuf definitions in the source `.proto` file
- [x] Regenerate [`Assets/Scripts/Network/Defines/Message.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/Defines/Message.cs)
- [x] Stop using one broad `PlayerInput` message to carry both movement and shooting

Acceptance:

- [x] `MoveInput`, `ShootInput`, and `CombatEvent` can be referenced independently in code
- [x] The project builds successfully after regeneration

### 2. Update Delivery Policy Mapping

- [x] Update [`Assets/Scripts/Network/NetworkApplication/DefaultMessageDeliveryPolicyResolver.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/DefaultMessageDeliveryPolicyResolver.cs)
- [x] Map `MoveInput` to `HighFrequencySync`
- [x] Map `PlayerState` to `HighFrequencySync`
- [x] Map `ShootInput` to `ReliableOrdered`
- [x] Map `CombatEvent` to `ReliableOrdered`

Acceptance:

- [x] `MessageManager` routes movement/state messages to the sync lane
- [x] `MessageManager` routes shooting/combat-result messages to the reliable lane

### 3. Update Sequence Filtering For High-Frequency Messages

- [x] Modify [`Assets/Scripts/Network/NetworkApplication/SyncSequenceTracker.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/SyncSequenceTracker.cs)
- [x] Replace `PlayerInput`-based stale filtering with `MoveInput`
- [x] Keep stale filtering for `PlayerState`
- [x] Do not apply stale-drop logic to `ShootInput`
- [x] Do not apply stale-drop logic to `CombatEvent`

Acceptance:

- [x] Older `MoveInput` packets are dropped
- [x] Older `PlayerState` packets are dropped
- [x] `ShootInput` is not silently discarded by sequence filtering

### 4. Narrow Prediction Buffer To Movement

- [x] Modify [`Assets/Scripts/Network/NetworkApplication/ClientPredictionBuffer.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/ClientPredictionBuffer.cs)
- [x] Store `MoveInput` instead of broad `PlayerInput`
- [x] Continue pruning buffered inputs using authoritative `PlayerState.Tick`
- [x] Keep shooting outside the prediction replay path

Acceptance:

- [x] Local movement prediction still works
- [x] Authoritative `PlayerState` still prunes acknowledged movement inputs
- [x] Shooting does not depend on prediction buffer replay

### 5. Preserve And Use Dual-Transport Runtime Wiring

- [x] Verify [`Assets/Scripts/Network/NetworkApplication/SharedNetworkRuntime.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/SharedNetworkRuntime.cs) is used with both reliable and sync transports
- [x] Verify [`Assets/Scripts/Network/NetworkHost/ServerNetworkHost.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkHost/ServerNetworkHost.cs) is used with both reliable and sync transports
- [x] Keep the current dual-transport constructor shape for MVP
- [x] Do not expand `ITransport` yet unless MVP proves it is necessary

Acceptance:

- [x] Client runtime can start with two distinct transport instances
- [x] Server host can start with two distinct transport instances
- [x] `MoveInput` / `PlayerState` can flow through the sync transport
- [x] `ShootInput` / `CombatEvent` can flow through the reliable transport

### 6. Finalize MVP Message Fields

- [x] Define `MoveInput` fields: `playerId`, `tick`, `moveX`, `moveY`
- [x] Define `ShootInput` fields: `playerId`, `tick`, `dirX`, `dirY`, optional `targetId`
- [x] Define `PlayerState` fields: `playerId`, `tick`, `position`, `rotation`, `hp`, optional `velocity`
- [x] Define `CombatEvent` fields: `tick`, `eventType`, `attackerId`, `targetId`, `damage`, optional `hitPosition`
- [x] Add `CombatEventType` if needed

Acceptance:

- [x] MVP gameplay data can be expressed without ad hoc payload extensions
- [x] Position, HP, and combat results all have explicit authoritative messages

### 7. Add Message Routing Tests

- [x] Extend [`Assets/Tests/EditMode/Network/MessageManagerTests.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Tests/EditMode/Network/MessageManagerTests.cs)
- [x] Add `SendMessage_MoveInput_UsesSyncLanePolicy`
- [x] Add `SendMessage_ShootInput_UsesReliableLanePolicy`
- [x] Add `SendMessage_CombatEvent_UsesReliableLanePolicy`
- [x] Add `Receive_StaleMoveInput_IsDropped`
- [x] Add `Receive_ShootInput_IsNotDroppedBySequenceTracker`

Acceptance:

- [x] Lane selection is covered by tests for all new MVP messages
- [x] High-frequency stale-drop behavior is covered by tests

### 8. Add Sync Strategy Tests

- [x] Extend [`Assets/Tests/EditMode/Network/SyncStrategyTests.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Tests/EditMode/Network/SyncStrategyTests.cs)
- [x] Add `ClientPredictionBuffer_AuthoritativeState_PrunesAcknowledgedMoveInputs`
- [x] Add `ServerNetworkHost_RejectsStaleMoveInputPerPeerWithoutCrossPeerInterference`

Acceptance:

- [x] Prediction buffer still behaves correctly after switching to `MoveInput`
- [x] Multi-session stale filtering remains isolated per peer

### 9. Wire Dual Transports In The Integration Layer

- [ ] Update the client integration entry point, likely [`Assets/Scripts/NetworkManager.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/NetworkManager.cs)
- [ ] Update the server startup integration point
- [ ] Instantiate one reliable transport and one sync transport
- [ ] Ensure runtime construction uses both transports instead of a single shared instance

Acceptance:

- [ ] Runtime uses logical dual-lane routing backed by two transport instances
- [ ] Logging or tests confirm movement/state traffic and reliable event traffic are separated

### 10. Build And Test

- [x] Run `dotnet build Network.EditMode.Tests.csproj -v minimal`
- [x] Run `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`

Acceptance:

- [x] Build succeeds
- [x] Edit-mode network tests succeed
- [x] New MVP regression tests succeed

## Recommended Order

1. Split protocol and message types
2. Update delivery policy mapping
3. Update sequence filtering
4. Narrow prediction buffer
5. Add and update tests
6. Wire dual transports in integration
7. Build and run tests
