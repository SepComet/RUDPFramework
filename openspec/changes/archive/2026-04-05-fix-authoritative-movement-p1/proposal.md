## Why

P0 fixed tick semantics and server-confirmed movement bootstrap, but the sample still allows client prediction and server authority to advance on different cadences. That keeps controlled-player reconciliation noisy even when both sides share the same movement constants, so the next change needs to make server movement cadence explicit and tighten the client correction path against that contract.

## What Changes

- Define a dedicated authoritative movement cadence contract for the shared server runtime, including a fixed simulation/update interval and explicit diagnostics that let the client compare its local prediction step against server authority timing.
- Tighten the existing server authoritative movement spec so movement simulation and `PlayerState` production are driven by the configured cadence instead of arbitrary caller-provided elapsed values.
- Tighten the client sync strategy so controlled-player reconciliation distinguishes between small cadence-aligned error and large divergence, using bounded correction for the former and hard snap only for the latter.
- Tighten the client authoritative player-state contract so local controlled-player presentation applies cadence-aware correction without changing remote interpolation rules.
- Extend regression coverage to protect cadence alignment, bounded local correction, and large-error snap fallback.

## Capabilities

### New Capabilities
- `authoritative-movement-cadence`: Defines the shared contract for fixed authoritative movement cadence, cadence diagnostics, and server/client observability.

### Modified Capabilities
- `server-authoritative-movement`: Require server-side movement simulation and snapshot production to follow the configured authoritative cadence.
- `network-sync-strategy`: Require cadence-aware local reconciliation with bounded correction for small error and snap fallback for large divergence.
- `client-authoritative-player-state`: Require the controlled-player presentation path to consume cadence-aware correction results without changing remote authoritative ownership.
- `gameplay-flow-regression-coverage`: Require edit-mode regressions for cadence alignment and controlled-player correction behavior.

## Impact

Affected areas include `ServerRuntimeHandle`, `ServerAuthoritativeMovementCoordinator`, client movement/reconciliation code in `MovementComponent` and related sync helpers, and edit-mode network regression tests. No new transport or session-lifecycle capability is introduced, but movement diagnostics and correction policy become more explicit.
