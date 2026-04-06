## Why

The current `MovementComponent.AccumulateLatest()` uses `Time.fixedDeltaTime` (typically 20ms Unity physics step) to accumulate pending input duration, while the server uses a fixed 50ms authoritative movement cadence. Mixing these two cadences in reconciliation-sensitive paths causes prediction timing to drift from authoritative timing, contributing to controlled-player jitter under steady input.

## What Changes

- Replace `Time.fixedDeltaTime`-based accumulation with an explicit prediction cadence derived from the server's `SimulationInterval` (50ms)
- The client's forward prediction accumulation aligns with the server's authoritative cadence, ensuring `SimulatedDurationSeconds` reflects server-time rather than render-loop time
- No external API changes; internal prediction timing refactored

## Capabilities

### New Capabilities

- `client-prediction-cadence`: Client forward prediction uses an explicit cadence derived from the server authoritative movement cadence, not `Time.fixedDeltaTime`, ensuring prediction timing aligns with authoritative timing in reconciliation-sensitive paths

### Modified Capabilities

- `client-prediction-replay`: Update requirement to clarify that replay substep size and forward prediction accumulation cadence both derive from the server authoritative movement cadence (already implied by existing spec, making explicit)

## Impact

- **Affected code**: `MovementComponent.AccumulateLatest()`, `MovementComponent.FixedUpdate()`
- **No breaking API changes** to message types or transport
- **No breaking changes** to physics integration (`Simulate` still uses `Time.fixedDeltaTime` for physics)
