# Network MVP TODO

## Goal

Make the current project actually satisfy the MVP described in [MobaSyncMVP.md](./MobaSyncMVP.md):

- Client sends only `MoveInput` and `ShootInput`
- Server owns gameplay truth for position, HP, combat resolution, and validation
- Server sends authoritative `PlayerState` and `CombatEvent`
- Client predicts only local movement
- Client reconciles local state and interpolates remote state for presentation

## Current Audit Summary

Already in place:

- [x] `MoveInput` / `ShootInput` / `CombatEvent` protocol split is done
- [x] Delivery policy mapping is aligned with sync lane vs reliable lane
- [x] High-frequency stale filtering is limited to `MoveInput` and `PlayerState`
- [x] Client prediction buffer is narrowed to movement
- [x] Dual-transport runtime wiring exists in the shared network layer
- [x] Network-layer regression tests exist for routing and stale filtering

Still missing for MVP:

- [ ] Client-side `ShootInput` send path
- [ ] Client-side `CombatEvent` receive/apply path
- [x] Server startup path that actually uses `ServerNetworkHost`
- [x] Server-authoritative movement/state loop
- [ ] Server-authoritative shooting/combat resolution loop
- [ ] Full `PlayerState` field application for rotation / HP / velocity
- [ ] Remote-player snapshot buffering and interpolation strategy
- [x] Explicit movement-stop handling via zero-input `MoveInput`
- [ ] End-to-end gameplay regression coverage
- [ ] Re-run build/test in an environment with the required .NET runtime installed

## Checklist

### 1. Keep The Shared Networking Foundation Stable

- [x] Keep `MoveInput`, `ShootInput`, `PlayerState`, and `CombatEvent` as the MVP gameplay messages
- [x] Keep `MoveInput` and `PlayerState` on `HighFrequencySync`
- [x] Keep `ShootInput` and `CombatEvent` on `ReliableOrdered`
- [x] Keep stale-drop logic only for `MoveInput` and `PlayerState`
- [x] Keep client prediction buffering limited to `MoveInput`
- [x] Keep dual-transport runtime construction in [`Assets/Scripts/Network/NetworkApplication/NetworkIntegrationFactory.cs`](./Assets/Scripts/Network/NetworkApplication/NetworkIntegrationFactory.cs)

Acceptance:

- [x] Network-layer message routing still matches the MVP transport mapping
- [x] Sequence filtering still matches the MVP tick rules
- [x] Shared runtime and host still support separate reliable and sync transports

### 2. Align Client Input Flow With MVP

- [x] Update [`Assets/Scripts/MovementComponent.cs`](./Assets/Scripts/MovementComponent.cs) so movement intent can send an explicit zero-vector stop message when the player releases input
- [x] Keep local prediction immediate for the controlled player
- [x] Add a client shooting input capture path
- [x] Add `NetworkManager.SendShootInput(...)`
- [x] Ensure the client sends only `MoveInput` and `ShootInput` for gameplay actions
- [x] Keep local shooting presentation optional and purely cosmetic

Acceptance:

- [x] Releasing movement input produces a final `MoveInput` that stops authoritative movement
- [x] Firing produces a `ShootInput` sent on the reliable lane
- [x] No MVP gameplay action depends on legacy broad messages such as `PlayerAction`

### 3. Apply Full Authoritative `PlayerState` On The Client

- [x] Extend the player-side presentation model to consume authoritative `position`, `rotation`, `hp`, and optional `velocity`
- [x] Keep local-player reconciliation driven by authoritative `PlayerState.Tick`
- [x] Use authoritative HP instead of any local guesswork
- [x] Decide where authoritative player state lives on the client side and keep that ownership explicit
- [x] Update UI or diagnostics so authoritative HP/state changes are observable during development

Acceptance:

- [x] Local player corrects to server truth for position and rotation
- [x] Local and remote players expose authoritative HP from `PlayerState`
- [x] The client does not finalize gameplay truth outside authoritative messages

### 4. Replace Ad-Hoc Remote Movement Smoothing With Snapshot Interpolation

- [x] Add a small `PlayerState` snapshot buffer for remote players
- [x] Interpolate between buffered snapshots instead of lerping directly to the latest state
- [x] Discard stale snapshots by tick
- [x] Keep remote players non-predicted
- [x] Document the interpolation delay / sample strategy in code comments or docs if it is non-obvious

Acceptance:

- [x] Remote movement is based on buffered authoritative snapshots
- [x] Out-of-order remote `PlayerState` packets do not corrupt presentation
- [x] Remote players are smoothed without becoming locally authoritative

### 5. Add Client-Side `CombatEvent` Handling

- [x] Register a `CombatEvent` handler in [`Assets/Scripts/NetworkManager.cs`](./Assets/Scripts/NetworkManager.cs)
- [x] Route combat results to the relevant player or combat presentation components
- [x] Apply hit / damage / death / shoot-rejected results from server truth
- [x] Keep local fire FX separate from authoritative damage and death resolution
- [x] Add UI or debug output for combat-result visibility during MVP development

Acceptance:

- [x] `CombatEvent` updates HP, death state, or hit feedback on clients
- [x] `ShootRejected` can be surfaced without client-side authoritative rollback logic
- [x] Combat results are driven by server messages, not speculative client outcomes

### 6. Add A Real Server Startup / Integration Entry Point

- [x] Add or update the runtime server bootstrap so production code actually constructs [`ServerNetworkHost`](./Assets/Scripts/Network/NetworkHost/ServerNetworkHost.cs) via [`ServerRuntimeEntryPoint`](./Assets/Scripts/Network/NetworkHost/ServerRuntimeEntryPoint.cs)
- [x] Start both reliable and sync transports from the server integration layer
- [x] Drain server pending messages on a regular loop through [`ServerRuntimeHandle`](./Assets/Scripts/Network/NetworkHost/ServerRuntimeHandle.cs)
- [x] Preserve server lifecycle diagnostics and visibility through the existing `ServerNetworkHost` lifecycle surface and metrics hooks
- [x] Make the startup path easy to locate and test

Acceptance:

- [x] There is a concrete server startup path in production code, not only shared infrastructure and tests
- [x] Server runtime uses two distinct transport instances when sync port is configured
- [x] Server can receive gameplay traffic on both lanes

### 7. Implement Server-Authoritative Movement And State Broadcast

- [x] Register `MoveInput` handling on the server
- [x] Maintain authoritative per-player movement state on the server
- [x] Validate and apply move input before mutating authoritative state
- [x] Use tick-aware stale filtering per peer without cross-peer interference
- [x] Broadcast authoritative `PlayerState` snapshots on the sync lane at a fixed cadence
- [x] Ensure zero-vector movement input stops authoritative movement

Acceptance:

- [x] Server owns final position and movement resolution
- [x] Clients receive authoritative `PlayerState` snapshots for reconciliation/interpolation
- [x] Movement stop is reflected by server-authoritative state, not just local client visuals

### 8. Implement Server-Authoritative Shooting And Combat Resolution

- [ ] Register `ShootInput` handling on the server
- [ ] Validate shoot requests before accepting them
- [ ] Resolve hit, damage, death, and rejection on the server
- [ ] Broadcast `CombatEvent` on the reliable lane
- [ ] Reflect authoritative HP changes in subsequent `PlayerState` snapshots
- [ ] Keep server combat resolution independent from cosmetic client preplay

Acceptance:

- [ ] Server decides whether shooting is valid
- [ ] Server emits authoritative `CombatEvent` for damage/death/rejection
- [ ] Clients update combat state from server truth

### 9. Expand Regression Coverage From Network Layer To Gameplay Flow

- [ ] Extend [`Assets/Tests/EditMode/Network/MessageManagerTests.cs`](./Assets/Tests/EditMode/Network/MessageManagerTests.cs) only as needed for lane policy regressions
- [x] Add tests that cover explicit zero-input movement stop behavior
- [ ] Add tests for client `ShootInput` send routing
- [ ] Add tests for `CombatEvent` receive/apply behavior
- [ ] Add tests for remote `PlayerState` buffering / interpolation decisions where practical
- [x] Add tests for server-authoritative movement processing
- [ ] Add tests for server-authoritative shooting/combat result generation
- [ ] Add at least one end-to-end fake-transport test that covers `MoveInput -> PlayerState` and `ShootInput -> CombatEvent`

Acceptance:

- [ ] MVP gameplay flow is covered beyond transport-only assertions
- [ ] Both client single-session and server multi-session behaviors remain protected
- [ ] Regression tests fail if movement/combat authority accidentally drifts back to the client

### 10. Re-Verify Build And Test

- [ ] Install or use an environment that contains the required .NET runtime for this repository
- [ ] Run `dotnet build Network.EditMode.Tests.csproj -v minimal`
- [ ] Run `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`
- [ ] Record the actual result after the environment issue is resolved

Acceptance:

- [ ] Build succeeds in a runnable local environment
- [ ] Edit-mode network tests succeed
- [ ] New MVP gameplay regression tests succeed

## Recommended Order

1. Keep the shared networking foundation unchanged
2. Fix client input flow, especially stop movement and `ShootInput`
3. Add real server startup and authoritative movement/state broadcast
4. Add authoritative shooting/combat resolution and `CombatEvent`
5. Apply full authoritative state and combat results on the client
6. Upgrade remote interpolation from direct lerp to snapshot buffering
7. Add gameplay-flow regression tests
8. Re-run build and test once the .NET runtime issue is resolved
