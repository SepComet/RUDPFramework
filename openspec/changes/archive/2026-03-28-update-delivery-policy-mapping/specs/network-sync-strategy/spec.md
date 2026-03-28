## MODIFIED Requirements

### Requirement: Hosts assign delivery policies to synchronization message types
The shared networking core SHALL allow hosts to map business message types to delivery policies. The default shared resolver used by `MessageManager` MUST map `MoveInput` and `PlayerState` to `HighFrequencySync`, while `ShootInput`, `CombatEvent`, and control-plane messages MUST resolve to `ReliableOrdered` unless a host intentionally supplies a different resolver.

#### Scenario: Default resolver sends movement and state traffic to the sync lane
- **WHEN** the runtime uses `DefaultMessageDeliveryPolicyResolver` to send `MoveInput` or `PlayerState`
- **THEN** the resolver returns `HighFrequencySync`
- **THEN** `MessageManager` sends that envelope through the sync transport lane when one is configured

#### Scenario: Default resolver keeps shooting and combat events on the reliable lane
- **WHEN** the runtime uses `DefaultMessageDeliveryPolicyResolver` to send `ShootInput` or `CombatEvent`
- **THEN** the resolver returns `ReliableOrdered`
- **THEN** `MessageManager` sends that envelope through the reliable transport lane

#### Scenario: Default resolver preserves reliable control traffic
- **WHEN** the runtime uses `DefaultMessageDeliveryPolicyResolver` to send login, logout, heartbeat, or other session-management messages
- **THEN** the resolver returns `ReliableOrdered`
- **THEN** those messages continue to use the reliable transport path
