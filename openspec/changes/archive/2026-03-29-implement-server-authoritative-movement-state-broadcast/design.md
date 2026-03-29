## Context

`ServerNetworkHost` already owns transport startup, message draining, and `MultiSessionManager`, but it does not yet register gameplay handlers or retain any authoritative movement model per peer. `MessageManager` can already route `MoveInput` and `PlayerState` across reliable/sync lanes, and `SyncSequenceTracker` already accepts stale filtering rules for high-frequency messages, but the server currently lacks a component that turns accepted `MoveInput` into authoritative state and periodic `PlayerState` output.

This change needs to stay inside the shared networking/server code under `Assets/Scripts/Network/` so the authoritative loop remains host-agnostic and testable in edit-mode tests. The client single-session path and existing dual-transport startup contract must remain intact.

## Goals / Non-Goals

**Goals:**
- Add a server-side movement authority component that registers `MoveInput` handling and owns authoritative per-player movement state.
- Keep stale filtering and last-accepted tick tracking independent for each peer so multi-session traffic cannot interfere across connections.
- Broadcast authoritative `PlayerState` snapshots at a fixed cadence on the existing sync lane contract.
- Make zero-vector `MoveInput` stop authoritative movement instead of relying on client-only visuals.
- Keep the runtime entry point easy to test from fake transports and edit-mode regression tests.

**Non-Goals:**
- Implement shooting, combat resolution, or authoritative HP changes beyond preserving fields needed by `PlayerState` broadcasting.
- Introduce Unity-specific frame-loop dependencies into shared networking code.
- Replace the existing client reconciliation/interpolation logic in this change.

## Decisions

### 1. Introduce a dedicated server-authoritative movement coordinator
The server needs a focused component, owned by the server host/runtime, that accepts decoded `MoveInput`, validates it, mutates authoritative state, and produces broadcast snapshots. Extending `ServerNetworkHost` with orchestration hooks is appropriate because it already owns `MessageManager`, transport lifetime, and access to `MultiSessionManager`, but the movement rules themselves should live in a dedicated authority/coordinator type rather than being spread across `ServerRuntimeHandle` and ad-hoc handlers.

Alternative considered: put movement mutation directly inside `MultiSessionManager`. Rejected because `MultiSessionManager` currently owns generic lifecycle state, not gameplay simulation rules or snapshot broadcast cadence.

### 2. Store authoritative movement state per managed peer
The authoritative state must be keyed per remote peer and include the last accepted movement tick, current position, facing/rotation, current velocity, and current movement intent. That state can be attached alongside `ManagedNetworkSession` ownership or maintained in a peer-keyed store owned by the authority coordinator, but the key requirement is that all stale-input evaluation and movement mutation remain peer-scoped.

Alternative considered: a single global last-move tick tracker. Rejected because it would let one peer's late or advanced traffic affect another peer's acceptance window.

### 3. Reuse the existing message routing and sync lane contract for `PlayerState`
The new authority loop should keep using `MessageManager` and the current delivery policy resolver rather than inventing a separate server broadcast channel. Authoritative snapshots remain ordinary `PlayerState` messages, which preserves the existing client reconciliation path and keeps the sync-lane policy centralized.

Alternative considered: special-case `PlayerState` broadcast outside `MessageManager`. Rejected because it would duplicate lane-selection logic and make regression coverage harder.

### 4. Drive authoritative simulation and broadcast with explicit server ticks/cadence hooks
The server runtime already exposes message draining and lifecycle updates through `ServerRuntimeHandle`. This change should add or define a similarly explicit authority update hook so hosts can advance movement resolution and emit snapshots on a known cadence. The cadence source should be injectable/testable so edit-mode tests can deterministically assert broadcast timing and stale-filter behavior.

Alternative considered: only mutate state when input arrives and broadcast immediately. Rejected because clients need regular authoritative `PlayerState` output for reconciliation and interpolation, including periods where input is zero and state is stable.

## Risks / Trade-offs

- [Risk] Additional server-side state per peer increases lifecycle cleanup complexity. → Mitigation: tie authoritative movement state ownership to the same per-peer registration and removal flow used by `MultiSessionManager`.
- [Risk] Cadence-driven broadcasting can spam unchanged snapshots or create unnecessary test brittleness. → Mitigation: keep cadence configuration explicit and default to a small fixed interval that tests can control.
- [Risk] Validation rules may be underspecified for MVP movement. → Mitigation: keep initial validation narrow and deterministic (peer identity, monotonic tick acceptance, finite vector input, zero-vector stop) and leave richer anti-cheat rules for later changes.
- [Risk] Extending shared server code can accidentally affect the client single-session path. → Mitigation: keep all new authority types behind the server host/runtime path and add regression tests that cover both reliable-only and dual-lane server setups.

## Migration Plan

1. Add the new capability/spec coverage for server-authoritative movement and the per-peer multi-session requirement update.
2. Introduce the server authority coordinator and peer movement state model in shared server code.
3. Register `MoveInput` handling through the server host/runtime composition path and expose an explicit authority update/broadcast cadence hook.
4. Add edit-mode regression tests for per-peer stale filtering, zero-vector stop, and sync-lane `PlayerState` broadcasting.
5. Re-run build/test once the .NET runtime environment is available.

## Open Questions

- Whether authoritative position integration should use a simple fixed-speed MVP model or plug into an existing gameplay movement service outside `Assets/Scripts/Network/`.
- Whether the first implementation should broadcast every cadence tick for all managed peers or suppress unchanged snapshots once client reconciliation coverage is confirmed.
