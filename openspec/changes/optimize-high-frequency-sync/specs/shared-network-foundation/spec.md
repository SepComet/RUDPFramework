## MODIFIED Requirements

### Requirement: Shared core preserves current transport and message contracts
The shared client/server foundation SHALL preserve the envelope-based business-message contract across client and server hosts while allowing delivery-policy selection behind the shared message-routing layer. Reliable control traffic MUST continue to use the existing `ITransport` contract, and high-frequency sync traffic MUST be composable through a host-agnostic sync strategy without introducing Unity-specific runtime types into the shared networking core.

#### Scenario: Shared hosts exchange the same envelope format across delivery lanes
- **WHEN** a client host sends a business message through either the reliable control path or the high-frequency sync path
- **THEN** the payload is encoded with the same shared envelope and message-type contract
- **THEN** the server host decodes and routes it through shared networking logic without a host-specific protocol fork

#### Scenario: Hosts compose delivery-policy selection without Unity dependencies
- **WHEN** a non-Unity server host constructs the runtime networking stack with reliable control traffic and a high-frequency sync lane
- **THEN** it uses shared delivery-policy abstractions without depending on Unity frame-loop types
- **THEN** the Unity client can use the same abstractions while still supplying its own host-specific dispatch behavior