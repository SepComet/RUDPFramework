## Follow-up: Local Controlled-Player Jitter

Current assessment:

- Loopback repro means transport delay is not the primary cause of the remaining local-player jitter.
- The next round should focus on deterministic prediction/reconciliation timing before adding more local smoothing.

Step-by-step plan:

1. Align replay integration granularity with live prediction
- Replace one-shot replay of an accumulated input duration with fixed substeps.
- Ensure replay uses the same movement integration shape as the normal `FixedUpdate` prediction path, especially for turn-and-move input.

2. Align client prediction cadence with server authoritative cadence
- Introduce an explicit local prediction/replay cadence derived from the authoritative movement cadence.
- Avoid mixing client-side `Time.fixedDeltaTime` prediction with server-side fixed-cadence authoritative integration in reconciliation-sensitive paths.

3. Stabilize or remove send-rate oscillation driven by server tick offset
- Revisit `MovementComponent.SetServerTick(...)` and stop toggling `_sendInterval` directly between nearby values when the offset crosses zero.
- If clock correction is still needed, add hysteresis or filtering so the send cadence does not bounce frame-to-frame.

4. Re-measure controlled-player correction after timing fixes
- Keep remote-player interpolation as-is; do not treat local-player jitter as a remote interpolation problem.
- Only refine local visual correction further if meaningful residual error remains after steps 1-3.

5. Add regression coverage and diagnostics for the remaining jitter path
- Add tests that compare live prediction and replayed prediction under the same turn/throttle sequence.
- Add tests for server tick offset calibration so small offset sign changes do not continuously retarget send cadence.
- Add or expose diagnostics for acknowledged move tick, predicted pose, authoritative pose, and correction magnitude per snapshot.

Acceptance:

- Controlled-player loopback movement no longer shows repeated small pull-back under steady turn-and-move input.
- Replay after authoritative reconciliation produces the same trajectory shape as forward local prediction for the same input sequence.
- Small server tick offset fluctuations do not cause visible local cadence oscillation.
