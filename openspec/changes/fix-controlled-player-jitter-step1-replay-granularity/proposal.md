## Why

本地回环测试中出现受控玩家（controlled player）持续小幅抖动。经分析，问题根源之一是回滚重放（replay）时积分粒度与实时预测不一致：实时预测在 FixedUpdate 中按 `Time.fixedDeltaTime`（20ms）逐步积分，而回放时把某个输入的累计时长一次性喂给 `ApplyTankMovementToPredictedState()`。Tank 运动学是非线性的——边转向边前进时，每步的旋转角影响下一步的前进方向，导致逐步积分和一次性积分的轨迹不同。这种偏差在每次对账后出现，被 visual correction 反复拉回，表现为细碎抖动。

## What Changes

1. **修改 `ReplayPendingInputs()` 的积分方式**：将一次性大时长积分改为固定步长的逐步积分，与 `FixedUpdate` 预测路径的积分形状完全一致
2. **`PredictedMoveStep.SimulatedDurationSeconds` 的处理语义变更**：`SimulatedDurationSeconds` 仍记录该输入的总模拟时长，但 replay 时按服务端的 50ms 步长（`ServerAuthoritativeMovementConfiguration.SimulationInterval`）进行分步模拟
3. **添加测试**：比较相同输入序列下逐步预测和回放预测的轨迹一致性，验证修复效果

## Capabilities

### New Capabilities
- `client-prediction-replay-granularity`: 定义客户端回放预测与实时预测使用相同积分步长的行为约束和验证方式

### Modified Capabilities
- `client-gameplay-input`: 扩展 `ReplayPendingInputs` 的实现要求，明确回放必须使用固定步长逐步积分而非一次性累积积分

## Impact

- **涉及代码**：`Assets/Scripts/MovementComponent.cs` 中的 `ReplayPendingInputs()` 方法
- **涉及测试**：`Assets/Tests/EditMode/Network/GameplayFlowRoundTripTests.cs` 或新建回归测试
- **其他系统**：`PredictedMoveStep` 结构体（`ClientPredictionBuffer.cs`）的语义略有调整，但接口不变
