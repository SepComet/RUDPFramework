# network-main-thread-dispatch Specification

## Purpose
Define the main-thread dispatch boundary between background transport callbacks and Unity runtime message handling.

## Requirements
### Requirement: Transport callbacks enqueue network message dispatch work
The network application layer SHALL place a thread-safe queue between `ITransport.OnReceive` callbacks and message handler execution. When a transport callback produces a valid application envelope, the callback path MUST enqueue dispatch work and return without invoking registered business handlers inline.

#### Scenario: Valid payload is deferred instead of dispatched inline
- **WHEN** a transport implementation raises `OnReceive` with a valid encoded application message
- **THEN** the message layer enqueues one dispatch work item for that message
- **THEN** the registered handler is not executed during the transport callback itself

#### Scenario: Invalid payload does not block later queued messages
- **WHEN** the transport callback receives malformed bytes followed by a valid application message
- **THEN** the malformed payload is handled as an error without enqueuing executable work
- **THEN** the later valid message can still be enqueued and processed normally

### Requirement: Main-thread drain executes queued handlers in receive order
The runtime SHALL provide an explicit main-thread drain step that executes queued network dispatch work in FIFO order. Message handlers, gameplay state mutation, and UI-facing reactions triggered by received messages MUST run only through this main-thread drain path.

#### Scenario: Drain executes queued work on demand
- **WHEN** one or more network messages have been enqueued from transport callbacks
- **THEN** no registered handler runs until the main-thread dispatcher performs a drain step
- **THEN** each queued handler executes during that drain step on the Unity main thread path

#### Scenario: Messages preserve receive order through the dispatcher
- **WHEN** multiple valid messages are enqueued in sequence for the same runtime
- **THEN** the main-thread dispatcher invokes their handlers in the same order they were enqueued

### Requirement: Runtime network host pumps the dispatcher each frame
The Unity-side runtime network host SHALL integrate a client-specific main-thread dispatcher implementation into its frame loop so queued network work is drained regularly while the network stack is running. That Unity dispatcher implementation MUST satisfy the shared host-dispatch abstraction used by the shared message-routing layer, while the transport background thread responsibilities remain limited to socket receive, KCP input/update, and transport-level error handling.

#### Scenario: Network host drains queued messages during runtime
- **WHEN** the Unity client runtime has started networking and a message is queued from the transport layer
- **THEN** the Unity client host performs dispatcher draining during its Unity update loop
- **THEN** the queued handler runs without the transport layer directly touching Unity objects

#### Scenario: Transport layer remains free of Unity object mutation
- **WHEN** developers inspect the responsibilities of the transport receive path after the shared networking refactor
- **THEN** they find socket receive, KCP processing, and enqueue/error handling only
- **THEN** Unity object mutation and UI updates are performed through the Unity host's main-thread dispatcher implementation rather than the transport callback path
