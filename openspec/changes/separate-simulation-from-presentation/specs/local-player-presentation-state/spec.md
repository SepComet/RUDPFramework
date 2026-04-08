# local-player-presentation-state Specification

## Purpose

Define how the local player's presentation layer holds current display state and smoothly interpolates toward the simulation layer's target state each frame.

## ADDED Requirements

### Requirement: Presentation layer holds current and target state

The local player's presentation layer SHALL maintain `_currentPosition` and `_currentRotation` (the actively displayed state) separately from `_targetPosition` and `_targetRotation` (the simulation layer's output).

#### Scenario: Presentation state initializes from first simulation target
- **WHEN** the presentation layer is initialized or first receives a simulation target
- **THEN** `_currentPosition` and `_currentRotation` are set equal to the initial target
- **THEN** subsequent updates lerp toward the target

### Requirement: Presentation layer lerps toward target each frame

The presentation layer SHALL each frame interpolate `_currentPosition` and `_currentRotation` toward `_targetPosition` and `_targetRotation` using linear interpolation, then apply the result to the Rigidbody.

#### Scenario: Lerp position and rotation toward target
- **WHEN** the presentation layer updates each frame with interpolation alpha α
- **THEN** `_currentPosition` is updated to `Vector3.Lerp(_currentPosition, _targetPosition, α)`
- **THEN** `_currentRotation` is updated to `Quaternion.Slerp(_currentRotation, _targetRotation, α)`
- **THEN** `_rigid.position` and `_rigid.rotation` are set to `_currentPosition` and `_currentRotation`

### Requirement: Presentation layer snaps when target error exceeds threshold

When the distance between `_currentPosition` and `_targetPosition` exceeds the snap threshold, the presentation layer SHALL immediately snap `_currentPosition` to `_targetPosition` without lerping.

#### Scenario: Snap when error exceeds threshold
- **WHEN** `Vector3.Distance(_currentPosition, _targetPosition) > SnapThreshold`
- **THEN** `_currentPosition` is set equal to `_targetPosition`
- **THEN** `_currentRotation` is set equal to `_targetRotation`
- **THEN** no lerping occurs in this frame
