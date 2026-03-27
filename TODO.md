# Network MVP TODO

## Goal

Implement the networking MVP described in [MobaSyncMVP.md](D:/Learn/GameLearn/UnityProjects/NetworkFW/MobaSyncMVP.md):

- Client sends only movement and shooting inputs
- Server is authoritative for gameplay state
- Server sends authoritative state and combat events
- Client performs local prediction for movement and interpolation/reconciliation for presentation

## Checklist

### 1. Split Network Message Types

- [ ] Add `MoveInput`, `ShootInput`, and `CombatEvent` to [`Assets/Scripts/Network/Defines/MessageType.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/Defines/MessageType.cs)
- [ ] Add matching protobuf definitions in the source `.proto` file
- [ ] Regenerate [`Assets/Scripts/Network/Defines/Message.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/Defines/Message.cs)
- [ ] Stop using one broad `PlayerInput` message to carry both movement and shooting

Acceptance:

- [ ] `MoveInput`, `ShootInput`, and `CombatEvent` can be referenced independently in code
- [ ] The project builds successfully after regeneration

### 2. Update Delivery Policy Mapping

- [ ] Update [`Assets/Scripts/Network/NetworkApplication/DefaultMessageDeliveryPolicyResolver.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/DefaultMessageDeliveryPolicyResolver.cs)
- [ ] Map `MoveInput` to `HighFrequencySync`
- [ ] Map `PlayerState` to `HighFrequencySync`
- [ ] Map `ShootInput` to `ReliableOrdered`
- [ ] Map `CombatEvent` to `ReliableOrdered`

Acceptance:

- [ ] `MessageManager` routes movement/state messages to the sync lane
- [ ] `MessageManager` routes shooting/combat-result messages to the reliable lane

### 3. Update Sequence Filtering For High-Frequency Messages

- [ ] Modify [`Assets/Scripts/Network/NetworkApplication/SyncSequenceTracker.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/SyncSequenceTracker.cs)
- [ ] Replace `PlayerInput`-based stale filtering with `MoveInput`
- [ ] Keep stale filtering for `PlayerState`
- [ ] Do not apply stale-drop logic to `ShootInput`
- [ ] Do not apply stale-drop logic to `CombatEvent`

Acceptance:

- [ ] Older `MoveInput` packets are dropped
- [ ] Older `PlayerState` packets are dropped
- [ ] `ShootInput` is not silently discarded by sequence filtering

### 4. Narrow Prediction Buffer To Movement

- [ ] Modify [`Assets/Scripts/Network/NetworkApplication/ClientPredictionBuffer.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/ClientPredictionBuffer.cs)
- [ ] Store `MoveInput` instead of broad `PlayerInput`
- [ ] Continue pruning buffered inputs using authoritative `PlayerState.Tick`
- [ ] Keep shooting outside the prediction replay path

Acceptance:

- [ ] Local movement prediction still works
- [ ] Authoritative `PlayerState` still prunes acknowledged movement inputs
- [ ] Shooting does not depend on prediction buffer replay

### 5. Preserve And Use Dual-Transport Runtime Wiring

- [ ] Verify [`Assets/Scripts/Network/NetworkApplication/SharedNetworkRuntime.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkApplication/SharedNetworkRuntime.cs) is used with both reliable and sync transports
- [ ] Verify [`Assets/Scripts/Network/NetworkHost/ServerNetworkHost.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/Network/NetworkHost/ServerNetworkHost.cs) is used with both reliable and sync transports
- [ ] Keep the current dual-transport constructor shape for MVP
- [ ] Do not expand `ITransport` yet unless MVP proves it is necessary

Acceptance:

- [ ] Client runtime can start with two distinct transport instances
- [ ] Server host can start with two distinct transport instances
- [ ] `MoveInput` / `PlayerState` can flow through the sync transport
- [ ] `ShootInput` / `CombatEvent` can flow through the reliable transport

### 6. Finalize MVP Message Fields

- [ ] Define `MoveInput` fields: `playerId`, `tick`, `moveX`, `moveY`
- [ ] Define `ShootInput` fields: `playerId`, `tick`, `dirX`, `dirY`, optional `targetId`
- [ ] Define `PlayerState` fields: `playerId`, `tick`, `position`, `rotation`, `hp`, optional `velocity`
- [ ] Define `CombatEvent` fields: `tick`, `eventType`, `attackerId`, `targetId`, `damage`, optional `hitPosition`
- [ ] Add `CombatEventType` if needed

Acceptance:

- [ ] MVP gameplay data can be expressed without ad hoc payload extensions
- [ ] Position, HP, and combat results all have explicit authoritative messages

### 7. Add Message Routing Tests

- [ ] Extend [`Assets/Tests/EditMode/Network/MessageManagerTests.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Tests/EditMode/Network/MessageManagerTests.cs)
- [ ] Add `SendMessage_MoveInput_UsesSyncLanePolicy`
- [ ] Add `SendMessage_ShootInput_UsesReliableLanePolicy`
- [ ] Add `SendMessage_CombatEvent_UsesReliableLanePolicy`
- [ ] Add `Receive_StaleMoveInput_IsDropped`
- [ ] Add `Receive_ShootInput_IsNotDroppedBySequenceTracker`

Acceptance:

- [ ] Lane selection is covered by tests for all new MVP messages
- [ ] High-frequency stale-drop behavior is covered by tests

### 8. Add Sync Strategy Tests

- [ ] Extend [`Assets/Tests/EditMode/Network/SyncStrategyTests.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Tests/EditMode/Network/SyncStrategyTests.cs)
- [ ] Add `ClientPredictionBuffer_AuthoritativeState_PrunesAcknowledgedMoveInputs`
- [ ] Add `ServerNetworkHost_RejectsStaleMoveInputPerPeerWithoutCrossPeerInterference`

Acceptance:

- [ ] Prediction buffer still behaves correctly after switching to `MoveInput`
- [ ] Multi-session stale filtering remains isolated per peer

### 9. Wire Dual Transports In The Integration Layer

- [ ] Update the client integration entry point, likely [`Assets/Scripts/NetworkManager.cs`](D:/Learn/GameLearn/UnityProjects/NetworkFW/Assets/Scripts/NetworkManager.cs)
- [ ] Update the server startup integration point
- [ ] Instantiate one reliable transport and one sync transport
- [ ] Ensure runtime construction uses both transports instead of a single shared instance

Acceptance:

- [ ] Runtime uses logical dual-lane routing backed by two transport instances
- [ ] Logging or tests confirm movement/state traffic and reliable event traffic are separated

### 10. Build And Test

- [ ] Run `dotnet build Network.EditMode.Tests.csproj -v minimal`
- [ ] Run `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`

Acceptance:

- [ ] Build succeeds
- [ ] Edit-mode network tests succeed
- [ ] New MVP regression tests succeed

## Recommended Order

1. Split protocol and message types
2. Update delivery policy mapping
3. Update sequence filtering
4. Narrow prediction buffer
5. Add and update tests
6. Wire dual transports in integration
7. Build and run tests
