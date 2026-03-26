# KCP 网络底层调整 TODO

## 目标

将当前项目的网络底层从“自写可靠 UDP + ACK/重传/会话”调整为“UDP 承载 KCP + 明确的传输层/会话层/消息层/同步层分层”，避免职责重叠，并为后续的同步优化、重连、监控打基础。

## 当前现状

- `Assets/Scripts/Network/NetworkTransport/ReliableUdpTransport.cs` 已经引入 `Kcp-CSharp.dll`，但主体逻辑仍然是自定义可靠 UDP。
- 当前传输层仍保留以下逻辑：
  - 自定义 `Packet`
  - 自定义 ACK
  - 自定义重传
  - 自定义超时会话清理
  - 自定义顺序交付
- `NetworkManager -> MessageManager -> ITransport` 的抽象还没有完全收口，接口和实现存在不一致。
- 当前业务链路不是纯 RPC，而是：
  - 登录 / 登出
  - 心跳 / 对时
  - `PlayerInput` 上行
  - `PlayerState` 下行
  - 本地预测 / 服务器校正

## 必须调整的内容

### 1. 收口传输层接口

统一 `ITransport` 的职责，避免上层绕过抽象调用不存在的方法。

建议接口至少包含：

- `StartAsync()`
- `Stop()`
- `Connect(...)` 或客户端构造时明确默认远端
- `Send(byte[] data)`
- `SendTo(byte[] data, IPEndPoint target)`，仅服务端或特殊场景需要
- `SendToAll(byte[] data)`，仅服务端广播需要
- `OnReceive`
- `OnConnected`
- `OnDisconnected`
- `OnError`

当前要处理的问题：

- `MessageManager` 调用了 `transport.Send(...)`，但 `ITransport` 中没有定义该接口。
- `ReliableUdpTransport` 当前的 `SendTo(Packet, IPEndPoint)` 与接口 `SendTo(byte[], IPEndPoint)` 不一致。

### 2. 用 KCP 替代自定义可靠 UDP

KCP 接入后，以下能力不应继续由项目侧重复实现：

- ACK 管理
- 重传调度
- 收发序号维护
- 有序交付
- 滑动窗口

因此需要删除或重构以下内容：

- `Assets/Scripts/Network/NetworkTransport/Packet.cs`
- `Assets/Scripts/Network/NetworkTransport/ClientSession.cs` 中基于自定义 seq/ack 的逻辑
- `Assets/Scripts/Network/NetworkTransport/ReliableUdpTransport.cs` 中的：
  - `CheckRetransmit`
  - `HandleAckPacket`
  - 自定义 `PendingAcks`
  - 自定义重复包 / 乱序包处理
  - 自定义可靠性定时器

### 3. 重建会话层

KCP 模式下需要清晰区分：

- UDP Socket
- KCP Session
- 业务连接状态

建议设计：

- 客户端：
  - 单一默认远端
  - 单一 `KcpSession`
- 服务端：
  - 按 `IPEndPoint + conv` 管理多个 `KcpSession`
  - 支持会话建立、心跳超时、断线清理

会话层至少需要管理：

- `conv`
- 远端地址
- 最后活跃时间
- KCP 实例
- 连接状态
- 断开原因

### 4. 将网络线程与 Unity 主线程解耦

当前 `MessageManager.OnTransportReceiveAsync(...)` 直接进入业务 handler，而后续 handler 会继续访问：

- `MasterManager`
- `Player`
- `GameObject`
- `UI`

这些逻辑不应该直接在网络接收线程执行。

需要改为：

1. 网络线程收包
2. 解析最小必要信息
3. 投递到线程安全队列
4. 在 Unity `Update()` 中统一分发到业务层

建议新增：

- `MainThreadDispatcher`
- 或 `ConcurrentQueue<Action>`
- 或 `ConcurrentQueue<ReceivedEnvelope>`

### 5. 重新划分消息 QoS

当前所有消息看起来都走同一种可靠传输语义，这对高频同步不合理。

建议至少拆成两类：

- 强可靠消息
  - 登录
  - 登出
  - 房间管理
  - 关键系统命令
- 高频同步消息
  - `PlayerInput`
  - `PlayerState`
  - 以后可能的快照、插值状态、非关键位置更新

需要明确一个原则：

- 如果 `PlayerState` 继续走可靠有序流，旧包阻塞会放大延迟。
- 如果 `PlayerInput` 全部严格可靠发送，也可能产生输入堆积。

这部分要结合项目玩法决定：

- 方案 A：全部先走 KCP，先完成架构收口，再做同步优化
- 方案 B：命令消息走 KCP，同步消息走裸 UDP / 另一条轻量通道

短期建议先用方案 A 收口，后续再细分。

### 6. 重构连接生命周期

需要把“传输连接”与“业务登录状态”分开。

建议生命周期：

1. 创建 UDP Socket
2. 初始化 KCP
3. 连接服务器 / 建立默认会话
4. 开始收包循环和 KCP Update
5. 发送 `LoginRequest`
6. 收到 `LoginResponse` 后进入已登录状态
7. 开始心跳与超时检测
8. 超时或异常时触发断线回调
9. 按需重连

不要再把“收到登录响应才知道默认服务器端点”这种逻辑和连接过程混在一起。

## 建议同步调整的内容

### 1. 对时与发送频率分离

当前 `MovementComponent` 通过修改 `_sendInterval` 来追赶服务器 Tick，这会把：

- 时钟校正
- 发包频率
- 同步稳定性

绑在一起。

建议改为：

- 固定输入发送频率
- 单独维护客户端与服务端 Tick 偏移
- 在校正阶段使用 replay / reconcile，而不是直接依赖发包间隔漂移

### 2. 增加 KCP 参数配置入口

建议支持配置以下参数：

- `NoDelay`
- `Interval`
- `Resend`
- `NC`
- `SndWnd`
- `RcvWnd`
- `MTU`
- `DeadLink`

建议做法：

- 新增 `KcpTransportConfig`
- 客户端和服务端分别可配置
- 支持 Inspector 或 ScriptableObject 配置

### 3. 增加网络观测指标

至少需要输出：

- RTT
- 重传次数
- 待发送队列长度
- 待接收队列长度
- 会话数量
- 最后活跃时间
- 超时断线原因

后续排查“卡顿、抖动、延迟尖刺、重连失败”时会用到。

### 4. 明确服务端广播策略

当前 `SendToAll` 是直接遍历会话广播。接入 KCP 后要明确：

- 广播是否逐会话独立写入
- 广播时是否允许慢连接拖累整体发送
- 广播消息是否需要按类型分优先级

## 推荐实施步骤

### 阶段 1：先把抽象收口

1. 调整 `ITransport`，补齐上层真正需要的发送与连接接口。
2. 让 `MessageManager` 只依赖 `ITransport` 暴露的方法，不再假设具体实现细节。
3. 修复当前 `ReliableUdpTransport` 与 `ITransport` 的方法签名不一致问题。
4. 让网络层先达到“接口一致、结构可替换”的状态。

交付标准：

- 上层不再直接依赖某个具体 Transport 的额外方法。
- 业务层不关心底层是 UDP 还是 KCP。

### 阶段 2：引入 `KcpTransport`

1. 新建 `KcpTransport`，不要在旧的 `ReliableUdpTransport` 上继续打补丁。
2. 用 `UdpClient` 只负责收发原始 UDP 数据报。
3. 每个连接维护一个 `KcpSession`。
4. UDP 收包后先交给对应的 KCP 实例 `Input`。
5. 周期性驱动 KCP `Update` / `Check`。
6. 从 KCP `Recv` 中取出完整业务消息后再触发 `OnReceive`。

交付标准：

- 传输层不再维护自定义 ACK/重传逻辑。
- KCP 可以完成完整收发。

### 阶段 3：移除旧可靠 UDP 结构

1. 删除或废弃 `Packet.cs`。
2. 删除或废弃旧 `ClientSession` 中基于 seq/ack 的缓存和重传代码。
3. 删除 `ReliableUdpTransport` 中的：
   - retransmit timer
   - ack handler
   - packet seq 交付逻辑
4. 保留必要的会话容器与连接生命周期管理。

交付标准：

- 项目中不再同时存在“两套可靠性机制”。

### 阶段 4：主线程分发改造

1. 新增线程安全接收队列。
2. 网络线程只负责：
   - 收包
   - KCP 输入输出
   - 基础错误处理
3. Unity 主线程负责：
   - 消息分发
   - 游戏对象修改
   - UI 更新

交付标准：

- 网络消息不会直接在非主线程操作 Unity 对象。

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

建议新增或重命名以下模块：

- `Assets/Scripts/Network/NetworkTransport/KcpTransport.cs`
- `Assets/Scripts/Network/NetworkTransport/KcpSession.cs`
- `Assets/Scripts/Network/NetworkTransport/KcpTransportConfig.cs`
- `Assets/Scripts/Network/NetworkApplication/MainThreadNetworkDispatcher.cs`

建议废弃或重构：

- `Assets/Scripts/Network/NetworkTransport/ReliableUdpTransport.cs`
- `Assets/Scripts/Network/NetworkTransport/ClientSession.cs`
- `Assets/Scripts/Network/NetworkTransport/Packet.cs`

## 验收标准

- 上层只依赖统一的 `ITransport`。
- 传输层不再重复实现 ACK / 重传 / 顺序控制。
- 客户端与服务端都能正常建立 KCP 会话。
- 登录、心跳、输入、状态同步链路可正常跑通。
- 非主线程不再直接访问 Unity 对象。
- 会话超时、断线、重连有明确状态与日志。
- 高频移动同步在丢包 / 抖动场景下仍可用。

## 备注

- 当前最不建议的做法，是在现有 `ReliableUdpTransport` 上继续叠加更多 KCP 相关判断。这样会让自定义可靠 UDP 和 KCP 职责长期重叠。
- 正确方向是：先抽象收口，再用新的 `KcpTransport` 替换旧实现。
