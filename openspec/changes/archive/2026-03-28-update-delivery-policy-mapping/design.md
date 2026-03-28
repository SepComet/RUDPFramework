## Context

`TODO.md` step 2 focuses narrowly on how the shared runtime chooses a delivery lane after the gameplay protocol split. The current code already centralizes that decision in `DefaultMessageDeliveryPolicyResolver`, which is consulted by `MessageManager` before selecting either the reliable transport or the optional sync transport.

This change does not introduce a new transport abstraction or alter the envelope contract. Its purpose is to lock down the default mapping contract so later MVP work on stale filtering, prediction, and dual-transport wiring can assume a stable policy table: latest-wins movement/state traffic uses the sync lane, while shooting and combat-result traffic continue to use reliable ordered delivery.

## Goals / Non-Goals

**Goals:**
- Define the default message-type to delivery-policy mapping used by the shared runtime.
- Keep the mapping small and explicit so `MoveInput` and `PlayerState` are the only gameplay messages promoted to `HighFrequencySync`.
- Preserve reliable ordered fallback for `ShootInput`, `CombatEvent`, and existing control-plane messages.
- Require regression tests that exercise `MessageManager` lane selection through the resolver contract.

**Non-Goals:**
- Wiring two concrete transport instances through all integration entry points.
- Changing stale-sequence filtering, prediction replay, or protobuf field definitions beyond what this mapping step depends on.
- Replacing the resolver with a configurable registry or runtime policy editor.

## Decisions

### Keep the default mapping in a static resolver table
The default runtime contract will remain a small static mapping owned by `DefaultMessageDeliveryPolicyResolver`. `MoveInput` and `PlayerState` are explicitly listed as `HighFrequencySync`, and unresolved message types fall back to `ReliableOrdered`.

Alternative considered: list every message type explicitly in the resolver.
Rejected because the TODO step only needs a narrow exception set. A reliable fallback keeps control traffic and future message types safe unless they are intentionally promoted to the sync lane.

### Make lane selection observable through `MessageManager` regression tests
The design relies on send-path tests rather than resolver-only unit tests. `MessageManager` is the behavior boundary that chooses which transport instance sends the envelope, so routing tests verify the mapping contract and the integration between resolver and transport selection at the same time.

Alternative considered: test only `DefaultMessageDeliveryPolicyResolver.Resolve`.
Rejected because that would not prove the runtime actually routes through the expected transport lane.

### Treat reliable ordered delivery as the default for discrete gameplay events
`ShootInput` and `CombatEvent` remain on the reliable ordered path by omission from the sync mapping table. This avoids expanding latest-wins semantics to discrete gameplay events where silent dropping or unordered handling would be incorrect.

Alternative considered: map all gameplay messages to the sync lane after the protocol split.
Rejected because delivery semantics differ by message intent, and event-style gameplay traffic must preserve reliable ordered behavior.

## Risks / Trade-offs

- [A default fallback can hide newly added message types on the reliable lane] -> Mitigation: keep the spec explicit that only `MoveInput` and `PlayerState` use `HighFrequencySync`, and add routing tests for every split MVP gameplay message.
- [Future transport wiring could drift from the resolver contract] -> Mitigation: keep `MessageManager` tests asserting which transport instance is selected for sync versus reliable policies.
- [This change can look redundant because the current code already implements it] -> Mitigation: use the change to align TODO step 2, specs, and regression coverage so later work has a stable contract to build on.

## Migration Plan

1. Update the `network-sync-strategy` delta spec to define the default resolver mapping for split gameplay messages.
2. Verify `DefaultMessageDeliveryPolicyResolver` maps `MoveInput` and `PlayerState` to `HighFrequencySync` and leaves `ShootInput`/`CombatEvent` on the reliable ordered fallback.
3. Keep or add `MessageManager` routing tests that prove sync-lane and reliable-lane selection for the split MVP gameplay messages.
4. Use this locked mapping as the baseline for later dual-transport integration work.

## Open Questions

- None for this planning slice. The TODO step already defines the target mapping and the current runtime shape is sufficient to implement it.
