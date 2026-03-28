# shared-network-foundation Specification

## Purpose
Define the shared transport, session-lifecycle, and message-routing foundation that both client and server hosts use without depending on Unity-specific runtime host classes.

## Requirements
### Requirement: Shared network core is host-agnostic
The project SHALL provide a shared network core that contains transport, session, envelope parsing, and message-routing behavior without depending on Unity-specific runtime host classes such as `MonoBehaviour` or frame-loop callbacks. Both client and server networking hosts MUST be able to use this shared core.

#### Scenario: Client host uses shared network core
- **WHEN** the Unity client constructs its runtime networking stack
- **THEN** it uses the shared transport and message-routing core for transport startup, sending, receiving, and handler registration
- **THEN** Unity-specific logic remains in the client host adapter rather than in the shared core classes

#### Scenario: Server host can use the same core without Unity types
- **WHEN** a server-side host constructs the runtime networking stack
- **THEN** it can use the same shared transport and message-routing core without depending on Unity host classes
- **THEN** server-specific startup and lifetime control are provided by a separate host adapter

### Requirement: Message routing uses a host-provided dispatcher strategy
The shared message-routing layer SHALL execute received business handlers through a host-provided dispatcher abstraction rather than constructing a Unity-specific dispatcher internally. The host MUST be able to choose the dispatch strategy that matches its runtime model.

#### Scenario: Unity client injects a queued main-thread dispatcher
- **WHEN** the Unity client constructs the shared message-routing layer
- **THEN** it supplies a dispatcher implementation that queues work for later execution on the Unity main thread
- **THEN** received handlers run according to that injected client dispatch strategy

#### Scenario: Server host injects a non-Unity dispatch strategy
- **WHEN** a non-Unity server host constructs the shared message-routing layer
- **THEN** it supplies a dispatcher implementation that does not rely on Unity frame-loop semantics
- **THEN** the shared message-routing layer still processes received messages correctly through that host-selected strategy

### Requirement: Shared core preserves current transport and message contracts
The shared client/server foundation SHALL preserve the envelope-based business-message contract across client and server hosts while allowing delivery-policy selection behind the shared message-routing layer. Reliable control traffic MUST continue to use the existing `ITransport` contract, and high-frequency sync traffic MUST remain composable by supplying a second host-agnostic sync transport instance to `SharedNetworkRuntime` or `ServerNetworkHost` rather than by expanding `ITransport` for MVP-specific lane semantics. The shared message-type contract MUST allow hosts to distinguish `MoveInput`, `ShootInput`, `CombatEvent`, and `PlayerState` as separate business messages across both delivery lanes.

#### Scenario: Shared runtime starts distinct reliable and sync transports
- **WHEN** a client host constructs `SharedNetworkRuntime` with one reliable transport and a different sync transport instance
- **THEN** starting the runtime starts both transport instances
- **THEN** stopping the runtime stops both transport instances while keeping the same shared message-routing contract

#### Scenario: Server host composes both transport lanes without protocol forks
- **WHEN** a server host constructs `ServerNetworkHost` with one reliable transport and a different sync transport instance
- **THEN** it observes inbound activity from both transport lanes through shared host logic
- **THEN** it routes messages with the same envelope and message-type contract instead of defining a lane-specific protocol fork

#### Scenario: Shared hosts exchange the same envelope format across delivery lanes
- **WHEN** a client host sends a business message through either the reliable control path or the high-frequency sync path
- **THEN** the payload is encoded with the same shared envelope and message-type contract
- **THEN** the server host decodes and routes it through shared networking logic without a host-specific protocol fork

#### Scenario: Hosts preserve dual-transport composition outside ITransport
- **WHEN** a host needs separate reliable and sync lanes for MVP gameplay traffic
- **THEN** it provides separate transport instances plus delivery-policy configuration to shared runtime or host entry points
- **THEN** the shared networking core does not require `ITransport` itself to grow MVP-specific multi-lane APIs

#### Scenario: Shared hosts route split gameplay message identities consistently
- **WHEN** client or server code sends `MoveInput`, `ShootInput`, `CombatEvent`, or `PlayerState`
- **THEN** the envelope carries a distinct message-type value for that business message
- **THEN** shared routing code can resolve handlers and delivery policy without decoding a mixed `PlayerInput` intent

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