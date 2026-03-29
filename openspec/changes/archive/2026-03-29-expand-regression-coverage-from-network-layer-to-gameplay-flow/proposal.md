## Why

The MVP networking flow now has concrete client input, client authoritative-state application, remote interpolation, and server-authoritative movement/combat behavior, but regression coverage still skews toward transport and isolated network-layer rules. Step 9 is needed now so future changes cannot silently push gameplay authority, routing, or presentation flow back toward client-side guesswork.

## What Changes

- Add a dedicated gameplay-flow regression coverage capability for edit-mode network tests.
- Define regression expectations for client `ShootInput` send routing, client `CombatEvent` receive/apply flow, remote `PlayerState` buffering/interpolation decisions, and fake-transport end-to-end gameplay message flow.
- Keep `MessageManagerTests` focused on lane-policy regressions only, with broader gameplay assertions moving into higher-level flow tests.
- Require coverage for both client single-session behavior and server multi-session authority paths where the MVP gameplay loop crosses that boundary.

## Capabilities

### New Capabilities
- `gameplay-flow-regression-coverage`: Define the required regression coverage that protects the MVP gameplay loop from client input through authoritative server state/combat results and client-side application.

### Modified Capabilities
- None.

## Impact

Affected areas include `Assets/Tests/EditMode/Network/`, lightweight fake-transport/runtime test fixtures, and any minimal runtime/test seams needed to observe client gameplay flow without changing shared networking contracts. This change should not alter production transport policy or MVP message definitions.
