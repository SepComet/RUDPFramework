## Context

The repository already has solid shared-network regression coverage for message typing, lane policy, stale filtering, and recent server-authoritative movement/combat behavior. What remains thin is the gameplay-flow layer that joins client input send paths, client receive/apply paths, remote snapshot decisions, and at least one fake-transport round trip across the server authority loop. The main constraint is to improve confidence without turning edit-mode tests into Unity-scene integration tests or changing shared networking contracts just to satisfy test code.

## Goals / Non-Goals

**Goals:**
- Add gameplay-flow regression tests that protect the MVP contract from client input through authoritative server outputs and client-side application.
- Keep low-level routing assertions in focused tests while moving broader gameplay expectations into runtime-level regression fixtures.
- Reuse existing fake transports, host/runtime entry points, and explicit runtime handles where possible.
- Introduce only minimal test seams when existing runtime surfaces cannot expose the state needed for stable assertions.

**Non-Goals:**
- Rework production gameplay architecture beyond what is minimally required for observability in tests.
- Add Unity play-mode or scene-driven end-to-end automation.
- Change message definitions, delivery policy rules, or authority ownership as part of this testing change.

## Decisions

### Decision: Keep regression coverage layered instead of expanding `MessageManagerTests`
`MessageManagerTests` should remain responsible for lane-policy and message-routing invariants only. Gameplay send/receive/application assertions belong in higher-level edit-mode tests that exercise `NetworkManager`, authoritative client state application, interpolation buffers, and server runtime handles together.

Alternative considered: continue adding gameplay assertions into `MessageManagerTests`.
- Rejected because it would blur transport-policy failures with gameplay-flow failures and make the tests harder to maintain.

### Decision: Prefer existing fake transports and runtime surfaces, add only narrow observability seams
The test suite should keep using fake transports and explicit runtime handles to drive inputs and inspect outputs. If a client-side flow cannot be asserted without reaching into private state, add a narrow seam such as an inspectable snapshot buffer view or combat-application callback surface rather than introducing Unity-only dependencies into shared code.

Alternative considered: add broad test-only hooks or mock-heavy abstractions around the whole networking layer.
- Rejected because it would distort the production architecture and create maintenance burden unrelated to MVP behavior.

### Decision: Add one explicit fake-transport gameplay round-trip that spans client and server responsibilities
Beyond isolated unit-style tests, the suite should include at least one end-to-end fake-transport regression that proves `MoveInput -> PlayerState` and `ShootInput -> CombatEvent` still flow through the MVP authority model. This test should stay deterministic, edit-mode friendly, and limited to the shared/client runtime surfaces already used in the repository.

Alternative considered: rely only on separate client tests and separate server tests.
- Rejected because it would leave the handoff between client send paths and authoritative server outputs unprotected.

## Risks / Trade-offs

- [Risk] Gameplay-flow tests may become brittle if they assert too many incidental details. → Mitigation: assert stable observable outcomes such as lane choice, accepted state updates, interpolation decisions, and authoritative event types rather than internal call order unless the order is part of the contract.
- [Risk] End-to-end fake-transport tests may need extra setup and slow the suite. → Mitigation: keep the number of full-flow tests small and reuse focused fixtures/helpers.
- [Risk] Client observability needs may tempt test-only architecture changes. → Mitigation: require every new seam to be narrow, production-safe, and useful for diagnostics as well as tests.

## Migration Plan

1. Add or extend edit-mode fixtures for client send/receive/application flow and remote snapshot buffering.
2. Add one deterministic fake-transport end-to-end gameplay test that drives both movement and combat authority.
3. Leave existing lane-policy tests in place, trimming or extending `MessageManagerTests` only where delivery-policy regressions specifically need coverage.
4. Update TODO/OpenSpec task tracking after the regression suite protects the MVP gameplay loop.

## Open Questions

- None. The remaining work is implementation detail inside the agreed MVP gameplay surfaces.
