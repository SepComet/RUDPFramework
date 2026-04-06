## Context

当前客户端本地预测和服务端权威同步存在两个 P0 级错位。第一，客户端把 `PlayerState.Tick` 当成“服务端已确认到哪个 `MoveInput.Tick`”来修剪 prediction buffer，但服务端实际把它作为广播快照序号生成，导致客户端错误移除或错误重放输入。第二，客户端移动速度来源于 UI / 登录返回链路，服务端权威速度来源于 `ServerAuthoritativeMovementConfiguration`，两边没有统一的真相来源。

这次改动跨越 protobuf 契约、共享网络同步语义、客户端本地预测初始化、服务端登录后移动 bootstrap，以及回归测试，因此需要显式设计文档先固定决策。

## Goals / Non-Goals

**Goals:**
- 明确分离权威快照 tick 和已确认移动输入 tick。
- 让客户端 reconciliation 只依赖显式 ack move tick。
- 建立服务器确认的移动参数启动流程，让客户端预测参数与服务端权威参数共享同一真相来源。
- 用回归测试保护上述行为，避免再次回到“一个字段承载两种语义”的状态。

**Non-Goals:**
- 不在本次 P0 中解决本地 controlled player 的视觉平滑策略。
- 不在本次 P0 中重构完整的服务端 movement cadence 管理，只要求现有语义能正确对账。
- 不扩展新的移动玩法或额外状态字段，除非它们是承载 ack tick / 权威参数所必需的最小改动。

## Decisions

### 1. `PlayerState.Tick` 保留为权威快照序号，新增显式 `AcknowledgedMoveTick`

`PlayerState.Tick` 现在已经被客户端和服务端用作“最新快照 / 最新同步状态”的序号。直接把它改成 ack tick 会让 remote interpolation、stale rejection、日志语义全部混乱。更稳妥的做法是保留 `PlayerState.Tick` 作为快照序号，并新增 `AcknowledgedMoveTick` 用于本地 controlled player reconciliation。

备选方案：
- 把 `PlayerState.Tick` 改成 ack tick，并额外引入 snapshot tick。这个方案会让已有 stale rejection 和 remote snapshot buffer 全部迁移到新字段，破坏面更大。
- 继续复用单字段并靠注释区分。这个方案无法阻止后续实现再次误用，直接排除。

### 2. 服务器在生成每个 `PlayerState` 时回填该玩家最后接受的 `MoveInput.Tick`

ack tick 属于每个玩家独立的服务器权威状态，而不是整个广播循环共享状态。服务端应当从玩家的权威移动状态中读取 `LastAcceptedMoveTick`，并在构造该玩家 `PlayerState` 时填入 `AcknowledgedMoveTick`。这样客户端拿到同一条快照时，既能用 `Tick` 做 stale rejection / 插值排序，也能用 `AcknowledgedMoveTick` 做 prediction buffer 修剪。

备选方案：
- 发送独立 ack 消息。这样会增加协议复杂度和时序耦合，本次只需要在已有 `PlayerState` 中补充字段即可满足需求。

### 3. 客户端本地预测参数必须在登录成功后切换到服务器确认值

本地预测只要继续使用 UI 本地速度，而服务端继续使用配置速度，就算 ack tick 语义正确也会不断被回拉。P0 需要把移动参数所有权收回到服务器，客户端只允许在登录前临时持有候选值，登录成功后必须切换到服务器确认的移动参数后再继续长期预测。

备选方案：
- 彻底删除客户端可配置速度。这个方向可行，但会扩大 MVP 工作面；当前先保留输入渠道，只是不再允许它绕过服务器确认值成为长期预测真相。
- 仅在代码里假定两边常量相等。这个方案没有可验证的契约，不能接受。

### 4. 回归测试以“语义分离”而不是“视觉无抖动”作为 P0 验收目标

P0 的核心是让协议与 reconciliation 语义正确。测试应精确验证：
- 服务器广播 tick 增长时，ack tick 仍保持玩家最后确认输入值。
- 客户端 reconciliation 只修剪 `<= AcknowledgedMoveTick` 的输入。
- 客户端在登录成功后使用服务器确认的移动参数建立或刷新本地预测配置。

视觉平滑与误差阈值可以在后续 P2 再处理，不应混入本次验收。

## Risks / Trade-offs

- [风险] protobuf 字段变更会影响生成代码与现有消息构造路径 -> 缓解：把改动限制在 `PlayerState` 最小增量字段，并补齐协议层回归测试。
- [风险] 客户端仍可能在登录前使用本地默认速度开始预测 -> 缓解：在 bootstrap 规范中明确“长期预测必须切换到服务器确认参数”，实现阶段为未确认参数增加显式初始化路径。
- [风险] 某些现有测试默认把 `PlayerState.Tick` 视作 ack tick -> 缓解：把这些测试改成分别断言 snapshot tick 与 `AcknowledgedMoveTick`。
- [风险] 只修复语义不修复 cadence 后，极端情况下仍会看到少量位置纠正 -> 缓解：将 cadence 统一保留在后续 P1，并确保本次不会再由错账语义导致持续拉扯。

## Migration Plan

1. 更新 OpenSpec 契约与任务清单，固定 `PlayerState` 双 tick 语义与权威参数 bootstrap 规则。
2. 实现协议字段与服务端状态生成逻辑，确保服务器能填充 `AcknowledgedMoveTick`。
3. 更新客户端 reconciliation 与本地预测初始化逻辑。
4. 补齐回归测试后运行 `dotnet test Network.EditMode.Tests.csproj --no-build -v minimal`。
5. 如果发现客户端初始化阶段需要兼容旧字段，使用默认值路径临时兜底，但不改变最终权威参数所有权。

## Open Questions

- 服务器确认的移动参数是否只需要 `MoveSpeed`，还是要同时把旋转速度也纳入同一 bootstrap 契约。如果现有客户端和服务端旋转速度并非单一常量来源，实现时应一并纳入。
- 登录成功消息是否已经稳定承载移动参数；如果没有，是否需要通过初始 `PlayerState` 或其他启动消息承载该参数。该问题在实现前需要结合现有消息结构做最小改动决策。
