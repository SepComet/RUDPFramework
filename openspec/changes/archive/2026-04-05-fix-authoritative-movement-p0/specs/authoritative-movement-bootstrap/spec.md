## ADDED Requirements

### Requirement: Client prediction bootstraps from server-confirmed movement parameters
The client SHALL establish controlled-player prediction parameters from server-confirmed authoritative movement settings before treating local prediction as steady-state truth. Client-local candidate values MAY exist before login succeeds, but long-lived prediction MUST switch to the server-confirmed parameters for the controlled player.

#### Scenario: Login success provides authoritative movement parameters for prediction
- **WHEN** the controlled client completes login and receives the server-confirmed movement bootstrap data
- **THEN** the client stores the authoritative movement parameters for that controlled player
- **THEN** subsequent local movement prediction uses those server-confirmed parameters instead of continuing to rely on an unrelated local UI value

### Requirement: Server-owned movement parameters remain the single gameplay authority
The server SHALL keep authoritative ownership of movement tuning used for authoritative movement resolution, and any client-visible movement parameters used for prediction MUST be derived from that server-owned configuration rather than from an independent client-only truth source.

#### Scenario: Client candidate speed does not override server movement authority
- **WHEN** a client proposes or locally configures a movement speed that differs from the server-owned movement speed
- **THEN** the server-owned movement configuration remains authoritative for gameplay resolution
- **THEN** the client's steady-state prediction parameters converge to the server-confirmed value instead of preserving the divergent local candidate
