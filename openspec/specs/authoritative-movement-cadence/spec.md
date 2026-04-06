# authoritative-movement-cadence Specification

## Purpose
Define the fixed authoritative movement cadence contract and its observability for server runtime diagnostics and regression coverage.

## Requirements
### Requirement: Server authoritative movement uses a fixed cadence contract
The shared server runtime SHALL define a fixed authoritative movement cadence for simulation and snapshot production. Authoritative movement updates MUST be stepped from that configured cadence instead of arbitrary caller-provided elapsed values.

#### Scenario: Runtime advances movement using configured cadence
- **WHEN** the server runtime advances authoritative movement while one or more managed peers have movement state
- **THEN** the authoritative movement coordinator steps simulation using the configured cadence interval
- **THEN** the same cadence governs later authoritative `PlayerState` production for that runtime

### Requirement: Cadence information is observable for diagnostics and regression tests
The shared runtime SHALL expose the active authoritative movement cadence through diagnostics or runtime state that tests and debugging tools can read without inspecting private loop internals.

#### Scenario: Tests can read active movement cadence
- **WHEN** an edit-mode regression or debugging path inspects the server runtime after movement setup
- **THEN** it can observe the authoritative movement cadence configured for that runtime
- **THEN** the observed value matches the cadence used by authoritative movement stepping
