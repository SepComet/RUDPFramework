## Context

`SharedNetworkRuntime` and `ServerNetworkHost` already expose an MVP-friendly constructor shape with one reliable `ITransport` and an optional sync `ITransport`. That behavior is important because the message-routing layer now distinguishes reliable gameplay/control traffic from high-frequency sync traffic, but the current spec only says sync traffic is composable in principle. TODO step 5 is about preserving the current dual-transport runtime boundary before the Unity integration layer is updated in a later step.

The main constraint is to keep the shared networking core host-agnostic. We need explicit runtime and host requirements for dual-lane startup, shutdown, and receive composition without introducing Unity types or widening `ITransport` into a multi-lane abstraction too early.

## Goals / Non-Goals

**Goals:**
- Preserve a shared-runtime contract where client and server hosts can provide distinct reliable and sync transport instances.
- Make startup, shutdown, and inbound handling expectations explicit for both `SharedNetworkRuntime` and `ServerNetworkHost`.
- Keep lane selection in `MessageManager` and delivery-policy abstractions rather than redesigning `ITransport`.
- Drive minimal implementation work, ideally verification plus any missing regression tests.

**Non-Goals:**
- Updating `NetworkManager` or the server bootstrap integration layer to instantiate two transports.
- Redesigning `ITransport`, introducing a transport multiplexer abstraction, or changing the envelope protocol.
- Expanding the sync strategy beyond the message-lane split already captured in `network-sync-strategy`.

## Decisions

### Preserve constructor-based dual-transport composition
The shared runtime contract will continue to accept one primary reliable transport plus an optional sync transport. This matches the existing code, keeps host wiring simple, and avoids forcing every transport implementation to understand multiple delivery lanes.

Alternative considered: extend `ITransport` with multiple send lanes or channel APIs. Rejected for MVP because it would ripple through every transport implementation and blur the boundary between transport concerns and message-routing policy.

### Specify dual-lane lifecycle behavior at the host/runtime level
The spec will explicitly require `SharedNetworkRuntime` and `ServerNetworkHost` to start and stop both transport instances when distinct transports are supplied, and to observe inbound activity from both lanes on the server side. That turns the current code shape into a protected contract instead of an incidental implementation detail.

Alternative considered: leave lifecycle behavior implicit and only test lane selection in `MessageManager`. Rejected because lane selection is not sufficient if host/runtime wiring later collapses back to one started transport.

### Keep message and delivery policy contracts unchanged
The updated requirement will keep the existing shared envelope format and `MessageManager` delivery-policy resolution, with dual-lane composition remaining outside `ITransport`. This isolates TODO step 5 from later integration changes while still making `MoveInput`/`PlayerState` vs. `ShootInput`/`CombatEvent` lane expectations usable at runtime.

Alternative considered: introduce a new capability just for dual transport wiring. Rejected because this is a refinement of the existing shared foundation contract, not a separate product capability.

## Risks / Trade-offs

- [Risk] Shared specs may become too implementation-shaped by naming concrete runtime classes. → Mitigation: keep the requirement centered on host-visible behavior while using `SharedNetworkRuntime` and `ServerNetworkHost` only where the current shared entry points are the contract.
- [Risk] Future transport redesign could invalidate the constructor shape. → Mitigation: scope this explicitly to the MVP and note that broader `ITransport` changes remain out of scope.
- [Risk] Existing tests may not cover startup or shutdown of both lanes. → Mitigation: include regression tasks for dual-transport lifecycle behavior, not only message routing.
