# KCP 网络底层调整 TODO

## 当前阶段状态

- 阶段 1 已完成：`ITransport` 已统一为 `Send` / `SendTo` / `SendToAll` / `OnReceive` / `StartAsync` / `Stop` 这一套稳定接口。
- 阶段 2 已完成：`KcpTransport` 已落地，`NetworkManager` 默认使用 KCP 作为运行时可靠传输实现，相关编辑器测试已经覆盖默认会话、多远端隔离、广播与停止清理。
- 阶段 3 已完成：遗留的 `ReliableUdpTransport` 兼容入口已经移除，项目中不再保留第二个“可靠 UDP”实现名义。
- 阶段 4 已完成：网络线程与 Unity 主线程之间已经建立显式分发边界，传输回调不再直接执行业务 handler。
- 阶段 5 已完成：共享会话生命周期、心跳职责边界、超时/重连状态已经从业务消息处理里分层出来，并扩展到服务端多会话管理。
- 阶段 6 及以后尚未开始：QoS/同步优化、监控指标仍然是后续工作。

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
  - 通过宿主注入的 dispatcher 执行消息分发，而不是在收包线程直接执行 handler
- `SessionManager` 负责：
  - 维护 `Disconnected` / `TransportConnected` / `LoginPending` / `LoggedIn` / `LoginFailed` / `TimedOut` / `ReconnectPending` / `Reconnecting` 状态
  - 管理心跳发送窗口、心跳超时和重连调度
  - 记录 RTT、最近 liveness 时间和最近服务器时钟样本
- `MultiSessionManager` 负责：
  - 按远端 `IPEndPoint` 维护多份 `SessionManager`
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
- 当前尚未完成的关键架构问题：
  - 高频同步消息仍未做 QoS 拆分
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

1. 重新评估 `PlayerInput` 是否必须严格可靠。
2. 重新评估 `PlayerState` 是否应使用可靠有序流。
3. 调整客户端预测、回滚、纠正策略。
4. 把对时逻辑从 `SessionManager` 的心跳窗口里进一步拆分成独立同步策略（如有必要）。

交付标准：

- 高频同步场景下不会因为旧包阻塞导致位置明显滞后。

### 阶段 7：监控与调试工具补齐

1. 打印会话状态。
2. 输出 RTT、发送队列、丢包、重传等指标。
3. 提供调试开关，避免正式环境日志过多。

交付标准：

- 网络问题可以通过日志和指标定位。

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
