## 1. Update Controlled-Player Input Capture

- [x] 1.1 Refine the controlled-player movement input flow so releasing input emits one zero-vector `MoveInput` while continued idle frames do not spam duplicate stop packets.
- [x] 1.2 Preserve immediate local movement prediction for the controlled player across both non-zero movement and release-to-stop transitions.
- [x] 1.3 Add an MVP shooting input capture path in the Unity-side controlled-player flow, including direction/target defaults that fit the current scene setup.

## 2. Align Network Send Boundaries With Split Gameplay Messages

- [x] 2.1 Add `NetworkManager.SendShootInput(...)` and route the new client fire path through `ShootInput`.
- [x] 2.2 Ensure client gameplay actions are sent only as `MoveInput` and `ShootInput`, with no remaining controlled-player dependence on legacy broad gameplay messages such as `PlayerAction`.
- [x] 2.3 Keep any local shooting feedback optional and cosmetic so authoritative combat still depends on server-driven messages.

## 3. Verify MVP Input-Flow Regressions

- [x] 3.1 Add or update tests covering explicit stop-message emission and the split-message-only gameplay send path.
- [x] 3.2 Add or update tests covering that `ShootInput` from the client path resolves to the reliable lane while `MoveInput` stop updates remain sync-lane traffic.
- [x] 3.3 Run `dotnet build Network.EditMode.Tests.csproj -v minimal` and `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal` after implementation.
