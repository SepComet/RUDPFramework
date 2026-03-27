## ADDED Requirements

### Requirement: Transport metrics module is transport-agnostic and host-agnostic
The project SHALL provide a transport metrics module contract and snapshot model that do not depend on Unity runtime types or a specific `ITransport` implementation. Transport implementations MUST be able to publish lifecycle, traffic, session, and error events through that contract without exposing Unity-specific dependencies.

#### Scenario: KCP transport can publish into a shared metrics contract
- **WHEN** `KcpTransport` is constructed with a metrics module implementation
- **THEN** it can report start, shutdown, payload, datagram, session, and error events through that contract
- **THEN** the metrics module remains reusable outside Unity-specific hosts

### Requirement: Metrics summaries include global and per-peer transport statistics
The metrics module SHALL aggregate one run summary from transport start to transport stop, including global totals and per-peer totals keyed by remote endpoint. The summary MUST include at least payload counts and bytes, datagram counts and bytes, session lifecycle totals, and error counts.

#### Scenario: Multi-session traffic is preserved per remote endpoint
- **WHEN** a server transport communicates with multiple remote endpoints during one run
- **THEN** the final summary contains transport totals for the whole run
- **THEN** it also contains separate per-peer summaries so one endpoint's traffic and errors do not overwrite another's

### Requirement: Metrics module can finalize and export one run summary
The metrics module SHALL support end-of-run finalization that produces one durable summary per run and MUST make repeated finalization idempotent. The default reporting path MUST write a JSON report and emit a compact console summary when finalization occurs.

#### Scenario: Transport stop exports a single final summary
- **WHEN** a transport run reaches shutdown and triggers metrics finalization
- **THEN** one JSON summary is written for that run and one compact console summary is printed
- **THEN** a repeated shutdown call does not create a duplicate report for the same run