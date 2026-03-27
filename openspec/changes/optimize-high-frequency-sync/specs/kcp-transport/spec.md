## MODIFIED Requirements

### Requirement: KCP is the sole reliable transport implementation
The project SHALL expose `KcpTransport` as the only reliable `ITransport` implementation used by runtime networking paths. Reliable control-plane business messages, including login, logout, heartbeat, and other ordered session-management traffic, MUST continue to flow through KCP-backed sessions, while high-frequency `PlayerInput` and `PlayerState` synchronization MAY use a separate sync lane defined by the sync-strategy capability.

#### Scenario: Runtime networking uses KCP for reliable control delivery
- **WHEN** the application constructs the reliable transport used for login and session control traffic
- **THEN** that transport instance is `KcpTransport`
- **THEN** reliable control payloads are sent and received through KCP session state

#### Scenario: High-frequency sync is allowed to bypass reliable ordered delivery
- **WHEN** the runtime routes `PlayerInput` or `PlayerState` according to the high-frequency sync strategy
- **THEN** those messages are not forced to use the reliable ordered KCP lane
- **THEN** reliable KCP delivery remains available for control-plane traffic