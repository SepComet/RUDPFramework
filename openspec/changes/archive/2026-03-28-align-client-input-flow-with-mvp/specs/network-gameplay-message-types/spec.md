## ADDED Requirements

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
