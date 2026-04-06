# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity MOBA game with a custom RUDP (Reliable UDP) networking layer built on KCP. The architecture follows a **server-authoritative** hybrid sync model:

- **Client**: captures input, sends `MoveInput`/`ShootInput`, predicts local movement, interpolates remote players
- **Server**: owns gameplay truth (position, HP, combat resolution), broadcasts authoritative `PlayerState`/`CombatEvent`

## Commands

```bash
# Build test assemblies
dotnet build Network.EditMode.Tests.csproj -v minimal

# Run regression suite
dotnet test Network.EditMode.Tests.csproj --no-build -v minimal
```

Set `DOTNET_CLI_HOME=.dotnet-home` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` if needed.

## Architecture

### Dual-Lane Transport

The transport layer uses two distinct lanes with different delivery semantics:

| Lane | Policy | Messages |
|------|--------|----------|
| **Sync lane** (`HighFrequencySync`) | Latest-wins, stale-drop | `MoveInput`, `PlayerState` |
| **Reliable lane** (`ReliableOrdered`) | Ordered, guaranteed delivery | `ShootInput`, `CombatEvent`, login/heartbeat |

Never mix messages with different delivery requirements into the same lane.

### Directory Structure

```
Assets/Scripts/Network/
├── Defines/           # MessageType enum, protobuf message definitions
├── NetworkTransport/  # KcpTransport, ReliableUdpTransport, ITransport
├── NetworkApplication/# MessageManager, SessionManager, DeliveryPolicy, dispatchers
└── NetworkHost/      # ServerNetworkHost, ServerAuthoritativeMovementCoordinator, ServerRuntimeHandle

Assets/Scripts/Extensions/   # Unity-specific helpers (protobuf-to-Unity conversions)
Assets/Tests/EditMode/Network/ # NUnit edit-mode regression tests
openspec/               # Specs and change artifacts
```

### Key Types

- `MessageManager`: routes all gameplay messages through `Envelope`, maps `MessageType` → `DeliveryPolicy`
- `ServerNetworkHost`: server lifecycle, session state, hosts authoritative coordinators
- `ServerAuthoritativeMovementCoordinator`: server-side movement validation and state broadcast
- `ServerAuthoritativeCombatCoordinator`: server-side combat resolution
- `SyncSequenceTracker`: stale-packet filtering for sync lane (keyed by `playerId + tick`)
- `ClientPredictionBuffer`: stores pending inputs for local player prediction/replay
- `RemotePlayerSnapshotInterpolator`: buffers remote `PlayerState` for smooth interpolation

### Client Prediction Flow

1. Client sends `MoveInput` on sync lane
2. Client immediately applies predicted movement locally
3. Server receives, validates, updates authoritative state
4. Server broadcasts `PlayerState` on sync lane
5. Client compares authoritative state against predicted; corrects if divergence exceeds threshold

### OpenSpec Workflow

Use `openspec` commands for substantial changes:
- `openspec status --change "<name>"` — check progress
- `openspec instructions apply --change "<name>" --json` — read current tasks before editing

## Code Rules

- **No Unity dependencies in `Assets/Scripts/Network/`** — shared networking must remain engine-agnostic
- **Server owns gameplay truth** — clients submit intent only, never finalize position/HP/combat
- **Tick required on all gameplay messages** — enables stale filtering and reconciliation
- **4-space indentation, `PascalCase` public APIs, `_camelCase` private fields**
- **Add NUnit tests** for any network-layer behavior change; use explicit regression-style names
