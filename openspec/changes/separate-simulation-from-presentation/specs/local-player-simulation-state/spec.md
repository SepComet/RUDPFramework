# local-player-simulation-state Specification

## Purpose

Define how the simulation layer maintains authoritative state, pending inputs, and computes the presentation target when receiving server PlayerState messages.

## ADDED Requirements

### Requirement: Simulation layer maintains authoritative baseline

The simulation layer SHALL maintain `_lastAuthoritativePosition`, `_lastAuthoritativeRotation`, and `_lastAcknowledgedTick` as the authoritative baseline. These are updated when the server acknowledges input through a PlayerState message.

#### Scenario: Authoritative baseline updates on PlayerState
- **WHEN** the client receives a PlayerState with tick T
- **THEN** `_lastAuthoritativePosition` is set to the PlayerState position
- **THEN** `_lastAuthoritativeRotation` is set to the PlayerState rotation
- **THEN** `_lastAcknowledgedTick` is set to T

### Requirement: Simulation layer maintains pending inputs

The simulation layer SHALL maintain a list of pending inputs that have been recorded locally but not yet acknowledged by the server.

#### Scenario: Pending inputs are pruned on acknowledgment
- **WHEN** the client receives a PlayerState with AcknowledgedMoveTick N
- **THEN** all pending inputs with tick <= N are removed from the pending list
- **THEN** remaining pending inputs (tick > N) are preserved for replay

### Requirement: Simulation layer computes presentation target on PlayerState

When receiving a server PlayerState, the simulation layer SHALL compute the presentation target by replaying unacknowledged pending inputs from the authoritative baseline, and update the `_presentationTarget`.

#### Scenario: Presentation target is computed after replay
- **WHEN** the client receives a PlayerState
- **THEN** all acknowledged inputs are pruned (tick <= AcknowledgedMoveTick)
- **THEN** remaining pending inputs are replayed starting from the authoritative position using 50ms fixed-step substeps
- **THEN** `_presentationTarget` is set to (authoritative position + replay displacement, authoritative rotation + replay rotation delta)

### Requirement: Simulation layer updates presentation target only on PlayerState

The simulation layer SHALL only update `_presentationTarget` when a new PlayerState is received. Between PlayerState messages, the presentation target remains constant.

#### Scenario: Presentation target is stable between PlayerState messages
- **WHEN** the client receives a PlayerState and computes `_presentationTarget`
- **AND** no further PlayerState is received in the following frames
- **THEN** `_presentationTarget` remains unchanged
- **THEN** the presentation layer continues lerping toward the same target
