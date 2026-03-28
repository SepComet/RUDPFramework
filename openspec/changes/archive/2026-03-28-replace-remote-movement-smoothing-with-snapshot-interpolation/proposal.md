## Why

Remote players are still smoothed by lerping directly toward the latest authoritative `PlayerState`, which makes presentation sensitive to uneven packet spacing and late packets. Now that the client already owns authoritative per-player snapshots, the next MVP step is to buffer remote snapshots and interpolate across them so remote motion stays smooth without becoming locally authoritative.

## What Changes

- Add a dedicated client-side remote snapshot interpolation path for authoritative `PlayerState` updates.
- Buffer a small number of remote authoritative snapshots and interpolate between buffered samples instead of lerping directly to the newest packet.
- Reject stale remote snapshots by tick before they can affect presentation.
- Keep remote players non-predicted and presentation-only while documenting the interpolation delay/sample strategy used by the MVP client.

## Capabilities

### New Capabilities
- `client-remote-snapshot-interpolation`: Define how the client buffers and interpolates authoritative remote `PlayerState` snapshots for presentation-only smoothing.

### Modified Capabilities
- `client-authoritative-player-state`: Remote-player presentation changes from "latest snapshot apply" to "buffered authoritative snapshot interpolation" while keeping stale-state rejection and authoritative ownership requirements.

## Impact

- Affected code: `Assets/Scripts/MovementComponent.cs`, `Assets/Scripts/Player.cs`, and any new remote snapshot helper used by the client.
- Affected tests: `Assets/Tests/EditMode/Network/` regression coverage for remote snapshot buffering, stale-packet rejection, and interpolation behavior.
- Documentation/spec impact: new interpolation capability spec plus a delta update for `client-authoritative-player-state`.
