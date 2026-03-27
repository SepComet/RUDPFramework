## MODIFIED Requirements

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
