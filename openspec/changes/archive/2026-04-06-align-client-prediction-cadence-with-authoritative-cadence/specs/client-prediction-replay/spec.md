# client-prediction-replay Specification

## Purpose

Define the contract that client-side replay of pending movement inputs after authoritative state acknowledgement uses fixed-step substeps matching the server authoritative movement cadence, not a single accumulated duration, so that replay trajectory matches live prediction trajectory for the same input sequence.

## MODIFIED Requirements

### Requirement: Replay uses fixed-step accumulation matching server cadence

The controlled-client prediction replay path SHALL consume each pending `PredictedMoveStep` by applying its input in fixed-duration substeps equal to the server authoritative movement cadence, regardless of the step's total `SimulatedDurationSeconds`. **Forward prediction accumulation SHALL also use the same server authoritative movement cadence as the unit of accumulation, ensuring forward accumulated duration and replay duration are derived from the same cadence constant.** The replay accumulation shape MUST be identical to the live `FixedUpdate` prediction path for the same input values.

#### Scenario: Replay produces same trajectory as live prediction for steady input
- **WHEN** the client replays a `PredictedMoveStep` with turn=0, throttle=1, duration=0.15s using a 0.05s server cadence
- **THEN** the replay applies 0.05s + 0.05s + 0.05s substeps in sequence
- **THEN** the final predicted position matches the position that would result from three consecutive FixedUpdate predictions of 0.05s each with the same input

#### Scenario: Replay produces same trajectory as live prediction for turn-and-move input
- **WHEN** the client replays a `PredictedMoveStep` with turn=0.5, throttle=1, duration=0.10s using a 0.05s server cadence
- **THEN** the replay applies two 0.05s substeps where each substep's heading affects the next substep's forward direction
- **THEN** the final predicted heading and position match the live prediction path for the same input sequence

#### Scenario: Replay handles non-multiples of cadence interval
- **WHEN** the client replays a `PredictedMoveStep` with duration=0.12s using a 0.05s cadence
- **THEN** the replay applies 0.05s + 0.05s + 0.02s substeps sequentially
- **THEN** no remaining duration is lost or double-counted

### Requirement: Replay trajectory determinism is verifiable

The client prediction system SHALL provide a deterministic way to verify that replay and live prediction produce identical trajectories for a given input sequence, enabling regression coverage.

#### Scenario: Replay and live prediction produce identical results
- **WHEN** a controlled client records a `MoveInput` sequence during live play
- **AND** the client triggers reconciliation and replays those same inputs
- **THEN** the final predicted pose after replay equals the predicted pose that would result from live FixedUpdate simulation for the same input sequence
- **THEN** the result is stable across multiple replays of the same input sequence
