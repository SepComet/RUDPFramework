# client-prediction-cadence Specification

## Purpose

Define that client forward prediction accumulation uses an explicit cadence derived from the server authoritative movement cadence, not `Time.fixedDeltaTime`, ensuring prediction timing aligns with authoritative timing in reconciliation-sensitive paths.

## Requirements

### Requirement: Forward prediction accumulation uses authoritative cadence

The controlled-client forward prediction path SHALL accumulate pending input duration using the server authoritative movement cadence as the unit of accumulation, not `Time.fixedDeltaTime` or other render-loop-derived values. This ensures `SimulatedDurationSeconds` reflects server-time and remains coherent with the server's 50ms step cadence.

#### Scenario: Accumulation uses server cadence regardless of FixedUpdate interval
- **WHEN** the client FixedUpdate runs at a 20ms interval
- **THEN** `AccumulateLatest` adds `kServerSimulationStepSeconds` (50ms) to the pending input duration
- **THEN** the accumulated `SimulatedDurationSeconds` reflects server-time, not real elapsed time

#### Scenario: Accumulation cadence is decoupled from frame rate
- **WHEN** FixedUpdate runs at a non-standard interval due to platform variation or frame drops
- **THEN** the accumulation unit remains `kServerSimulationStepSeconds`
- **THEN** prediction timing does not drift relative to the server's authoritative cadence

### Requirement: Forward prediction and replay use the same cadence source

The controlled-client prediction system SHALL use the same cadence source for both forward accumulation and replay substepping, ensuring that `SimulatedDurationSeconds` consumed during replay matches the cadence used during forward prediction.

#### Scenario: Forward accumulated duration matches replay substep size
- **WHEN** the client accumulates pending input for 100ms of server-time
- **THEN** the replay path consumes the same 100ms in 50ms substeps
- **THEN** the forward accumulated duration and replay duration are derived from the same cadence constant
