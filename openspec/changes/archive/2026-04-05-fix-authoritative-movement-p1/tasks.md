## 1. Cadence Contract

- [x] 1.1 Introduce a fixed authoritative movement cadence configuration/runtime surface that server movement stepping can read and tests can observe.
- [x] 1.2 Update the server authoritative movement loop and snapshot emission path to use the configured cadence instead of arbitrary elapsed input.

## 2. Controlled Reconciliation

- [x] 2.1 Refactor controlled-player reconciliation to distinguish bounded correction from hard snap after authoritative replay.
- [x] 2.2 Wire cadence-aware correction thresholds through the local movement/sync path without changing remote-player interpolation rules.

## 3. Regression Coverage

- [x] 3.1 Add or update edit-mode tests that prove authoritative movement stepping and snapshot output follow the configured cadence.
- [x] 3.2 Add or update edit-mode tests that prove controlled-player reconciliation uses bounded correction for small error and hard snap for large divergence.
- [x] 3.3 Re-run OpenSpec status and confirm the change is apply-ready.
