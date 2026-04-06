## MODIFIED Requirements

### Requirement: Gameplay messages expose explicit MVP payload fields
The shared networking contract SHALL define the MVP payload fields for gameplay messages explicitly in the source protobuf schema and generated C# messages. `MoveInput` MUST expose `playerId`, `tick`, `moveX`, and `moveY`; `ShootInput` MUST expose `playerId`, `tick`, `dirX`, `dirY`, and an optional `targetId`; `PlayerState` MUST expose `playerId`, `tick`, `acknowledgedMoveTick`, `position`, `rotation`, `hp`, and an optional `velocity`; `CombatEvent` MUST expose `tick`, `eventType`, `attackerId`, `targetId`, `damage`, and an optional `hitPosition`. The shared contract MUST also provide `CombatEventType` so combat results use explicit event categories rather than ad hoc integer payload conventions.

#### Scenario: Movement input carries explicit movement fields
- **WHEN** client or server code constructs or parses `MoveInput`
- **THEN** the message exposes `playerId`, `tick`, `moveX`, and `moveY`
- **THEN** movement intent does not rely on an overloaded payload extension

#### Scenario: Shooting input carries explicit aim fields
- **WHEN** client or server code constructs or parses `ShootInput`
- **THEN** the message exposes `playerId`, `tick`, `dirX`, `dirY`, and `targetId`
- **THEN** shooting direction and optional target selection are represented directly in the message contract

#### Scenario: Authoritative player state carries explicit gameplay state fields
- **WHEN** client or server code constructs or parses `PlayerState`
- **THEN** the message exposes `playerId`, `tick`, `acknowledgedMoveTick`, `position`, `rotation`, `hp`, and `velocity`
- **THEN** snapshot ordering and acknowledged-input reconciliation are both expressed without ad hoc payload extensions or overloaded tick semantics

#### Scenario: Combat events carry explicit result fields and event categories
- **WHEN** client or server code constructs or parses `CombatEvent`
- **THEN** the message exposes `tick`, `eventType`, `attackerId`, `targetId`, `damage`, and `hitPosition`
- **THEN** `CombatEventType` provides explicit combat-result categories for interpreting that event payload
