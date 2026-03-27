# kcp-transport Specification

## Purpose
TBD - created by archiving change introduce-kcp-transport. Update Purpose after archive.
## Requirements
### Requirement: Client mode uses a default KCP session
The transport SHALL support a client mode that is constructed with a default remote endpoint and creates exactly one default KCP session after `StartAsync`. Calls to `Send(byte[] data)` in client mode MUST encode the payload through that session and emit the resulting UDP datagrams to the configured remote endpoint.

#### Scenario: Client sends through the default remote session
- **WHEN** the application starts a client-mode transport and calls `Send` with a business payload
- **THEN** the transport routes the payload through the default KCP session
- **THEN** the UDP socket sends the encoded datagrams to the configured remote endpoint

### Requirement: Server mode isolates KCP session state per remote endpoint
The transport SHALL support a server mode that receives UDP datagrams from multiple remote endpoints and maintains independent KCP session state for each active remote endpoint. A datagram received from one endpoint MUST only be applied to that endpoint's session, and `SendToAll(byte[] data)` MUST encode and enqueue the payload once per active session.

#### Scenario: Two remotes do not share KCP state
- **WHEN** a server-mode transport receives KCP traffic from two different remote endpoints
- **THEN** the transport creates or reuses separate KCP session state for each endpoint
- **THEN** payloads reconstructed for one endpoint are delivered with that endpoint as the sender

#### Scenario: Broadcast writes through each active session
- **WHEN** the application calls `SendToAll` while the server transport has multiple active sessions
- **THEN** the transport encodes the payload for each active KCP session
- **THEN** the UDP socket sends datagrams to every active remote endpoint without collapsing them into a shared session

### Requirement: OnReceive only dispatches complete KCP payloads
The transport SHALL invoke `OnReceive` only after a complete application payload has been reconstructed from `Kcp.Recv`. Raw UDP packets, partial KCP fragments, and transport-level acknowledgements MUST NOT be surfaced to the message layer.

#### Scenario: Fragmented payload is withheld until complete
- **WHEN** a business message spans multiple UDP datagrams and only a subset of those datagrams has been processed
- **THEN** the transport does not invoke `OnReceive`

#### Scenario: Reassembled payload is forwarded to the message layer
- **WHEN** the remaining datagrams for a fragmented KCP message are processed and `Kcp.Recv` yields a complete payload
- **THEN** the transport invokes `OnReceive` exactly once with the reconstructed payload and the originating remote endpoint

### Requirement: Active sessions are driven until stop and cleaned up on shutdown
The transport SHALL continue driving KCP timers for every active session while it is running, so retransmissions, acknowledgements, and flushes can progress even when no new UDP datagrams arrive. Calling `Stop()` MUST stop the receive and update loops, release the UDP socket, and clear active session state.

#### Scenario: Idle sessions still receive KCP timer updates
- **WHEN** the transport has an active session with pending KCP work but no new incoming UDP datagrams
- **THEN** the transport continues calling the KCP update path according to its internal schedule

#### Scenario: Stop releases transport resources
- **WHEN** the application calls `Stop()` on a running transport
- **THEN** the transport stops receiving new UDP datagrams
- **THEN** the transport clears its active KCP session state before shutdown completes
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
### Requirement: KCP transport can emit structured metrics through an optional module
`KcpTransport` SHALL allow callers to provide an optional transport metrics module without changing the shared `ITransport` contract. While running, `KcpTransport` MUST publish transport lifecycle, session creation and disposal, logical payload traffic, UDP datagram traffic, and transport-stage errors into that module, and it MUST expose a current metrics snapshot query for diagnostics and tests.

#### Scenario: Injected metrics module receives KCP traffic statistics
- **WHEN** a caller starts a `KcpTransport`, sends and receives payloads, and then stops the transport
- **THEN** the injected metrics module receives enough events to aggregate the run's payload, datagram, session, and error statistics
- **THEN** diagnostics code can query the current snapshot without reading the exported report file

#### Scenario: Default metrics module exports on KCP transport shutdown
- **WHEN** a caller uses `KcpTransport` without providing a custom metrics module and later calls `Stop()`
- **THEN** the transport uses its built-in metrics module to finalize the run summary during shutdown
- **THEN** the transport emits the final JSON report and compact console summary exactly once for that run