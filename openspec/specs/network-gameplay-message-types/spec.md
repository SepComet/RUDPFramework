## ADDED Requirements

### Requirement: Gameplay message types are defined independently
The shared networking contract SHALL define `MoveInput`, `ShootInput`, `CombatEvent`, and `PlayerState` as independently addressable business message types rather than overloading one broad gameplay input payload.

#### Scenario: Shared code references split gameplay messages
- **WHEN** shared networking code or tests need to reference movement input, shooting input, authoritative state, or combat results
- **THEN** each concern is represented by its own business message type
- **THEN** code does not need to reinterpret one broad `PlayerInput` payload to determine message intent

### Requirement: Protobuf schema remains the canonical source for generated gameplay messages
The repository SHALL keep the source protobuf schema that defines gameplay network messages under version control, and generated C# message types SHALL be regenerated from that schema when gameplay message definitions change.

#### Scenario: Gameplay message schema changes regenerate shared C# types
- **WHEN** a contributor adds or changes `MoveInput`, `ShootInput`, `CombatEvent`, or `PlayerState` fields in the source protobuf schema
- **THEN** the shared generated `Message.cs` output is regenerated from that schema
- **THEN** the checked-in generated code matches the schema contract used by client and server hosts

### Requirement: Gameplay messages expose explicit MVP payload fields
The shared networking contract SHALL define the MVP payload fields for gameplay messages explicitly in the source protobuf schema and generated C# messages. `MoveInput` MUST expose `playerId`, `tick`, `moveX`, and `moveY`; `ShootInput` MUST expose `playerId`, `tick`, `dirX`, `dirY`, and an optional `targetId`; `PlayerState` MUST expose `playerId`, `tick`, `position`, `rotation`, `hp`, and an optional `velocity`; `CombatEvent` MUST expose `tick`, `eventType`, `attackerId`, `targetId`, `damage`, and an optional `hitPosition`. The shared contract MUST also provide `CombatEventType` so combat results use explicit event categories rather than ad hoc integer payload conventions.

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
- **THEN** the message exposes `playerId`, `tick`, `position`, `rotation`, `hp`, and `velocity`
- **THEN** authoritative movement and health state are expressed without ad hoc payload extensions

#### Scenario: Combat events carry explicit result fields and event categories
- **WHEN** client or server code constructs or parses `CombatEvent`
- **THEN** the message exposes `tick`, `eventType`, `attackerId`, `targetId`, `damage`, and `hitPosition`
- **THEN** `CombatEventType` provides explicit combat-result categories for interpreting that event payload

### Requirement: Client gameplay actions use split gameplay messages directly
The client-facing gameplay send path SHALL express MVP gameplay actions directly as `MoveInput` and `ShootInput`. Controlled-player movement and firing MUST NOT depend on legacy broad gameplay messages such as `PlayerAction`.

#### Scenario: Client movement uses MoveInput directly
- **WHEN** the controlled client sends gameplay movement intent
- **THEN** the send path uses `MoveInput`
- **THEN** the client does not wrap that movement intent in `PlayerAction` or another broad gameplay payload

#### Scenario: Client firing uses ShootInput directly
- **WHEN** the controlled client sends gameplay firing intent
- **THEN** the send path uses `ShootInput`
- **THEN** the client does not wrap that firing intent in `PlayerAction` or another broad gameplay payload
