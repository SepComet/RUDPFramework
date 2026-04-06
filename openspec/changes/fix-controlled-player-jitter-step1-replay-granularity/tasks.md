## 1. 理解和实现

- [x] 1.1 理解 `ApplyTankMovementToPredictedState` 的积分逻辑（旋转 → 前进方向的依赖关系）
- [x] 1.2 理解当前 `ReplayPendingInputs` 的一次性积分行为与问题
- [x] 1.3 在 `MovementComponent` 中引入服务端 `SimulationInterval` 的引用（50ms 步长常量）

## 2. 修改 `ReplayPendingInputs` 实现

- [x] 2.1 修改 `ReplayPendingInputs` 循环，将每个 `PredictedMoveStep` 的总时长按 50ms 步长分步模拟
- [x] 2.2 添加浮点截断保护，确保所有时长都被消耗而无遗失
- [x] 2.3 验证修改后的实现与 `FixedUpdate` 预测路径的积分形状一致

## 3. 添加回归测试

- [x] 3.1 在 `GameplayFlowRoundTripTests.cs` 或新建测试文件中添加轨迹一致性测试
- [x] 3.2 测试用例：相同 turn+throttle 输入序列，逐步预测 vs 回放预测的最终位置和旋转相等
- [x] 3.3 测试用例：非线性运动（同时转向和前进），验证逐步积分与一次性积分的结果不同
- [x] 3.4 测试用例：非 50ms 倍数的总时长（如 0.12s），验证分步后无遗失

## 4. 验证

- [ ] 4.1 运行所有 EditMode 测试确保无回归（在 Unity Editor 内执行）
- [ ] 4.2 本地回环验证抖动是否改善
