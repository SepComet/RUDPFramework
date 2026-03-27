## ADDED Requirements

### Requirement: KCP is the sole reliable transport implementation
The project SHALL expose `KcpTransport` as the only reliable `ITransport` implementation used by runtime networking paths. Reliable business messages, including login, heartbeat, player input, and player state synchronization, MUST continue to flow through KCP-backed sessions rather than any legacy reliable UDP compatibility class.

#### Scenario: Runtime networking uses KCP for reliable delivery
- **WHEN** the application constructs the transport used by `MessageManager` for its normal runtime networking path
- **THEN** that transport instance is `KcpTransport`
- **THEN** reliable business payloads are sent and received through KCP session state

### Requirement: Legacy reliable UDP entry points are retired
The codebase SHALL NOT keep a directly instantiable `ReliableUdpTransport` entry point that implies a second reliable delivery mechanism. If a non-reliable UDP transport is needed in the future, it MUST use a distinct name and MUST NOT claim reliable semantics.

#### Scenario: Legacy reliable transport is not available to callers
- **WHEN** developers inspect the transport implementations available to runtime code
- **THEN** they do not find a usable `ReliableUdpTransport` class representing reliable delivery
- **THEN** the remaining transport naming makes the reliable-versus-unreliable boundary explicit
