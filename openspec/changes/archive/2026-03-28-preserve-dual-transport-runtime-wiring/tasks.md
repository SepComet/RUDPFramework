## 1. Verify shared dual-transport runtime boundaries

- [x] 1.1 Verify `SharedNetworkRuntime` still preserves the MVP constructor shape with a primary reliable `ITransport`, an optional sync `ITransport`, and no new multi-lane transport API.
- [x] 1.2 Verify `ServerNetworkHost` still preserves the same dual-transport constructor shape, starts both lanes when distinct transports are supplied, and attaches inbound handling for both transports.

## 2. Add regression coverage for dual-lane lifecycle behavior

- [x] 2.1 Add or extend edit-mode tests to prove `SharedNetworkRuntime` starts and stops two distinct transport instances while continuing to route sync-lane messages through the configured sync transport.
- [x] 2.2 Add or extend edit-mode tests to prove `ServerNetworkHost` starts two distinct transport instances and records inbound activity from the sync transport without cross-lane protocol changes.

## 3. Validate the MVP shared-network contract

- [x] 3.1 Run `dotnet build Network.EditMode.Tests.csproj -v minimal`.
- [x] 3.2 Run `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`.
