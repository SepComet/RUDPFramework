## ADDED Requirements

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