## Why

The shared runtime already has an MVP-oriented dual-transport shape, but TODO step 5 is still undocumented at the spec level. We need to lock that contract before changing integration wiring so client and server hosts keep a clear, host-agnostic way to run reliable control traffic and high-frequency sync traffic on separate transport instances without prematurely redesigning `ITransport`.

## What Changes

- Document that `SharedNetworkRuntime` preserves separate reliable and sync transport inputs for the MVP and starts or stops both lanes when distinct instances are supplied.
- Document that `ServerNetworkHost` preserves the same dual-transport constructor shape and attaches receive handling for both lanes without forking message contracts.
- Require the shared runtime foundation to keep delivery-lane composition outside `ITransport` so hosts can use two transport instances instead of expanding the transport abstraction early.
- Add implementation tasks to verify or tighten regression coverage around dual-transport startup and message-lane usage in the shared runtime and server host.

## Capabilities

### New Capabilities

### Modified Capabilities
- `shared-network-foundation`: Narrow the shared runtime contract so MVP hosts explicitly preserve dual-transport composition through `SharedNetworkRuntime` and `ServerNetworkHost` without widening `ITransport`.

## Impact

Affected code is expected in `Assets/Scripts/Network/NetworkApplication/SharedNetworkRuntime.cs`, `Assets/Scripts/Network/NetworkHost/ServerNetworkHost.cs`, and related edit-mode regression tests. This change should not introduce new transport interfaces or Unity-specific dependencies into shared networking code.
