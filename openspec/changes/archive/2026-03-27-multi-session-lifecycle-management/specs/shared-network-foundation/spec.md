## MODIFIED Requirements

### Requirement: Shared runtime owns host-agnostic session lifecycle orchestration
The shared network foundation SHALL include host-agnostic session lifecycle orchestration alongside transport startup and message routing. Client and server hosts MUST be able to compose the shared foundation with session orchestration that consumes transport events, login results, and heartbeat signals without depending on Unity-specific runtime types, while supporting both single-session client composition and multi-session server composition.

#### Scenario: Client host composes runtime with single-session lifecycle manager
- **WHEN** the Unity client constructs its shared networking runtime
- **THEN** that runtime includes shared session lifecycle management for its single remote session in addition to transport and message routing
- **THEN** Unity-specific code remains responsible only for reacting to lifecycle state changes and driving host behavior

#### Scenario: Server host composes shared foundation with multi-session orchestration
- **WHEN** a non-Unity server host constructs the runtime networking stack for multiple remote peers
- **THEN** it uses the shared transport and message-routing foundation together with shared multi-session lifecycle orchestration
- **THEN** server-specific cleanup, admission, and gameplay reactions stay in the server host adapter rather than forking the shared lifecycle contract
