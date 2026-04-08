## Why

当前 `MovementComponent` 将模拟层（pending inputs 维护、服务器校正、重放）和表现层（视觉插值、bounded correction）耦合在一起。bounded correction 的收敛目标是实时变化的 server position，导致客户端永远在追赶——每次收到服务器状态，收敛目标就变了。表现为本地移动平滑，加上网络同步后抖动。

## What Changes

- **新增** `LocalPlayerPresentationState` 类型：持有 `_currentPosition/Rotation`（当前显示）和 `_targetPosition/Rotation`（模拟层给的目标）
- **新增** `LocalPlayerSimulationState` 类型：持有 `_lastAuthoritativePosition/Rotation`、`_pendingInputs`、`_presentationTarget`
- **重构** `MovementComponent`：`OnAuthoritativeState` 只更新 `_presentationTarget`，不直接修改 rigid.position
- **重构** 表现层：每帧用 Lerp 将 `_currentPosition` 插值到 `_targetPosition`，再设置 rigid.position
- **移除** `ControlledPlayerCorrection` 相关逻辑：bounded correction 被表现层 Lerp 替代

## Capabilities

### New Capabilities
- `local-player-presentation-state`: 表现层状态（current/target position & rotation）及每帧插值更新
- `local-player-simulation-state`: 模拟层状态（authoritative baseline、pending inputs、presentation target）

### Modified Capabilities
- `local-player-reconciliation`: 模拟层收到服务器状态后计算 PresentationTarget，表现层负责插值收敛，不再使用 bounded correction

## Impact

- 新增 `Assets/Scripts/Network/NetworkApplication/LocalPlayerPresentationState.cs`
- 新增 `Assets/Scripts/Network/NetworkApplication/LocalPlayerSimulationState.cs`
- 修改 `Assets/Scripts/MovementComponent.cs`
- 删除 `Assets/Scripts/ControlledPlayerCorrection.cs`（或保留作他用）
