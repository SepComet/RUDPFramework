## 1. Metrics module

- [x] 1.1 Add transport metrics contracts, snapshot models, and default JSON-plus-console reporting implementation in shared networking code.
- [x] 1.2 Ensure the metrics module aggregates one run of global and per-peer counters and finalizes idempotently.

## 2. KCP transport integration

- [x] 2.1 Extend `KcpTransport` with optional metrics-module injection and current-snapshot access without changing `ITransport`.
- [x] 2.2 Publish start, shutdown, session, payload, datagram, and error events from `KcpTransport` into the metrics module and finalize reports on `Stop()`.

## 3. Verification

- [x] 3.1 Add edit-mode tests for metrics aggregation, peer isolation, and single-report shutdown behavior.
- [x] 3.2 Run the relevant network edit-mode test suite and confirm the new metrics behavior passes.