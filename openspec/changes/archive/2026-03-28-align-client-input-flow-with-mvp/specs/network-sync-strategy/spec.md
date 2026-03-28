## ADDED Requirements

### Requirement: Client gameplay input preserves movement and shooting lane semantics
The client gameplay-input flow SHALL preserve the MVP delivery-lane contract when sending gameplay actions. Explicit zero-vector `MoveInput` updates generated on input release MUST remain valid high-frequency sync traffic, and `ShootInput` generated from local fire intent MUST use the reliable ordered lane.

#### Scenario: Stop movement update remains sync traffic
- **WHEN** the controlled client sends a zero-vector `MoveInput` after releasing movement input
- **THEN** the message is still treated as `MoveInput` for delivery-policy resolution
- **THEN** the networking stack routes it through the high-frequency sync lane when one is configured

#### Scenario: Shoot input remains reliable traffic
- **WHEN** the controlled client sends `ShootInput` from the MVP fire-input path
- **THEN** the networking stack resolves that message to the reliable ordered lane
- **THEN** firing intent does not share the latest-wins sync delivery behavior used for movement updates
