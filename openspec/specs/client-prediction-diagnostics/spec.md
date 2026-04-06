# client-prediction-diagnostics Specification

## Purpose

Define diagnostics that expose per-snapshot prediction state for regression testing and runtime debugging, enabling verification that replay produces identical trajectories to live prediction and that small server tick offset fluctuations do not cause visible local cadence oscillation.

## Requirements

### Requirement: Authoritative snapshot exposes acknowledged move tick

The client prediction system SHALL expose the acknowledged movement-input tick from the most recently accepted authoritative `PlayerState` snapshot.

#### Scenario: Diagnostics report acknowledged move tick
- **WHEN** the client accepts an authoritative `PlayerState`
- **THEN** diagnostics can read the acknowledged move tick from that snapshot
- **THEN** this value is available for regression tests and runtime debugging

### Requirement: Authoritative snapshot exposes predicted vs authoritative pose

The client prediction system SHALL expose both the locally predicted pose and the authoritative pose for the controlled player at each snapshot.

#### Scenario: Diagnostics report predicted and authoritative poses
- **WHEN** the client has a locally predicted pose and receives an authoritative `PlayerState`
- **THEN** diagnostics can read both the predicted pose and the authoritative pose
- **THEN** the correction magnitude (difference between predicted and authoritative) is computable

### Requirement: Authoritative snapshot exposes correction magnitude

The client prediction system SHALL expose the correction magnitude applied during reconciliation for regression testing.

#### Scenario: Diagnostics report correction magnitude
- **WHEN** the client reconciles from authoritative `PlayerState`
- **THEN** diagnostics can read the correction magnitude applied
- **THEN** this value is available to verify that small server tick offset fluctuations do not cause excessive local corrections
