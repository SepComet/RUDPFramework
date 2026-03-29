## 1. Client Gameplay Flow Coverage

- [x] 1.1 Add or extend edit-mode client tests that prove fire intent sends `ShootInput` through the dedicated gameplay path and only retain `MessageManagerTests` assertions needed for reliable-lane policy regressions.
- [x] 1.2 Add edit-mode regression tests that deliver authoritative `CombatEvent` messages through the client receive path and verify player-owned authoritative state, presentation, or diagnostics apply hit/damage/death/rejection results from server truth.
- [x] 1.3 Add any minimal production-safe observability seams needed for the client gameplay tests without introducing Unity dependencies into shared networking code.

## 2. Snapshot And End-To-End Coverage

- [x] 2.1 Add edit-mode regression tests that cover remote `PlayerState` buffering, stale snapshot rejection, and interpolation-versus-clamp decisions where practical.
- [x] 2.2 Add at least one deterministic fake-transport gameplay-flow test that drives `MoveInput -> PlayerState` through the authoritative server path.
- [x] 2.3 Extend that fake-transport gameplay-flow coverage to also drive `ShootInput -> CombatEvent` and verify the combined flow protects client single-session input behavior plus server multi-session authority.

## 3. Tracking And Verification

- [x] 3.1 Update `TODO.md` to mark the gameplay-flow regression coverage items completed or narrowed to any remaining follow-up work.
- [x] 3.2 Keep the new regression suite organized under `Assets/Tests/EditMode/Network/` with names and fixtures that separate lane-policy tests from higher-level gameplay-flow tests.
- [x] 3.3 Run `dotnet build Network.EditMode.Tests.csproj -v minimal` and `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`, then record the actual result in the implementation summary.
