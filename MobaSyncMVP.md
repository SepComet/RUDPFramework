# MOBA Hybrid Sync MVP

## Goal

Build a minimal hybrid sync model for a MOBA-like game:

- Client sends only player inputs
- Server runs authoritative gameplay logic
- Server sends authoritative state and combat results back to clients
- Client uses prediction for local control and interpolation/reconciliation for presentation

This MVP supports only two player inputs:

- Move
- Shoot

Other core gameplay data such as position and HP remain server-authoritative.

## Core Model

### Client Responsibilities

- Capture local input
- Send input messages to server
- Predict local movement immediately
- Play local shooting presentation immediately if desired
- Reconcile local player state when authoritative state arrives
- Interpolate remote player movement for smooth display

### Server Responsibilities

- Receive all player inputs
- Validate and apply movement
- Validate shooting requests
- Resolve hit, damage, death, and other combat results
- Maintain authoritative position, HP, and combat state
- Broadcast authoritative state snapshots
- Broadcast authoritative combat events

## Message Plan

| Message | Direction | Reliability | Frequency | Purpose |
|---|---|---|---|---|
| `MoveInput` | Client -> Server | Low reliability / latest wins | 10-20 Hz | Report movement input |
| `ShootInput` | Client -> Server | Reliable ordered | On demand | Report shoot request |
| `PlayerState` | Server -> Client | Low reliability / latest wins | 10-20 Hz | Sync position, rotation, HP |
| `CombatEvent` | Server -> Client | Reliable ordered | On demand | Sync hit, damage, death |
| `Heartbeat` / `ClockSync` | Both ways | Reliable ordered | 1-2 Hz | Keepalive and server tick sync |

## Message Definitions

### MoveInput

Sent frequently. Old packets can be dropped.

Suggested fields:

- `playerId`
- `tick`
- `moveX`
- `moveY`

Notes:

- Represents current movement intent
- Should be treated as a high-frequency sync message
- Latest input matters more than full delivery history

### ShootInput

Sent only when player fires.

Suggested fields:

- `playerId`
- `tick`
- `dirX`
- `dirY`
- optional `targetId`

Notes:

- Must be delivered reliably
- Server decides whether shooting is valid
- Client may play local muzzle flash or firing animation immediately, but not authoritative hit resolution

### PlayerState

Sent from server as authoritative snapshot.

Suggested fields:

- `playerId`
- `tick`
- `position`
- `rotation`
- `hp`
- optional `velocity`

Notes:

- Used for local reconciliation and remote interpolation
- Position and HP are server-authoritative
- Older states should be discarded if a newer state already exists

### CombatEvent

Sent when authoritative combat logic produces a result.

Suggested fields:

- `tick`
- `eventType`
- `attackerId`
- `targetId`
- `damage`
- optional `hitPosition`

Typical event types:

- `Hit`
- `DamageApplied`
- `Death`
- `ShootRejected`

Notes:

- Reliable ordered delivery is required
- Clients update HP, death state, hit reactions, and combat UI from this message class

## Authority Rules

### Client Authoritative

Only for temporary presentation:

- Local movement prediction
- Local fire animation / local FX preplay

These are visual conveniences only and must be correctable.

### Server Authoritative

Always authoritative for:

- Final position
- HP
- Combat resolution
- Shoot validation
- Hit validation
- Death state

Clients must never be allowed to finalize these outcomes.

## Client Handling Rules

### Local Player

- Send `MoveInput` continuously while movement changes
- Apply local predicted movement immediately
- Send `ShootInput` when firing
- Optionally play local fire effect immediately
- When `PlayerState` arrives:
  - compare authoritative position against predicted position
  - if error is small, smooth correct
  - if error is large, snap or fast-correct
- When `CombatEvent` arrives:
  - update HP and combat result using server truth

### Remote Players

- Do not predict their gameplay logic
- Buffer recent `PlayerState` snapshots
- Interpolate between snapshots for smooth rendering
- Apply `CombatEvent` immediately

## Transport Mapping

This repository already has the right high-level direction:

- High-frequency sync lane for movement/state-like messages
- Reliable lane for guaranteed gameplay events

Recommended mapping:

- `MoveInput` -> sync lane
- `ShootInput` -> reliable lane
- `PlayerState` -> sync lane
- `CombatEvent` -> reliable lane

Important:

- Do not keep both movement and shooting inside the same `PlayerInput` message if they need different delivery policies
- Split them into distinct message types so delivery policy stays explicit

## Tick and Ordering

Use `tick` on all gameplay-relevant messages.

Purposes:

- detect stale sync messages
- support reconciliation
- align state snapshots to server simulation
- simplify debugging

Recommended rules:

- `MoveInput` and `PlayerState` may drop stale packets
- `ShootInput` and `CombatEvent` should remain ordered and reliable

## Recommended MVP Scope

### Phase 1

- Implement `MoveInput`
- Implement authoritative `PlayerState`
- Run local prediction for self
- Run interpolation for remote players

### Phase 2

- Implement `ShootInput`
- Implement authoritative `CombatEvent`
- Server resolves hit and damage
- Clients update HP and hit feedback from server results

### Phase 3

- Add optional projectile authority model if needed
- Add more combat event types
- Add anti-cheat diagnostics or state hash logging only as auxiliary tooling

## Non-Goals For MVP

Do not include these in the first version:

- Client-side authoritative combat
- Client majority voting
- Client hash majority recovery
- Full deterministic lockstep
- Complex rollback netcode
- Advanced anti-cheat enforcement

## Implementation Notes For This Repository

Based on the current network architecture:

- Current `PlayerInput` is too broad for this MVP if movement and shooting need different reliability
- Prefer adding separate message types for `MoveInput` and `ShootInput`
- Keep `PlayerState` as a high-frequency authoritative snapshot
- Add a new reliable message type for `CombatEvent`

If the current transport setup uses only one underlying transport instance, the application layer can still distinguish message policy logically, but true sync/reliable isolation is better when backed by distinct lanes or transport behavior.

## Summary

For the MVP:

- Client sends only `MoveInput` and `ShootInput`
- Server owns all gameplay truth
- Server sends `PlayerState` and `CombatEvent`
- Client predicts local movement
- Client interpolates remote movement
- Client corrects to server state when divergence appears

This gives a practical, controllable, and extensible baseline for a small MOBA-style networking model.
