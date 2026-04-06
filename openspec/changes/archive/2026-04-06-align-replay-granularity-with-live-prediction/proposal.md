## Why

The current client prediction replay path uses a one-shot replay of an accumulated input duration, while live prediction uses fixed-step integration. This mismatch causes local player jitter during steady turn-and-move input — the replay produces a different trajectory than forward prediction for the same input sequence.

## What Changes

- Replace one-shot replay of accumulated input duration with fixed substeps matching the live prediction integration shape
- Ensure replay uses the same movement math (turn-and-move input handling) as normal `FixedUpdate` prediction
- Add regression test comparing live prediction vs replayed prediction under the same turn/throttle sequence
- Introduce explicit diagnostics for acknowledged move tick, predicted pose, authoritative pose, and correction magnitude

## Capabilities

### New Capabilities

- `client-prediction-replay`: Replay of pending client inputs after authoritative state acknowledgement uses fixed-step substeps that mirror live prediction integration, ensuring identical trajectory output for identical input sequences
- `client-prediction-diagnostics`: Explicit diagnostics exposing acknowledged move tick, predicted pose, authoritative pose, and correction magnitude per snapshot for regression testing and runtime debugging

### Modified Capabilities

- `client-authoritative-player-state`: Add requirement that replay integration must use fixed substeps matching live prediction cadence, not accumulated one-shot duration

## Impact

- **Affected code**: `ClientPredictionBuffer`, movement integration paths in `MovementComponent` or equivalent
- **No breaking API changes** to message types or transport
- **Testing impact**: New regression tests required for prediction/replay parity
