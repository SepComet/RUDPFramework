# client-gameplay-input Specification

## MODIFIED Requirements

### Requirement: Controlled client movement input preserves immediate prediction and explicit stop signaling

The MVP client SHALL capture movement intent for the controlled player in Unity-side input code, apply local movement prediction immediately, and send `MoveInput` updates through the networking boundary. When movement input transitions from non-zero to idle, the client MUST send one final zero-vector `MoveInput` so authoritative movement can stop cleanly. When the client reconciles against authoritative state and replays pending `MoveInput` messages, the replay path MUST apply each pending input in fixed-duration substeps matching the server authoritative movement cadence, so that replay trajectory matches live prediction trajectory for the same input sequence.

#### Scenario: Controlled player moves locally without waiting for the network
- **WHEN** the controlled player provides non-zero movement input
- **THEN** the client applies local movement prediction immediately for presentation
- **THEN** the client submits a `MoveInput` carrying the current player id, tick, and planar movement vector through the networking send path

#### Scenario: Releasing movement emits an explicit stop update
- **WHEN** the controlled player releases movement input after previously providing non-zero movement
- **THEN** the client sends exactly one final `MoveInput` whose movement vector is zero
- **THEN** local predicted movement also stops immediately without waiting for authoritative correction

#### Scenario: Replay uses fixed-step substeps matching server cadence
- **WHEN** the client accepts an authoritative `PlayerState` and replays pending `MoveInput` messages
- **THEN** each `PredictedMoveStep` is consumed by applying its input in fixed-duration substeps equal to the server authoritative movement cadence
- **THEN** the replay accumulation shape is identical to the live FixedUpdate prediction path for the same input values
- **THEN** non-linear trajectories (e.g. simultaneous turn-and-move) produce the same result in both replay and live prediction
