# client-prediction-cadence Specification

## Purpose

Define that client forward prediction accumulation uses an explicit cadence derived from the server authoritative movement cadence, not `Time.fixedDeltaTime`, ensuring prediction timing aligns with authoritative timing in reconciliation-sensitive paths.

## Requirements

### Requirement: Forward prediction accumulation tracks real elapsed time since last authoritative state

The controlled-client forward prediction path SHALL accumulate pending input duration using the actual wall-clock elapsed time since the last authoritative state arrival, not a fixed server cadence increment per FixedUpdate. This ensures `SimulatedDurationSeconds` advances at the same rate as real time and is synchronized with the server's 20Hz authoritative cadence.

#### Scenario: Accumulation uses wall-clock time since last authoritative state
- **WHEN** the client receives an authoritative state at wall-clock time T
- **THEN** the next accumulation period starts from T
- **WHEN** the subsequent FixedUpdate runs
- **THEN** `AccumulateWithElapsedTime` adds only the wall-clock elapsed time since T (not the FixedUpdate interval)
- **THEN** the accumulated `SimulatedDurationSeconds` is proportional to actual elapsed real time

#### Scenario: Accumulation is decoupled from FixedUpdate cadence
- **WHEN** FixedUpdate runs at 50Hz (20ms per step) but the server sends authoritative state at 20Hz (50ms per broadcast)
- **THEN** the accumulation rate is driven by wall-clock time, not by FixedUpdate calls
- **THEN** the pending input duration accumulates to match the real elapsed time between authoritative state arrivals, preventing 2.5x accumulation speedup

### Requirement: Forward prediction and replay use the same cadence source

The controlled-client prediction system SHALL use the same wall-clock time source for both forward accumulation and replay substepping, ensuring that `SimulatedDurationSeconds` consumed during replay matches the wall-clock elapsed time accumulated during forward prediction.

#### Scenario: Forward accumulated duration matches replay substep size
- **WHEN** the client accumulates pending input for 100ms of wall-clock elapsed time
- **THEN** the replay path consumes the same 100ms in 50ms substeps
- **THEN** the forward accumulated duration and replay duration are both derived from the same wall-clock time source
