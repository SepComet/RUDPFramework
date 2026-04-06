## Why

客户端当前把 `PlayerState.Tick` 同时当作权威快照序号和已确认输入 tick 使用，而服务端广播的 `PlayerState.Tick` 实际上代表广播序号。这会让本地 prediction buffer 错误修剪与重放，持续制造可见抖动。与此同时，客户端本地预测速度与服务端权威速度来自不同来源，导致即使 tick 语义修正后，客户端轨迹仍可能系统性偏离。

## What Changes

- 为权威移动同步引入明确的已确认输入 tick 语义，禁止继续复用 `PlayerState.Tick` 同时表达广播序号和输入确认序号。
- 调整 `PlayerState` 消息契约，使权威快照同时携带快照 tick 与已确认移动输入 tick。
- 定义客户端权威移动参数启动能力，要求客户端本地预测使用服务器确认的移动参数，而不是独立 UI 本地值。
- 更新客户端 reconciliation 规则，使 prediction buffer 只按已确认移动输入 tick 修剪。
- 补充编辑模式回归覆盖，保护 ack tick / broadcast tick 分离以及权威移动参数启动流程。

## Capabilities

### New Capabilities
- `authoritative-movement-bootstrap`: 定义客户端在开始本地预测前如何接收并应用服务器确认的权威移动参数。

### Modified Capabilities
- `client-authoritative-player-state`: 本地 reconciliation 从按 `PlayerState.Tick` 对账改为按显式 ack move tick 对账。
- `network-gameplay-message-types`: `PlayerState` 消息契约新增显式已确认移动输入 tick 字段。
- `network-sync-strategy`: prediction history 修剪规则从快照 tick 改为 ack move tick。
- `server-authoritative-movement`: 服务器广播的权威状态同时暴露快照序号与最后确认的移动输入 tick。
- `gameplay-flow-regression-coverage`: 回归测试新增 ack/broadcast tick 分离与权威移动参数启动覆盖。

## Impact

- 影响共享协议与生成消息代码，包括 `PlayerState` 的字段契约。
- 影响客户端 `MovementComponent`、`ClientPredictionBuffer` 与本地预测初始化流程。
- 影响服务端权威移动协调器、登录成功后的移动参数建立、以及权威状态广播逻辑。
- 影响 edit-mode 网络回归测试与假传输端到端测试。
