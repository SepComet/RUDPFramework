## Why

The client currently treats authoritative `PlayerState` mostly as a position-correction signal, so rotation, HP, and optional velocity do not have one explicit owner on the receiving side. TODO step 3 now depends on making server truth visible and consistently applied on both local and remote players before snapshot interpolation or combat-result handling can build on top of it.

## What Changes

- Introduce an explicit client-side authoritative player-state capability that defines where received `PlayerState` data lives and how local versus remote presentation consumes it.
- Apply authoritative `position`, `rotation`, `hp`, and optional `velocity` on the client while keeping local-player reconciliation keyed by authoritative `PlayerState.Tick`.
- Expose authoritative HP and related state changes through existing player-facing UI or diagnostics so MVP development can observe server truth during playtests.
- Keep remote authoritative-state application minimal for this step and leave buffered interpolation behavior to the next TODO step.

## Capabilities

### New Capabilities
- `client-authoritative-player-state`: Defines client-side ownership, application, and observability of authoritative `PlayerState` data for local and remote players.

### Modified Capabilities
- None.

## Impact

- Affected code: `Assets/Scripts/MovementComponent.cs`, `Assets/Scripts/Player.cs`, `Assets/Scripts/MasterManager.cs`, and client UI/diagnostic scripts that expose authoritative state.
- Affected behavior: Local reconciliation remains prediction-based but consumes full authoritative state, while remote players stop relying on ad hoc position-only updates.
- Testing: Edit-mode regressions will need coverage for authoritative-state application, stale-state rejection, and explicit ownership of HP/rotation data on the client.
