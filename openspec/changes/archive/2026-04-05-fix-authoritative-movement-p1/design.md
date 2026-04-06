## Context

P0 separated snapshot tick from acknowledged movement-input tick and moved steady-state client prediction onto server-confirmed movement parameters. The remaining visible jitter comes from two implementation gaps: the server authoritative movement path still accepts arbitrary elapsed values from its outer loop, and the controlled client still rewrites rigidbody state immediately on every accepted authoritative snapshot. Those behaviors are individually acceptable for correctness, but together they amplify small cadence drift into visible pull-back.

## Goals / Non-Goals

**Goals:**
- Make authoritative movement cadence an explicit shared runtime contract instead of an accidental property of whichever loop calls into server movement.
- Keep server authoritative movement deterministic enough that client prediction can be compared against a known cadence during debugging and regression tests.
- Replace unconditional hard rewrites for small controlled-player divergence with a bounded correction path that preserves server authority while reducing visible jitter.
- Preserve the existing remote-player interpolation path and the P0 snapshot-tick versus acknowledged-move-tick contract.

**Non-Goals:**
- Do not redesign transport lanes, login flow, or session ownership.
- Do not add a new prediction model or speculative physics stack beyond the existing movement inputs and authoritative snapshots.
- Do not change remote-player interpolation into extrapolation.
- Do not remove the ability to hard snap when local state diverges materially from authoritative truth.

## Decisions

### Decision: Introduce a dedicated authoritative movement cadence contract
The server runtime will expose a single configured movement step interval that drives authoritative movement simulation and state emission. The coordinator will consume that fixed cadence rather than arbitrary elapsed values supplied by callers.

Why:
- A fixed cadence makes server movement behavior testable and comparable against client prediction.
- It eliminates one major source of drift where identical movement constants still produce different trajectories because integration step sizes differ.

Alternative considered:
- Keep variable elapsed integration and only document recommended server loop timing. Rejected because the problem is not documentation; it is the lack of an enforceable contract.

### Decision: Surface cadence diagnostics through existing movement/sync plumbing
The runtime will expose enough information for logs, tests, and reconciliation code to know the authoritative movement cadence and the authoritative tick carried by snapshots.

Why:
- Debugging convergence problems requires seeing both the snapshot identity and the cadence under which it was produced.
- Tests need a stable way to assert cadence-aligned behavior without relying on wall-clock timing.

Alternative considered:
- Keep cadence entirely internal to the server runtime. Rejected because that hides the exact value the client needs to compare against when diagnosing jitter.

### Decision: Apply bounded correction for small controlled-player error
The controlled client will classify accepted authoritative corrections into two paths: small error remains server-authoritative but is corrected through bounded convergence over subsequent local updates; large error still snaps immediately.

Why:
- Most visible jitter now comes from repeated tiny rewrites, not giant desync.
- A bounded correction path reduces visual pull-back while preserving the ability to recover quickly from true divergence.

Alternative considered:
- Continue snapping on every accepted state. Rejected because it preserves correctness but also preserves the visible jitter this change is meant to reduce.

### Decision: Keep remote-player presentation rules unchanged
Remote players will continue using buffered interpolation/clamp over accepted authoritative snapshots. This change only modifies the local controlled-player reconciliation path.

Why:
- Remote presentation already has a distinct smoothing strategy with a different trade-off surface.
- Mixing the two concerns would broaden the change without addressing the current problem source.

## Risks / Trade-offs

- [Bounded local correction can hide a real divergence for too long] -> Keep a hard snap threshold and assert it in regression coverage.
- [Fixed authoritative cadence may require updates to server runtime callers] -> Centralize cadence ownership in runtime configuration and keep the public integration seam narrow.
- [Cadence diagnostics may be misread as a new network contract] -> Keep diagnostics descriptive and avoid introducing a second movement truth outside authoritative snapshots.
- [Unity rigidbody timing may still differ from deterministic server stepping] -> Tie correction policy to authoritative cadence and document that the client still converges to server truth instead of matching every substep exactly.

## Migration Plan

1. Introduce the authoritative movement cadence configuration and route server movement stepping through it.
2. Update controlled-player reconciliation to classify small versus large error using the cadence-aware correction policy.
3. Extend edit-mode regression coverage for cadence-aligned convergence and snap fallback.
4. Verify the sample still preserves server-authoritative outcomes and archive the change after tests pass.

## Open Questions

- Whether cadence diagnostics belong in an explicit debug/state object or can remain as properties on existing runtime helpers.
- The exact positional/rotational error thresholds that should trigger hard snap versus bounded correction for the sample scene.
