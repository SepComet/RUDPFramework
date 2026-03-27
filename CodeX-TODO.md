# KCP 网络底层调整 TODO

## 当前阶段状态

- 阶段 1 已完成：`ITransport` 已统一为 `Send` / `SendTo` / `SendToAll` / `OnReceive` / `StartAsync` / `Stop` 这一套稳定接口。
- 阶段 2 已完成：`KcpTransport` 已落地，`NetworkManager` 默认使用 KCP 作为运行时可靠传输实现，相关编辑器测试已经覆盖默认会话、多远端隔离、广播与停止清理。
- 阶段 3 已完成：遗留的 `ReliableUdpTransport` 兼容入口已经移除，项目中不再保留第二个“可靠 UDP”实现名义。
- 阶段 4 已完成：网络线程与 Unity 主线程之间已经建立显式分发边界，传输回调不再直接执行业务 handler。
- 阶段 5 及以后尚未开始：连接生命周期、QoS/同步优化、监控指标仍然是后续工作。

## 当前真实现状

- 当前可靠传输链路已经是：`NetworkManager -> SharedNetworkRuntime -> MessageManager -> MainThreadNetworkDispatcher -> ITransport -> KcpTransport`。
- 当前客户端与服务端共用的网络基础设施已经具备：
  - 共享的 `ITransport` / `KcpTransport`
  - 共享的 `MessageManager`
  - 共享的 `SharedNetworkRuntime`
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
- `MainThreadNetworkDispatcher` 负责：
  - 维护线程安全 FIFO 队列
  - 在 Unity 主线程 drain 队列并执行 handler
- `ServerNetworkHost` 当前可以作为非 Unity 宿主复用同一套网络核心，并使用非主线程 dispatcher 策略
- `NetworkManager` 负责：
  - 在 `Update()` 中定期 drain 网络消息
  - 在主线程上触发游戏对象修改与 UI 相关逻辑
- 当前业务链路仍然包括：
  - 登录 / 登出
  - 心跳 / 对时
  - `PlayerInput` 上行
  - `PlayerState` 下行
  - 本地预测 / 服务器校正
- 当前尚未完成的关键架构问题：
  - 连接成功、登录成功、心跳超时、重连等状态尚未完全分层
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

## 后续阶段

### 阶段 5：连接与心跳改造

1. 明确“连接成功”和“登录成功”是两个不同状态。
2. 心跳只承担：
   - 存活检测
   - RTT / 时间同步
3. 会话超时和断线重连逻辑放在 session manager，而不是业务消息处理里。

交付标准：

- 断线、超时、登录失败、重连等状态可以被明确区分。

### 阶段 6：同步策略优化

1. 重新评估 `PlayerInput` 是否必须严格可靠。
2. 重新评估 `PlayerState` 是否应使用可靠有序流。
3. 调整客户端预测、回滚、纠正策略。
4. 把对时逻辑从 `_sendInterval` 漂移中拆出来。

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

## 验收标准

- 上层只依赖统一的 `ITransport`。
- `KcpTransport` 是唯一可靠传输实现。
- 客户端与服务端都能正常建立 KCP 会话。
- 登录、心跳、输入、状态同步链路可正常跑通。
- 非主线程不再直接访问 Unity 对象。
- 会话超时、断线、重连有明确状态与日志。
- 高频移动同步在丢包 / 抖动场景下仍可用。
