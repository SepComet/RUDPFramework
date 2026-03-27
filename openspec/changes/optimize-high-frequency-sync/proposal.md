## Why

The shared networking stack now has stable transport, dispatch, and session-lifecycle boundaries, but all gameplay traffic still rides the same reliable ordered KCP path. Stage 6 is needed now because high-frequency `PlayerInput` and `PlayerState` traffic can still suffer from head-of-line blocking, and clock synchronization is still coupled to heartbeat/session bookkeeping instead of being treated as a tunable sync policy.

## What Changes

- Add a shared high-frequency sync strategy layer that lets hosts assign delivery policies to gameplay synchronization messages instead of forcing `PlayerInput` and `PlayerState` through the same reliable ordered path as login and control traffic.
- Define latest-wins sequencing rules for high-frequency client input and authoritative player-state updates so stale packets can be discarded instead of blocking fresher movement data.
- Extract clock-synchronization sampling from `SessionManager` ownership into an explicit sync-policy component that can consume heartbeat or gameplay timing samples without changing lifecycle state semantics.
- Update client prediction and reconciliation flow so authoritative state correction is aligned with the new sync-message sequencing rules.
- Keep KCP as the only reliable transport for control-plane traffic such as login, logout, heartbeat/liveness, and other messages that still require guaranteed ordered delivery.

## Capabilities

### New Capabilities
- `network-sync-strategy`: Shared delivery-policy, sequencing, and reconciliation rules for high-frequency gameplay synchronization and independent clock-sync sampling.

### Modified Capabilities
- `kcp-transport`: Reliable KCP delivery remains the default control-plane path, but high-frequency `PlayerInput` and `PlayerState` are no longer required to stay on the same reliable ordered lane.
- `network-session-lifecycle`: Session lifecycle keeps heartbeat-focused liveness and timeout ownership, while clock-sync sampling moves to a separate sync strategy instead of living inside `SessionManager`.
- `shared-network-foundation`: The shared client/server runtime composes message routing with delivery-policy selection for reliable control traffic and high-frequency sync traffic without introducing Unity-specific dependencies.

## Impact

- Affected code: `MessageManager`, `SharedNetworkRuntime`, `NetworkManager`, `ServerNetworkHost`, transport composition around `ITransport`/future sync lanes, movement prediction/reconciliation code, and new sync-policy/state types.
- Affected behavior: login and other control traffic stay reliable, while `PlayerInput`/`PlayerState` follow latest-wins sequencing and stale-update dropping to reduce visible movement lag under packet loss or jitter.
- Affected tests: edit mode networking tests need explicit coverage for delivery-policy routing, stale packet rejection, prediction/correction behavior, and independent clock-sync sampling alongside existing lifecycle regressions.