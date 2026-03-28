## 1. Verify the MVP protobuf field contract

- [x] 1.1 Verify `Assets/Scripts/Network/Defines/message.proto` defines the MVP fields for `MoveInput`, `ShootInput`, `PlayerState`, and `CombatEvent`, and still exposes `CombatEventType`.
- [x] 1.2 Regenerate or verify `Assets/Scripts/Network/Defines/Message.cs` so the checked-in generated C# fields match the protobuf contract.

## 2. Add regression coverage for gameplay message fields

- [x] 2.1 Add or extend edit-mode tests to prove `MoveInput` and `ShootInput` expose the expected MVP movement and shooting fields through serialization or envelope parsing.
- [x] 2.2 Add or extend edit-mode tests to prove `PlayerState`, `CombatEvent`, and `CombatEventType` expose the expected authoritative-state and combat-result fields.

## 3. Validate the finalized message contract

- [x] 3.1 Run `dotnet build Network.EditMode.Tests.csproj -v minimal`.
- [x] 3.2 Run `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`.
