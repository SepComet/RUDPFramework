## Context

The repository already has split gameplay message types in `message.proto`, and the generated `Message.cs` currently exposes most of the MVP fields listed in TODO step 6. The remaining gap is not the existence of message identities, but the lack of a formal field-level contract that says which gameplay data each message must carry for the MVP.

This matters because later gameplay, prediction, and integration work will depend on these fields being stable. If the contract stays implicit, contributors may treat fields like `hp`, `targetId`, `velocity`, or `hitPosition` as optional implementation details and drift into ad hoc payload extensions.

## Goals / Non-Goals

**Goals:**
- Define the MVP field contract for `MoveInput`, `ShootInput`, `PlayerState`, and `CombatEvent` at the spec level.
- Preserve `CombatEventType` as the explicit enum used by combat-result messages.
- Align the source protobuf schema, generated C# output, and regression tests with the same field contract.
- Keep the change minimal if the repository is already compliant.

**Non-Goals:**
- Redesigning message routing, prediction, or transport policy.
- Introducing a new protocol version or broad schema migration.
- Expanding message payloads beyond the fields listed in TODO step 6.

## Decisions

### Treat field shape as part of the gameplay message capability
The change will extend `network-gameplay-message-types` from message identity only to field-level requirements. This keeps type identity and payload contract in the same capability instead of scattering message semantics across unrelated specs.

Alternative considered: create a separate capability just for message payload schemas. Rejected because these fields are part of what makes the gameplay messages meaningful in the first place.

### Keep protobuf as the source of truth and generated C# as the checked-in reflection of that contract
Implementation tasks will verify `message.proto` first and only update generated `Message.cs` if the protobuf contract changes. This preserves the existing workflow where the schema is authoritative and generated output must match it.

Alternative considered: update only generated C# tests without touching the schema contract. Rejected because field-level requirements must remain anchored in the source protobuf.

### Interpret MVP optional fields using existing protobuf semantics
`targetId`, `velocity`, and `hitPosition` will remain part of the explicit contract without forcing a new transport or protocol redesign. Message-valued fields already support presence in protobuf, and string optionality can stay represented through the existing schema shape unless implementation work reveals a stricter presence requirement.

Alternative considered: require proto3 `optional` field presence immediately for all optional fields. Rejected for now because TODO step 6 only requires explicit MVP fields, not a broader presence-tracking migration.

## Risks / Trade-offs

- [Risk] The repository may already satisfy most of the contract, making the change look documentation-heavy. → Mitigation: include tasks to verify generated output and add regression tests so the spec still protects against future drift.
- [Risk] Optional-field semantics for `targetId` may remain slightly looser than full presence tracking. → Mitigation: capture the MVP requirement as field availability now and defer stricter presence semantics until there is a concrete gameplay need.
- [Risk] Regenerating `Message.cs` can create large diffs if tooling versions change. → Mitigation: only regenerate if the source protobuf actually needs edits.
