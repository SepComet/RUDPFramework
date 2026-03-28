## Context

The repository already has the shared MVP message split in place: `MoveInput` exists for high-frequency movement and `ShootInput` exists for reliable firing intent. The current Unity client path has only implemented part of that contract. `MovementComponent` captures movement every send interval, but it returns `null` for zero input, which means releasing input does not emit an authoritative stop update. The same component also drives immediate local prediction for the controlled player, while `NetworkManager` only exposes `SendMoveInput(...)` and has no shooting send path.

This change is cross-cutting enough to justify a design document because it touches Unity input capture, local prediction timing, message selection, and delivery-lane expectations together. It also needs to preserve the shared/client boundary already established by the networking architecture: Unity input polling stays in Unity-side scripts, while the shared networking layer continues to own message transport policy.

## Goals / Non-Goals

**Goals:**
- Define an MVP-safe client input flow that sends movement and shooting through `MoveInput` and `ShootInput` only.
- Preserve immediate local movement prediction for the controlled player.
- Ensure movement release emits one explicit zero-vector `MoveInput` so the server can stop authoritative motion.
- Add a `NetworkManager.SendShootInput(...)` API that keeps shooting traffic on the reliable lane through existing delivery-policy resolution.
- Keep local shooting feedback optional and cosmetic so authoritative combat remains server-driven.

**Non-Goals:**
- Redesign the shared transport, message envelope, or delivery-policy system.
- Introduce `UnityEngine` dependencies into shared code under `Assets/Scripts/Network/`.
- Define full authoritative combat-result handling or client-side rollback for rejected shots.
- Rework remote-player interpolation or the later authoritative `PlayerState` application steps from the TODO.

## Decisions

### Decision: Keep gameplay input ownership in Unity-side controlled-player components
Movement capture, release detection, and shooting input polling will stay in Unity-facing scripts such as `MovementComponent` or an adjacent controlled-player helper, with `NetworkManager` acting as the send boundary into shared networking code.

This keeps Unity polling (`Input.*`) out of shared networking code and matches the current architecture, where `MovementComponent` already owns local prediction and `NetworkManager` already owns message submission.

Alternative considered: move gameplay input orchestration into shared networking services. Rejected because shared code cannot depend on Unity input APIs and the TODO step only needs client-side MVP wiring, not a new host-agnostic input abstraction.

### Decision: Represent input release as an edge-triggered zero-vector `MoveInput`
The client will continue sampling movement every send interval, but it must distinguish between "still idle" and "just released input." On the transition from non-zero movement to idle, it will send one final `MoveInput` whose vector is zero. Continued idle frames will not keep spamming zero messages unless a later movement input starts and stops again.

This satisfies the authoritative-stop acceptance criteria without bloating sync traffic or changing the meaning of `MoveInput`.

Alternative considered: infer stop on the server from missing movement packets. Rejected because it couples stop timing to packet cadence and conflicts with the TODO requirement for an explicit stop message.

### Decision: Keep local movement prediction immediate and independent from send-path branching
The controlled player should continue applying local movement immediately from the latest captured input, including the zero-vector case on release, while the network send path decides whether that captured input should be transmitted this frame.

This preserves current feel and keeps prediction behavior stable even as the send rules become more explicit.

Alternative considered: simulate only after a message is queued for send. Rejected because it would delay local response and entangle presentation with networking cadence.

### Decision: Add a narrow `SendShootInput(...)` API and leave shot presentation outside authoritative gameplay
`NetworkManager` will gain a dedicated method for sending `ShootInput`, mirroring the current movement send API. The client capture path will construct `ShootInput` from local fire intent and rely on the existing delivery-policy resolver so the message remains on the reliable lane. Any local muzzle flash, animation, or other feedback remains optional and must not decide damage, hit confirmation, or death state.

This keeps the MVP send surface explicit and ensures the client no longer needs legacy broad gameplay messages for firing.

Alternative considered: reuse a generic "send gameplay action" entry point. Rejected because it weakens the split-message contract and leaves room for `PlayerAction`-style payloads to creep back into client code.

## Risks / Trade-offs

- [Risk] Edge-triggered stop detection can regress if input state tracking mixes "captured this frame" with "sent this frame." → Mitigation: define the release rule around the last non-zero captured movement state and cover it with regression tests.
- [Risk] Shooting capture may depend on an existing Unity scene/input setup that is less formalized than movement. → Mitigation: keep the MVP contract narrow, allow optional cosmetic feedback, and leave targeting details minimal when no authoritative target is selected.
- [Risk] Client scripts could accidentally reintroduce legacy gameplay messages while adding fire input. → Mitigation: codify the split-message-only rule in specs and tests around `NetworkManager` send APIs.
- [Risk] Overloading `MovementComponent` with too many responsibilities could make later TODO steps harder to implement. → Mitigation: keep this step focused on input capture/send semantics and defer broader controller refactors unless implementation friction proves they are necessary.

## Migration Plan

1. Update the specs so the MVP client-input contract, message selection rules, and lane expectations are explicit.
2. Implement Unity-side input changes for movement release detection, local prediction continuity, and shooting capture.
3. Add `NetworkManager.SendShootInput(...)` and remove any remaining client gameplay dependence on legacy broad messages.
4. Add regression coverage for stop-message emission, reliable shooting dispatch, and split-message-only gameplay sends.
5. Roll back, if needed, by removing the shooting send entry point and returning to movement-only capture while preserving the existing shared message definitions.

## Open Questions

- Which existing Unity input source should drive the initial `ShootInput` direction in MVP: pointer-derived aim, avatar facing, or a simpler fixed forward fallback?
- Should the first implementation keep shooting capture inside `MovementComponent`, or is a small adjacent controlled-player input script clearer once fire intent is added?
