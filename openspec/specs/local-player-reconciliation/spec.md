# local-player-reconciliation Specification

## Purpose

Define how the local (controlled) player reconciles client-side prediction with server authoritative state. This capability ensures that when the client receives a server `PlayerState`, it treats that snapshot as the latest gameplay baseline, rebuilds local prediction from still-unacknowledged inputs, and smooths visible presentation toward the rebuilt predicted pose.

## Requirements

### Requirement: Reconcile applies authoritative state and replays unconfirmed inputs in correct order

The controlled-client reconciliation path SHALL treat a newly accepted authoritative `PlayerState` as the latest gameplay baseline, prune only pending movement inputs whose tick is less than or equal to `AcknowledgedMoveTick`, replay all still-unacknowledged pending inputs from that authoritative baseline using fixed-step substeps matching the server authoritative movement cadence, and publish the replay result as the controlled player's latest predicted simulation pose. Presentation smoothing MUST consume that rebuilt predicted pose as a target without redefining the gameplay truth of the replay result.

#### Scenario: Authoritative acceptance rebuilds prediction from the latest baseline
- **WHEN** the controlled player accepts an authoritative `PlayerState` whose acknowledged movement-input tick is `N`
- **THEN** the reconciliation treats the received authoritative position and rotation as the new gameplay baseline
- **THEN** the reconciliation replays all pending inputs with tick greater than `N` using 50ms fixed-step substeps
- **THEN** the replay result becomes the controlled player's latest predicted simulation pose for that authoritative update
- **THEN** the presentation layer receives that predicted pose as its smoothing target

#### Scenario: Repeated authoritative updates rebuild prediction without consuming pending inputs
- **WHEN** the controlled player accepts two increasing authoritative `PlayerState` snapshots before all pending inputs have been acknowledged
- **THEN** each accepted snapshot rebuilds the predicted simulation pose from its own authoritative baseline
- **THEN** only inputs acknowledged by the newer snapshot are pruned from the pending-input buffer
- **THEN** still-unacknowledged inputs remain available for replay against later authoritative snapshots

#### Scenario: Visible smoothing does not redefine rebuilt gameplay truth
- **WHEN** the controlled player has a rebuilt predicted simulation pose and a visible presentation pose that has not yet converged
- **THEN** gameplay logic continues to treat the rebuilt predicted pose as the latest client prediction truth
- **THEN** the visible pose may temporarily differ while smoothing converges
- **THEN** a large divergence may still hard-snap the visible pose directly to the rebuilt predicted pose

### Requirement: Bounded correction handles residual error after replay

The controlled-client reconciliation SHALL compare the controlled player's visible presentation pose against the rebuilt predicted simulation pose after replay, interpolate the visible pose toward that predicted pose for small residual error, and snap the visible pose directly to the predicted pose when divergence exceeds the configured snap threshold.

#### Scenario: Small residual error uses presentation interpolation
- **WHEN** the controlled player completes replay and the remaining distance between visible pose and rebuilt predicted pose is within the configured snap threshold
- **THEN** the client keeps the rebuilt predicted pose as presentation target
- **THEN** the visible pose converges toward that target through presentation smoothing across later frames
- **THEN** the replayed predicted pose remains unchanged as gameplay truth during that convergence

#### Scenario: Large divergence snaps visible pose to rebuilt predicted pose
- **WHEN** the controlled player completes replay and the remaining distance between visible pose and rebuilt predicted pose exceeds the configured snap threshold
- **THEN** the client snaps the visible position and rotation directly to the rebuilt predicted pose
- **THEN** any previous presentation-only smoothing state is cleared or replaced
- **THEN** later local prediction continues from the rebuilt predicted baseline
