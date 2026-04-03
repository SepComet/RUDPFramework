## 当前修补目标：客户端移动抖动

### 已确认的主要问题

1. 客户端当前把 `PlayerState.Tick` 当成“已确认输入 tick”用于 prediction buffer 修剪与重放，但服务端当前发出的 `PlayerState.Tick` 实际上是广播 tick，不是最后确认的 `MoveInput.Tick`。
2. 客户端本地预测速度来自登录 UI / `LoginResponse.Speed`，服务端权威移动速度来自 `ServerAuthoritativeMovementConfiguration.MoveSpeed`，两者当前没有强约束保证一致。
3. 客户端本地预测按 `FixedUpdate + Time.fixedDeltaTime` 积分，服务端权威状态按外部传入的 `elapsed` 积分；一旦服务端主循环步长与客户端固定步长不一致，转向移动轨迹会持续偏离。
4. 客户端收到权威状态后当前仍采用“直接硬回写位置/旋转/速度，再重放”的纠正策略；当对账 tick 或积分条件不一致时，会直接表现为频繁拉扯和可见抖动。

### 修补目标

- 让客户端 reconciliation 使用明确的“已确认移动输入 tick”，而不是复用广播 tick。
- 统一客户端预测参数与服务端权威参数，至少保证移动速度、转向速度、tick 语义一致。
- 明确服务端 authoritative movement update cadence，避免客户端与服务端长期运行在不同积分节拍上。
- 在完成语义统一后，再决定是否需要补充更平滑的本地纠正策略，而不是先靠插值掩盖逻辑偏差。

### 实施计划

1. 先调整协议或状态表达：为客户端对账提供明确的 movement ack tick，禁止继续用 `PlayerState.Tick` 兼任广播序号和输入确认序号。
2. 修改 `ClientPredictionBuffer` / `MovementComponent` 的 reconciliation 逻辑，使 prediction buffer 按 ack tick 修剪，只重放真正未确认的输入。
3. 统一客户端与服务端移动参数来源，收敛 `LoginRequest.Speed` / `LoginResponse.Speed` 与 `ServerAuthoritativeMovementConfiguration.MoveSpeed` 的职责，避免双源真相。
4. 检查并修正服务端 authoritative movement 的生产主循环，确保 update cadence 可预测、可配置，并与客户端调试信息可对照。
5. 补充或更新回归测试，覆盖：
   - 广播 tick 与 ack tick 分离后的 reconciliation 行为
   - 客户端 / 服务端速度不一致时的保护或拒绝策略
   - 固定 cadence 下的本地预测与权威状态收敛
6. 在语义正确后，再评估是否需要把本地 controlled player 的硬纠正改成阈值纠正、渐进纠正或混合策略，以进一步降低视觉抖动。

### 修补优先级

- P0：tick 语义分离，修正 prediction buffer 错账问题。
- P0：统一移动速度来源，消除客户端预测与服务端权威结果的常量偏差。
- P1：统一或约束 authoritative movement update cadence。
- P2：在逻辑正确前提下优化 controlled player 的视觉纠正体验。

## 验收标准

- 上层只依赖统一的 `ITransport`。
- `KcpTransport` 是唯一可靠传输实现。
- 客户端与服务端都能正常建立 KCP 会话。
- 登录、心跳、输入、状态同步链路可正常跑通。
- 非主线程不再直接访问 Unity 对象。
- 会话超时、断线、重连有明确状态与日志。
- 高频移动同步在丢包 / 抖动场景下仍可用。
