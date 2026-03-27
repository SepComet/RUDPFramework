# Repository Guidelines

## Scope

This file is for AI coding agents working in this repository. Optimize for minimal, correct changes that preserve the shared client/server networking architecture.

## Workspace Rules

- Use LF line endings in files you create or edit.
- Do not introduce `UnityEngine` dependencies into shared networking code under `Assets/Scripts/Network/`.
- Keep Unity-only adapters and conversion helpers under `Assets/Scripts/Extensions/` or other host-specific locations.
- Do not revert unrelated user changes in the worktree.

## Project Layout

- `Assets/Scripts/Network/`: shared transport, message routing, session lifecycle, and host adapters.
- `Assets/Scripts/Extensions/`: Unity-specific helpers such as protobuf-to-Unity conversions.
- `Assets/Tests/EditMode/Network/`: NUnit edit-mode regression tests.
- `openspec/`: specs, active changes, and archived changes.

## Commands

- `dotnet build Network.EditMode.Tests.csproj -v minimal`
  Build runtime and edit-mode test assemblies.
- `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`
  Run the CLI regression suite.
- `openspec status --change "<name>"`
  Check change progress.
- `openspec instructions apply --change "<name>" --json`
  Read current implementation tasks before editing code.

If needed, set `DOTNET_CLI_HOME=.dotnet-home` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`.

## Code Change Guidance

- Follow existing C# style: 4-space indentation, `PascalCase` for public APIs, `_camelCase` for private fields.
- Prefer focused types. Keep `SessionManager` single-session; use coordinators such as `MultiSessionManager` for per-peer collections.
- Preserve the client single-session path unless the task explicitly changes it.
- Treat `network-main-thread-dispatch` as client-only. Shared/server work should align with `shared-network-foundation`, `network-session-lifecycle`, and `multi-session-lifecycle`.

## Testing Expectations

- Add or update NUnit tests with every network-layer behavior change.
- Cover both client single-session and server multi-session behavior when touching lifecycle code.
- Prefer explicit regression-style test names such as `Method_Scenario_ExpectedBehavior`.
- Do not finish a networking change without running `dotnet test` unless blocked.

## OpenSpec Workflow

- For substantial work: propose, apply, verify, then archive.
- Sync delta specs to `openspec/specs/` before archive when requirements changed.
- Keep implementation aligned with active change artifacts; update task checkboxes as work completes.
