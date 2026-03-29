## Why

The MVP networking work is already implemented, but the TODO still requires a final build-and-test verification in a runnable local environment. This change closes that gap by making the verification step explicit, repeatable, and recorded against the current gameplay regression suite.

## What Changes

- Define a small verification capability for running the repository's edit-mode build and test commands in an environment with the required .NET runtime.
- Record the actual build and test result for the current MVP networking and gameplay-flow regression suite.
- Update project tracking so TODO step 10 reflects the completed verification state and any remaining environment caveats.

## Capabilities

### New Capabilities
- `build-test-verification`: Defines the required local environment assumptions, commands, and recorded result for final MVP build/test verification.

### Modified Capabilities
- None.

## Impact

Affected areas include OpenSpec tracking under `openspec/`, the root `TODO.md`, and the CLI verification path driven by `dotnet build Network.EditMode.Tests.csproj -v minimal` and `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`.
