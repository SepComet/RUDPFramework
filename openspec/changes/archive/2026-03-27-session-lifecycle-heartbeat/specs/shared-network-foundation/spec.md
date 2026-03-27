## ADDED Requirements

### Requirement: Shared runtime owns host-agnostic session lifecycle orchestration
The shared network foundation SHALL include host-agnostic session lifecycle orchestration alongside transport startup and message routing. Client and server hosts MUST be able to compose the same shared runtime with a session manager that consumes transport events, login results, and heartbeat signals without depending on Unity-specific runtime types.

#### Scenario: Client host composes runtime with lifecycle manager
- **WHEN** the Unity client constructs its shared networking runtime
- **THEN** that runtime includes shared session lifecycle management in addition to transport and message routing
- **THEN** Unity-specific code remains responsible only for reacting to lifecycle state changes and driving host behavior

#### Scenario: Server host observes the same lifecycle vocabulary
- **WHEN** a non-Unity server host composes the shared networking runtime
- **THEN** it uses the same lifecycle state model and session-manager abstractions as the client-side shared runtime
- **THEN** server-specific cleanup or admission behavior stays in the server host adapter rather than forking the shared core contract
