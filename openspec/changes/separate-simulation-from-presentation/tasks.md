## 1. 准备阶段

- [x] 1.1 阅读 `openspec/specs/local-player-presentation-state/spec.md` 和 `openspec/specs/local-player-simulation-state/spec.md`
- [x] 1.2 阅读现有 `MovementComponent.cs` 和 `ControlledPlayerCorrection.cs`

## 2. 新增表现层状态

- [x] 2.1 添加 `_presentationPosition`、`_presentationRotation`、`_presentationTargetPosition`、`_presentationTargetRotation` 字段到 MovementComponent
- [x] 2.2 实现 `UpdatePresentation()` 方法：Lerp 或 snap 到 target，设置 rigid.position/rotation

## 3. 新增模拟层状态

- [x] 3.1 使用现有的 `_predictionBuffer` 和新增的 authoritative baseline 字段
- [x] 3.2 在 `Reconcile()` 中实现：prune inputs + replay + 计算 target

## 4. 重构 MovementComponent

- [x] 4.1 添加表现层和模拟层状态字段（不使用独立类型，直接在 MovementComponent 中）
- [x] 4.2 `OnAuthoritativeState()` 移除 `ClearPendingInputs()` 和 `_simulationAccumulator = 0f`（移到 Reconcile 后）
- [x] 4.3 `Update()` 中调用 `UpdatePresentation()`
- [x] 4.4 移除 `ControlledPlayerCorrection` 相关逻辑（bounded correction 被 Lerp 替代）

## 5. 验证

- [x] 5.1 编译验证（代码无语法错误）
- [ ] 5.2 关闭网络同步：移动平滑
- [ ] 5.3 开启网络同步：抖动消除或显著减少
