## Context

The client now keeps one authoritative `PlayerState` snapshot per player, and local prediction already reconciles against authoritative ticked state. Remote players, however, still smooth presentation by lerping directly from the current transform toward the latest accepted snapshot, which couples presentation quality to packet spacing and can visibly snap when packets arrive unevenly.

This step only targets remote-player presentation. It must preserve the existing authoritative ownership model, keep stale-packet rejection, and avoid turning remote players into predicted entities. The implementation should stay on the Unity client side and must not change shared networking contracts under `Assets/Scripts/Network/`.

## Goals / Non-Goals

**Goals:**
- Add a small remote-only snapshot buffer that stores accepted authoritative `PlayerState` presentation samples in tick order.
- Render remote players by interpolating between buffered authoritative snapshots instead of lerping straight to the newest packet.
- Reject stale or duplicate remote snapshots before they affect interpolation state.
- Document the interpolation delay/sample strategy clearly enough that later tuning does not require reverse-engineering the code.

**Non-Goals:**
- Do not change local-player prediction or reconciliation behavior.
- Do not add extrapolation, lag compensation, or remote gameplay simulation.
- Do not move authoritative ownership out of `Player` or change message schemas/lane selection.

## Decisions

### Use a dedicated remote snapshot interpolation helper
Remote interpolation state will live in a focused client-side helper, separate from `ClientAuthoritativePlayerState`. `ClientAuthoritativePlayerState` remains the authoritative latest-wins owner, while the new helper manages a short ordered presentation buffer for remote players only.

Why this approach:
- It keeps authoritative state ownership and presentation buffering as distinct concerns.
- It lets local players continue reading the latest accepted snapshot directly for reconcile.
- It avoids pushing Unity presentation logic into shared networking code.

Alternative considered:
- Reusing only the latest authoritative snapshot and lerping harder. Rejected because it does not solve uneven packet spacing or properly use buffered snapshots.

### Buffer a small ordered window and interpolate at a fixed delay
The remote helper will keep a capped ordered list of accepted authoritative snapshots plus their local receive timestamps. Rendering will target a small fixed delay behind real time, initially `0.1s`, which is roughly two MVP movement send intervals (`0.05s`). Each remote `FixedUpdate` will find the two buffered samples that bracket `now - interpolationDelay` and interpolate position/rotation between them.

Why this approach:
- A fixed delay gives the client a high chance of having both an older and newer sample even under modest jitter.
- Receive-time bracketing keeps the MVP implementation simple without introducing a full server-clock interpolation timeline.
- Using two send intervals is small enough for MVP responsiveness while still providing smoothing headroom.

Alternatives considered:
- Tick-only interpolation against estimated server time. Rejected for now because it adds more clock-sync coupling than this TODO step requires.
- Extrapolating past the latest sample. Rejected because remote players must remain non-predicted in this step.

### Hold the latest snapshot when interpolation cannot bracket two samples
If the buffer has only one sample or the render time falls after the newest buffered snapshot, remote presentation will clamp to the latest accepted authoritative snapshot instead of extrapolating. Older samples that are no longer needed for the current interpolation window will be trimmed, and the total buffer size will stay small.

Why this approach:
- It preserves authoritative presentation and avoids introducing speculative remote movement.
- It keeps the buffer bounded and simple to reason about in tests.

Alternative considered:
- Continuing to lerp from the current transform to the newest snapshot as fallback. Rejected because it reintroduces the ad-hoc smoothing path this change is replacing.

## Risks / Trade-offs

- [Interpolation delay adds visible latency to remote presentation] → Keep the initial delay small (`0.1s`) and document it so future tuning is straightforward.
- [Sparse packet arrival may still produce brief holds on the newest snapshot] → Clamp to the latest authoritative snapshot rather than extrapolating incorrect movement.
- [Buffer bookkeeping can drift from authoritative ownership rules] → Keep stale rejection in the authoritative owner and cover the remote buffer behavior with edit-mode regression tests.
- [Rotation smoothing may disagree with velocity direction on sharp turns] → Interpolate authoritative rotation directly from buffered snapshots instead of deriving facing from velocity.
