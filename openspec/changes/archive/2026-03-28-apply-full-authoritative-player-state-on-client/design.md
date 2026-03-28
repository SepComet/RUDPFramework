## Context

The current client receives authoritative `PlayerState` packets through `NetworkManager`, routes them via `MasterManager`, and lets `MovementComponent` consume them. That path only applies position meaningfully today: the controlled player uses `PlayerState.Tick` for reconciliation, while remote players lerp toward the latest position. Rotation, HP, and optional velocity are present in the wire contract but do not have one explicit client-side owner, so TODO step 3 cannot be completed without clarifying where authoritative state lives and how presentation reads it.

This change sits between two already-decided networking layers. The wire contract already defines `PlayerState.position`, `rotation`, `hp`, and optional `velocity`, and the sync strategy already treats `PlayerState` as authoritative latest-wins sync traffic. The next TODO step will add buffered interpolation for remote players, so this design must avoid baking interpolation policy into the same change.

## Goals / Non-Goals

**Goals:**
- Define one explicit client-side ownership point for authoritative `PlayerState` per player.
- Ensure both local and remote players can read authoritative position, rotation, HP, and optional velocity from that owned state.
- Keep local reconciliation keyed by authoritative `PlayerState.Tick` while applying the full authoritative payload, not only position.
- Make authoritative HP/state changes visible in existing MVP diagnostics or player UI.

**Non-Goals:**
- Add remote snapshot interpolation buffers or interpolation delay tuning from TODO step 4.
- Introduce client-side authoritative combat resolution or speculative HP changes.
- Redesign the networking receive path beyond the minimum ownership and presentation boundaries needed for authoritative state application.

## Decisions

### Decision: Store authoritative runtime state in a dedicated client-side snapshot model per player
`Player` will own a small runtime model representing the latest accepted authoritative state for that player, instead of leaving `MovementComponent` as the only place that has partial knowledge of the last server packet. This keeps HP, rotation, velocity, and position under one player-scoped owner that both movement/presentation code and UI can query.

Alternative considered:
- Keep authoritative data only inside `MovementComponent`. Rejected because HP and other non-movement fields would still be awkwardly owned by a movement-focused component, and UI/diagnostic code would need to reach into movement internals for non-movement truth.

### Decision: Preserve local reconciliation in `MovementComponent`, but reconcile from the owned authoritative snapshot
The controlled player still needs prediction replay and authoritative-tick pruning, so `MovementComponent` remains responsible for local reconciliation. The difference is that it will reconcile using the latest accepted authoritative snapshot rather than treating `PlayerState` as a transient position correction packet.

Alternative considered:
- Move all reconciliation into `Player`. Rejected because it would either duplicate prediction-buffer logic or make `Player` absorb low-level Rigidbody concerns that already live in `MovementComponent`.

### Decision: Remote players apply full authoritative fields immediately without adding a buffer in this change
Remote players will consume authoritative position, rotation, HP, and velocity from the owned snapshot, but this step will not introduce snapshot buffering. If simple smoothing remains, it must still respect the latest accepted authoritative snapshot and stale-drop rules without creating a second source of truth.

Alternative considered:
- Add remote snapshot buffering now. Rejected because it overlaps directly with TODO step 4 and would blur the acceptance criteria for this change.

### Decision: Surface authoritative HP/state changes through existing lightweight diagnostics
Development visibility should come from the current MVP UI layer, such as `MainUI` or `PlayerUI`, instead of a larger debugging framework. The chosen output only needs to make authoritative HP and other key state changes observable during playtests.

Alternative considered:
- Defer all diagnostics until combat events land. Rejected because TODO step 3 explicitly requires observability of authoritative state before combat-result handling is added.

## Risks / Trade-offs

- [Player and movement responsibilities drift together again] → Mitigation: keep `Player` as the owner of authoritative snapshots and keep `MovementComponent` focused on applying movement/presentation behavior.
- [Remote players still look rough before interpolation work lands] → Mitigation: document that this step applies full authority but intentionally leaves higher-quality smoothing to TODO step 4.
- [UI begins depending on speculative state by accident] → Mitigation: wire HP/state text from the authoritative snapshot only, not from locally predicted movement or combat code.
- [Optional velocity field is absent in some packets] → Mitigation: treat missing velocity as a valid zero/unknown state and avoid making presentation correctness depend on it being present.
