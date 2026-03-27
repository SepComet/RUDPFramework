## ADDED Requirements

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
The shared client/server foundation SHALL preserve the existing `ITransport` send/receive contract and the envelope-based `MessageManager` routing model so client and server hosts exchange the same business payload format through the same transport abstractions.

#### Scenario: Shared hosts exchange the same envelope format
- **WHEN** a client host sends a business message through the shared core to a server host using the shared core
- **THEN** the message is encoded using the same envelope contract on the client side
- **THEN** the server host decodes and routes it through the shared message-routing layer without a host-specific protocol fork
