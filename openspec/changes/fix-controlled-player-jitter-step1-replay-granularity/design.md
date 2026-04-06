## Context

本地回环测试中受控玩家出现小幅抖动。抖动根源之一是 `ReplayPendingInputs()` 中回放时对每个 `PredictedMoveStep` 的一次性大时长积分，与实时预测路径中 FixedUpdate 按 `Time.fixedDeltaTime` 逐步积分的形状不一致。

当前 `ReplayPendingInputs()` 实现：
```csharp
foreach (var replayInput in replayInputs)
{
    ApplyTankMovementToPredictedState(
        replayInput.Input.TurnInput,
        replayInput.Input.ThrottleInput,
        replayInput.SimulatedDurationSeconds);  // 一次性传入总时长
}
```

Tank 运动学中旋转影响前进方向：`heading(t+dt) = heading(t) + turnInput * turnSpeed * dt`，`position(t+dt) = position(t) + forward(heading(t+dt)) * throttleSpeed * dt`。逐步积分和一次性积分在 dt 较大时产生分歧。

## Goals / Non-Goals

**Goals:**
- `ReplayPendingInputs()` 按固定步长逐步积分，与 FixedUpdate 预测路径完全一致
- 回放结果与逐步实时预测的轨迹一致，消除因积分形状不同导致的残余误差
- 不改变外部接口，只修改内部积分方式

**Non-Goals:**
- 不修改服务端的 50ms cadence
- 不解决 send interval 摆动问题（Step 3 范畴）
- 不修改 visual correction 逻辑（Step 4 范畴）

## Decisions

### Decision: 步长取服务端的 SimulationInterval（50ms），而非客户端的 Time.fixedDeltaTime（20ms）

**选择**：按服务端 `SimulationInterval`（50ms）作为回放步长。

**理由**：
- 服务端以 50ms 步长积分产生 authoritative state，客户端回放必须与其一致才能消除偏差
- 客户端 FixedUpdate 20ms 是渲染/物理步长，不代表服务端模拟粒度
- 每个 `PredictedMoveStep` 的 `SimulatedDurationSeconds` 可能是 50ms、100ms 等，按 50ms 步长逐次推进即可

**替代方案**：
- 用 20ms 步长回放：与客户端 FixedUpdate 一致，但与服务端不同步，仍会产生偏差
- 用 `SimulatedDurationSeconds` 作为单步：即当前行为，会导致非线性分歧

### Decision: 循环内部分步模拟，不引入新的状态累积

**选择**：在 `ReplayPendingInputs` 循环内按 50ms 步长迭代调用 `ApplyTankMovementToPredictedState`。

**实现方式**：
```csharp
private void ReplayPendingInputs(IReadOnlyList<PredictedMoveStep> replayInputs)
{
    const float serverStepSeconds = 0.05f;  // 50ms，服务端 SimulationInterval
    foreach (var replayInput in replayInputs)
    {
        var remaining = replayInput.SimulatedDurationSeconds;
        while (remaining > 0f)
        {
            var step = Mathf.Min(remaining, serverStepSeconds);
            ApplyTankMovementToPredictedState(
                replayInput.Input.TurnInput,
                replayInput.Input.ThrottleInput,
                step);
            remaining -= step;
        }
    }
    // ...
}
```

**理由**：
- 不改变 `PredictedMoveStep` 结构体接口，只修改消费方式
- 无需新增临时状态变量
- 逻辑清晰，与实时预测路径的积分形状完全一致

## Risks / Trade-offs

- **[风险]** 如果 `SimulatedDurationSeconds` 累计值有浮点误差，循环可能产生多一步或少一步的小偏差
  - **缓解**：使用 `Mathf.Min(remaining, step)` 保护，最后一步自然截断；或对 `remaining -= step` 后加 epsilon 比较
- **[风险]** 50ms 步长对极短的输入（比如只有一帧的输入）会产生额外计算
  - **可接受**：额外一次函数调用，代价可忽略
