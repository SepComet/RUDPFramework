## 1. Lock The Default Delivery Mapping

- [x] 1.1 Verify `Assets/Scripts/Network/NetworkApplication/DefaultMessageDeliveryPolicyResolver.cs` explicitly maps `MessageType.MoveInput` and `MessageType.PlayerState` to `DeliveryPolicy.HighFrequencySync`.
- [x] 1.2 Verify the default resolver leaves `MessageType.ShootInput`, `MessageType.CombatEvent`, and control-plane messages on the `DeliveryPolicy.ReliableOrdered` fallback path.
- [x] 1.3 Confirm `MessageManager` continues consulting the resolver before selecting the reliable or sync transport lane.

## 2. Protect Lane Selection With Regression Tests

- [x] 2.1 Keep or add edit-mode routing tests proving `MoveInput` uses the sync lane and does not send through the reliable transport.
- [x] 2.2 Keep or add edit-mode routing tests proving `ShootInput` and `CombatEvent` use the reliable lane and do not send through the sync transport.
- [x] 2.3 Keep or add coverage that control-plane traffic still defaults to the reliable ordered lane when the default resolver is used.

## 3. Validate The Step-2 Contract

- [x] 3.1 Build `Network.EditMode.Tests.csproj -v minimal` to verify the delivery-mapping change does not break the shared networking assemblies.
- [x] 3.2 Run `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal` to confirm the routing regression suite passes.
- [x] 3.3 Update `TODO.md` or related implementation notes only if verification shows the step-2 completion markers need correction.
