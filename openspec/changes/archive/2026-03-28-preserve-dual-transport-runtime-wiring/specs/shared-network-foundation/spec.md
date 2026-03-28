## MODIFIED Requirements

### Requirement: Shared core preserves current transport and message contracts
The shared client/server foundation SHALL preserve the envelope-based business-message contract across client and server hosts while allowing delivery-policy selection behind the shared message-routing layer. Reliable control traffic MUST continue to use the existing `ITransport` contract, and high-frequency sync traffic MUST remain composable by supplying a second host-agnostic sync transport instance to `SharedNetworkRuntime` or `ServerNetworkHost` rather than by expanding `ITransport` for MVP-specific lane semantics. The shared message-type contract MUST allow hosts to distinguish `MoveInput`, `ShootInput`, `CombatEvent`, and `PlayerState` as separate business messages across both delivery lanes.

#### Scenario: Shared runtime starts distinct reliable and sync transports
- **WHEN** a client host constructs `SharedNetworkRuntime` with one reliable transport and a different sync transport instance
- **THEN** starting the runtime starts both transport instances
- **THEN** stopping the runtime stops both transport instances while keeping the same shared message-routing contract

#### Scenario: Server host composes both transport lanes without protocol forks
- **WHEN** a server host constructs `ServerNetworkHost` with one reliable transport and a different sync transport instance
- **THEN** it observes inbound activity from both transport lanes through shared host logic
- **THEN** it routes messages with the same envelope and message-type contract instead of defining a lane-specific protocol fork

#### Scenario: Shared hosts exchange the same envelope format across delivery lanes
- **WHEN** a client host sends a business message through either the reliable control path or the high-frequency sync path
- **THEN** the payload is encoded with the same shared envelope and message-type contract
- **THEN** the server host decodes and routes it through shared networking logic without a host-specific protocol fork

#### Scenario: Hosts preserve dual-transport composition outside ITransport
- **WHEN** a host needs separate reliable and sync lanes for MVP gameplay traffic
- **THEN** it provides separate transport instances plus delivery-policy configuration to shared runtime or host entry points
- **THEN** the shared networking core does not require `ITransport` itself to grow MVP-specific multi-lane APIs
