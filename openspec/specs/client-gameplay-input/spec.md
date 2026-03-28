# client-gameplay-input Specification

## Purpose
Define how the controlled Unity client captures MVP gameplay intent, preserves immediate local prediction, and sends movement and shooting through split gameplay messages.

## Requirements
### Requirement: Controlled client movement input preserves immediate prediction and explicit stop signaling
The MVP client SHALL capture movement intent for the controlled player in Unity-side input code, apply local movement prediction immediately, and send `MoveInput` updates through the networking boundary. When movement input transitions from non-zero to idle, the client MUST send one final zero-vector `MoveInput` so authoritative movement can stop cleanly.

#### Scenario: Controlled player moves locally without waiting for the network
- **WHEN** the controlled player provides non-zero movement input
- **THEN** the client applies local movement prediction immediately for presentation
- **THEN** the client submits a `MoveInput` carrying the current player id, tick, and planar movement vector through the networking send path

#### Scenario: Releasing movement emits an explicit stop update
- **WHEN** the controlled player releases movement input after previously providing non-zero movement
- **THEN** the client sends exactly one final `MoveInput` whose movement vector is zero
- **THEN** local predicted movement also stops immediately without waiting for authoritative correction

### Requirement: Controlled client captures shooting intent as a dedicated gameplay input
The MVP client SHALL capture local fire intent separately from movement and translate that intent into `ShootInput` messages rather than overloading movement or generic gameplay-action payloads.

#### Scenario: Firing produces a shoot input message
- **WHEN** the controlled player triggers a fire action
- **THEN** the client constructs a `ShootInput` containing the current player id, tick, and aim direction used by the MVP client flow
- **THEN** the message is sent through a dedicated shooting send path instead of a legacy generic gameplay-action message

### Requirement: Local shooting presentation remains cosmetic
The MVP client SHALL treat any immediate local shooting feedback as optional cosmetic presentation and MUST NOT use it to finalize authoritative combat outcomes.

#### Scenario: Cosmetic firing feedback does not decide gameplay truth
- **WHEN** the client plays local muzzle flash, animation, or similar fire feedback before server confirmation
- **THEN** that feedback does not apply authoritative damage, hit confirmation, or death resolution locally
- **THEN** gameplay truth remains dependent on authoritative server messages
