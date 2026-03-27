## Why

The transport layer can already move payloads and manage KCP sessions, but it does not expose structured runtime metrics for weak-network verification or resume-ready project data. We need a transport-agnostic metrics module now so each run can produce a durable summary without introducing Unity dependencies or bloating `ITransport`.

## What Changes

- Add an independent transport metrics module interface and snapshot model that aggregate transport-level statistics without depending on Unity or a concrete transport implementation.
- Wire `KcpTransport` to emit transport lifecycle, session, logical payload, UDP datagram, and error events into the metrics module through that interface.
- Add final-run reporting so `KcpTransport.Stop()` writes one JSON summary per run and prints a compact console summary.
- Expose a runtime snapshot query API on `KcpTransport` for tests and diagnostic tooling without changing `ITransport`.

## Capabilities

### New Capabilities
- `transport-metrics-reporting`: Structured transport metrics aggregation, peer-level summaries, and final report export for a single transport run.

### Modified Capabilities
- `kcp-transport`: KCP transport instances can publish lifecycle, traffic, session, and error statistics to an injected metrics module and emit a final report on shutdown.

## Impact

- Affected code: `Assets/Scripts/Network/NetworkTransport/` transport implementation and new diagnostics module types.
- Affected APIs: `KcpTransport` constructors gain an optional metrics-module dependency and expose a snapshot query method; `ITransport` remains unchanged.
- Affected tests: edit-mode transport tests gain coverage for metrics aggregation, multi-session peer summaries, and single-report shutdown behavior.