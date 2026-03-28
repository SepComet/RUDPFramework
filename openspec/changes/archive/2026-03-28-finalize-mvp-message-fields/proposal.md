## Why

The MVP already split gameplay messages into separate message types, but TODO step 6 still needs the field-level contract to be locked down explicitly. We need to formalize the exact protobuf payload shape now so later gameplay and integration work does not drift into ad hoc payload extensions or ambiguous authoritative state semantics.

## What Changes

- Specify the required MVP fields for `MoveInput`, `ShootInput`, `PlayerState`, and `CombatEvent` in the shared gameplay message contract.
- Require the protobuf schema and generated C# messages to continue exposing `CombatEventType` and the explicit gameplay fields needed for movement, shooting, authoritative state, and combat results.
- Add implementation tasks to verify the checked-in schema and generated output match the MVP field contract and to add regression coverage if field-level tests are missing.

## Capabilities

### New Capabilities

### Modified Capabilities
- `network-gameplay-message-types`: Extend the gameplay message-type requirement from message identity only to explicit MVP field definitions for movement input, shooting input, authoritative player state, and combat events.

## Impact

Affected files are expected in `Assets/Scripts/Network/Defines/message.proto`, generated `Assets/Scripts/Network/Defines/Message.cs`, and edit-mode regression tests that validate message field availability or serialization shape. This change should preserve the existing split-message design and avoid broad protocol redesign.
