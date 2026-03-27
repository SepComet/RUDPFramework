# KCP 网络底层调整 TODO

## 当前阶段状态

- 阶段 1 已完成：`ITransport` 已统一为 `Send` / `SendTo` / `SendToAll` / `OnReceive` / `StartAsync` / `Stop` 这一套稳定接口。
- 阶段 2 已完成：`KcpTransport` 已落地，`NetworkManager` 默认使用 KCP 作为运行时可靠传输实现，相关编辑器测试已经覆盖默认会话、多远端隔离、广播与停止清理。
- 阶段 3 已完成：遗留的 `ReliableUdpTransport` 兼容入口已经移除，项目中不再保留第二个“可靠 UDP”实现名义。
- 阶段 4 已完成：网络线程与 Unity 主线程之间已经建立显式分发边界，传输回调不再直接执行业务 handler。
- 阶段 5 已完成：共享会话生命周期、心跳职责边界、超时/重连状态已经从业务消息处理里分层出来，并扩展到服务端多会话管理。
- 阶段 6 已完成：高频同步已经拆分为显式 delivery policy / sync lane / latest-wins sequencing / 独立 clock sync 状态，相关编辑器测试已经覆盖路由、过期包丢弃、预测缓冲修剪与多会话隔离。
- 阶段 7 已开始但未完成：传输 metrics 已补充可读摘要、摘要文本报告和调试开关，但会话级指标、聚合视图和更完整的故障诊断仍需继续完善。

## 当前真实现状

- 当前可靠传输链路已经是：`NetworkManager -> SharedNetworkRuntime -> MessageManager -> MainThreadNetworkDispatcher -> ITransport -> KcpTransport`。
- 当前客户端与服务端共用的网络基础设施已经具备：
  - 共享的 `ITransport` / `KcpTransport`
  - 共享的 `MessageManager`
  - 共享的 `SharedNetworkRuntime`
  - 共享的 `SessionManager` / `MultiSessionManager` / `ConnectionState`
  - 由宿主注入的 dispatcher 策略，而不是在消息层内硬编码 Unity 主线程语义
- `KcpTransport` 负责：
  - 客户端默认会话
  - 服务端按远端地址隔离会话
  - KCP `Input` / `Update` / `Recv`
  - 只在拿到完整业务消息后触发 `OnReceive`
- `MessageManager` 负责：
  - 解析 `Envelope`
  - 根据 `MessageType` 查找 handler
  - 通过 `IMessageDeliveryPolicyResolver` 将控制面消息路由到可靠有序 lane，并将 `PlayerInput` / `PlayerState` 路由到高频同步 lane
  - 对 `PlayerInput` / `PlayerState` 执行基于 tick 的 latest-wins 过滤，丢弃过期同步包
  - 通过宿主注入的 dispatcher 执行消息分发，而不是在收包线程直接执行 handler
- `SessionManager` 负责：
  - 维护 `Disconnected` / `TransportConnected` / `LoginPending` / `LoggedIn` / `LoginFailed` / `TimedOut` / `ReconnectPending` / `Reconnecting` 状态
  - 管理心跳发送窗口、心跳超时和重连调度
  - 记录 RTT 与最近 liveness 时间，但不再拥有服务器时钟样本
- `ClockSyncState` 负责：
  - 独立记录最近接受的服务器 tick 样本
  - 接收心跳响应与权威 `PlayerState` 的时钟样本
  - 以独立同步策略状态的形式供客户端预测/纠正与服务端多会话观察读取
- `MultiSessionManager` 负责：
  - 按远端 `IPEndPoint` 维护多份 `SessionManager`
  - 按远端维护独立 `ClockSyncState`
  - 让服务端按每个远端独立观察登录、超时、断线、重连状态
  - 提供按远端查询、枚举、移除会话的共享入口
- `MainThreadNetworkDispatcher` 负责：
  - 维护线程安全 FIFO 队列
  - 在 Unity 主线程 drain 队列并执行 handler
- `ServerNetworkHost` 当前可以作为非 Unity 宿主复用同一套网络核心，并通过 `MultiSessionManager` 暴露与客户端一致的生命周期状态词汇
- `NetworkManager` 负责：
  - 在 `Update()` 中定期 drain 网络消息并驱动 `SessionManager` 超时评估
  - 在主线程上触发游戏对象修改与 UI 相关逻辑
  - 仅在会话已登录且心跳到期时发送心跳
- 当前业务链路仍然包括：
  - 登录 / 登出
  - 心跳 / 对时
  - `PlayerInput` 上行
  - `PlayerState` 下行
  - 本地预测 / 服务器校正
- 当前已完成但仍需后续迭代优化的部分：
  - 高频同步消息已经具备 policy/lane 拆分，但第一版 sync lane 仍通过共享抽象接入，后续仍可替换为更激进的底层实现
  - 网络观测指标还不完整

## 已完成阶段回顾

### 阶段 3：移除旧可靠 UDP 结构

已完成结果：

- 项目中不存在可直接实例化的 `ReliableUdpTransport`。
- 默认运行时可靠传输路径仍然是 `KcpTransport`。
- 文档不再描述“项目里还保留一套旧 reliable UDP 实现”。

### 阶段 4：主线程分发改造

已完成结果：

1. 已新增线程安全接收队列：`Assets/Scripts/Network/NetworkApplication/MainThreadNetworkDispatcher.cs`
2. 网络线程当前只负责：
   - 收包
   - KCP 输入输出
   - 基础错误处理
   - 将有效业务消息入队
3. Unity 主线程当前负责：
   - 消息分发
   - 游戏对象修改
   - UI 更新

交付结论：

- 网络消息不会直接在非主线程操作 Unity 对象。

### 阶段 5：连接与心跳改造

已完成结果：

1. 已区分 `TransportConnected` 和 `LoggedIn` 两种状态，登录成功不再隐含为“网络已连接”的别名。
2. 心跳当前只承担：
   - 存活检测
   - RTT 统计
   - 时间同步
3. 会话超时、登录失败、重连调度当前由共享 `SessionManager` 管理，而不是散落在业务 handler 里。
4. 服务端当前通过 `MultiSessionManager` 按每个远端地址独立管理 `SessionManager`，不再把所有远端压成一个 runtime 级状态。

交付结论：

- 断线、超时、登录失败、重连等状态当前可以被明确区分和测试。
- 服务端当前也可以独立观察玩家 A / B / C 各自的生命周期状态，而不是只看到一个总状态。

## 后续阶段

### 阶段 6：同步策略优化

已完成结果：

1. 已新增共享 `DeliveryPolicy`、`IMessageDeliveryPolicyResolver`、`TransportMessageLane`，让宿主显式组合可靠控制面与高频同步 lane。
2. `PlayerInput` / `PlayerState` 当前默认走 `HighFrequencySync` policy，登录、登出、心跳等控制流继续走可靠 KCP。
3. 已新增 `SyncSequenceTracker`，对 `PlayerInput` / `PlayerState` 按 tick 执行 latest-wins 过滤，丢弃过期同步包。
4. 已新增 `ClockSyncState`，把服务器 tick 样本所有权从 `SessionManager` 挪出，并让心跳响应与权威状态都能更新该状态。
5. 客户端当前通过 `ClientPredictionBuffer` 在收到权威状态后修剪已确认输入，并只重放更新的待确认输入。
6. 编辑器测试当前已覆盖：
   - delivery policy 路由
   - stale packet rejection
   - clock sync forwarding
   - prediction buffer pruning
   - server multi-session sync isolation

交付结论：

- 高频同步场景下已经不再依赖可靠有序交付来保证 `PlayerInput` / `PlayerState` 的处理顺序。
- 心跳生命周期与时钟同步当前已经分离，各自的职责边界可以独立演进。

### 阶段 7：监控与调试工具补齐

当前已完成：

1. `DefaultTransportMetricsModule` 除 JSON 外，额外输出更易读的 `.summary.txt` 摘要文件。
2. metrics JSON 现在包含 `ReadableSummary` 字段，直接给出 headline、session、traffic、error 和 top peer 提示。
3. 已新增 metrics 调试选项，可独立控制：
   - 是否写 JSON 报告
   - 是否写文本摘要
   - 是否输出控制台摘要
4. 控制台摘要当前会打印更易读的会话、流量、错误和热点 peer 信息，而不是只保留一行紧凑指标。
5. KCP metrics 当前已经额外输出会话级诊断字段，包括：
   - `rx_srtt` / `rx_rto` 派生的 RTT 与重传超时
   - `ikcp_waitsnd`、发送/接收队列与缓冲深度
   - 基于 KCP `snd_buf` 的在途重传段统计与累计重传观察值
   - 每个 peer 的会话生命周期状态聚合
6. 共享会话层当前也会把单会话 / 多会话状态快照写入 metrics，包括：
   - `SharedNetworkRuntime` 的登录、心跳、失败、重连调度状态
   - `ServerNetworkHost` 按远端 peer 聚合的 `ConnectionState`
   - 可发送心跳、最近 RTT、下一次重连时间、最近服务器 tick
7. metrics 当前会额外落一份 `.diagnosis.txt` 中文诊断文件，直接输出：
   - 整体稳定性判断
   - 高延迟 / 发送堆积 / 重传 / 重连风险提示
   - 共享会话状态总览
   - 热点 peer 的排障线索

仍待完成：

1. 提供更适合直接排障的聚合视图或查看工具，而不是只落文件。
2. 继续补更多跨运行阶段的历史视图，例如连续超时、重连抖动、会话恢复成功率。
3. 如果后续要做运维化使用，再补按时间窗口聚合的趋势指标，而不只看单次运行快照。

交付标准：

- 网络问题可以通过日志和指标快速定位，且常见问题不需要先手动阅读原始 JSON 结构。

## 推荐新增的结构

建议后续继续补齐以下模块：

- `Assets/Scripts/Network/NetworkTransport/KcpTransportConfig.cs`
- `Assets/Scripts/Network/NetworkApplication/SessionMetrics.cs`
- `Assets/Scripts/Network/NetworkApplication/MultiSessionManager.cs`

## 验收标准

- 上层只依赖统一的 `ITransport`。
- `KcpTransport` 是唯一可靠传输实现。
- 客户端与服务端都能正常建立 KCP 会话。
- 登录、心跳、输入、状态同步链路可正常跑通。
- 非主线程不再直接访问 Unity 对象。
- 会话超时、断线、重连有明确状态与日志。
- 高频移动同步在丢包 / 抖动场景下仍可用。
