## Why

The networking layer can now start a real server runtime, but the server still does not own player movement or produce authoritative `PlayerState` snapshots. Until that loop exists, client reconciliation and remote interpolation remain disconnected from actual server truth, so the MVP still relies on local visuals instead of authoritative simulation.

## What Changes

- Add a concrete server-authoritative movement capability that accepts `MoveInput`, validates it per peer, updates authoritative movement state, and emits `PlayerState` snapshots on a fixed sync cadence.
- Introduce explicit server-side movement ownership for position, velocity, rotation, and last accepted movement tick so zero-vector input can stop movement through server truth.
- Keep stale-input filtering peer-scoped so one client's out-of-order `MoveInput` packets cannot suppress another client's movement updates.
- Define the server broadcast contract for authoritative `PlayerState` snapshots so clients can reconcile the local player and interpolate remote players from server output.

## Capabilities

### New Capabilities
- `server-authoritative-movement`: Server-side handling of `MoveInput`, authoritative movement state mutation, and fixed-cadence `PlayerState` broadcast.

### Modified Capabilities
- `multi-session-lifecycle`: Server multi-session coordination also tracks authoritative movement state and stale-input evaluation independently for each managed peer.

## Impact

Affected areas include the shared server host/runtime under `Assets/Scripts/Network/`, server-side gameplay state ownership, authoritative `PlayerState` broadcast wiring, and edit-mode regression coverage for multi-peer movement handling.
