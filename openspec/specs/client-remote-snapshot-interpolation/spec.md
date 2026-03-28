# client-remote-snapshot-interpolation Specification

## Purpose
Define how the Unity client buffers and interpolates authoritative remote `PlayerState` snapshots for presentation-only movement smoothing.

## Requirements
### Requirement: Remote players interpolate between buffered authoritative snapshots
The client SHALL smooth remote-player presentation by buffering a small ordered set of accepted authoritative `PlayerState` snapshots and interpolating between buffered samples instead of lerping directly toward the latest snapshot.

#### Scenario: Remote presentation uses buffered samples
- **WHEN** the client has at least two buffered authoritative snapshots for a remote player
- **THEN** remote position and rotation are calculated from interpolation between buffered snapshots
- **THEN** the client does not smooth that remote player by directly lerping from the current transform to only the newest snapshot

### Requirement: Remote snapshot interpolation uses a documented fixed delay
The client SHALL render remote players at a small fixed interpolation delay behind the newest received authoritative snapshot timeline, and that delay/sample strategy MUST be documented in code comments or adjacent docs when the implementation is not otherwise obvious.

#### Scenario: Interpolation delay is explicit to maintainers
- **WHEN** a maintainer reads the remote snapshot interpolation path
- **THEN** the code or nearby documentation states the interpolation delay and how buffered samples are selected
- **THEN** the remote smoothing behavior can be tuned without reverse-engineering timing assumptions

### Requirement: Remote interpolation remains presentation-only
The client SHALL keep remote players non-predicted while using buffered snapshot interpolation. If interpolation cannot bracket two authoritative samples, the client MUST clamp to the latest accepted authoritative snapshot rather than extrapolating remote gameplay state.

#### Scenario: Missing future sample does not trigger remote prediction
- **WHEN** a remote player has fewer than two usable buffered snapshots for the current render time
- **THEN** the client presents the latest accepted authoritative snapshot for that remote player
- **THEN** the client does not extrapolate or simulate additional remote gameplay truth locally
