## Context

`KcpTransport` currently exposes send/receive behavior and session isolation, but transport-level diagnostics are limited to ad-hoc console logging. Weak-network verification, multi-session troubleshooting, and resume-facing statistics all need structured counters and end-of-run summaries that do not depend on Unity and do not force changes onto `ITransport`.

## Goals / Non-Goals

**Goals:**
- Add a transport-agnostic metrics module interface and snapshot model in shared networking code.
- Let `KcpTransport` publish lifecycle, session, payload, datagram, and error events into that module with minimal intrusion.
- Produce one JSON report plus one console summary when a transport run ends at `Stop()`.
- Allow tests and diagnostics code to query the current metrics snapshot without reading the exported file.

**Non-Goals:**
- Add gameplay, UI, or session-state business metrics above the transport layer.
- Change `ITransport` or require all future transports to implement metrics immediately.
- Persist per-event trace logs or high-volume packet histories in v1.
- Add Unity-specific visualization or editor tooling for the exported metrics.

## Decisions

### Use a standalone diagnostics interface instead of nesting metrics types under `KcpTransport`
The metrics contract will live in shared networking code as a transport-agnostic module interface with snapshot DTOs. `KcpTransport` will only hold a private reference and call interface methods at integration points. This keeps the module reusable and avoids making other callers depend on a concrete transport type.

### Keep `ITransport` unchanged and extend only `KcpTransport`
`ITransport` remains the transport contract for runtime networking. `KcpTransport` constructors gain an optional metrics-module parameter and a snapshot query method. This scopes the feature to the only reliable runtime transport without imposing a cross-cutting interface change.

### Treat each `StartAsync` to `Stop()` window as one metrics run
The module resets when the transport starts, accumulates counters for the active run, and finalizes exactly once at shutdown. Repeated `Stop()` calls must be idempotent so the same run does not emit duplicate reports.

### Export global and per-peer summaries, not event streams
The module aggregates totals for payloads, UDP datagrams, sessions, and errors globally and by remote endpoint. This is sufficient for Clumsy validation and multi-session diagnosis while avoiding heavy trace storage and output noise.

### Default reporting is JSON to `Logs/transport-metrics/` plus a compact console summary
The built-in module writes a timestamped JSON file on finalization and prints a one-line summary to the console. JSON preserves data for later scripting, while the console line gives an immediate close-out signal during local runs.

## Risks / Trade-offs

- [Extra synchronization overhead in hot transport paths] -> Mitigation: keep module callbacks coarse-grained, aggregate with counters/snapshots, and avoid per-packet file I/O.
- [Shutdown reporting can fail because of file-system issues] -> Mitigation: make file export best-effort, keep the in-memory snapshot available, and still print a console summary/error.
- [Transport metrics can be misread as business-layer truth] -> Mitigation: keep field names explicitly transport-scoped and exclude gameplay/session outcome claims from the module.
- [Dirty worktree around `KcpTransport` can cause merge pressure] -> Mitigation: constrain edits to additive hooks, new diagnostics types, and focused tests without rewriting existing KCP logic.

## Migration Plan

Add the diagnostics types, wire `KcpTransport`, update transport tests, and keep default behavior backward compatible by making metrics injection optional. No data migration or host adapter changes are required.

## Open Questions

None for v1; output mode and aggregation scope are fixed to JSON plus console and global plus per-peer summaries.