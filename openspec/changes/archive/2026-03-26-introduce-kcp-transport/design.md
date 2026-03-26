## Context

阶段一已经把消息层收敛到 `ITransport`，当前 `MessageManager` 只依赖 `Send`、`SendTo`、`SendToAll`、`OnReceive`、`StartAsync` 和 `Stop`。运行时实现仍然是 `ReliableUdpTransport`，但它现在只是一个基于 `UdpClient` 的纯 UDP 收发器，并没有 KCP 会话、重传调度或完整消息重组能力。`NetworkManager` 仍然直接实例化这个实现，因此阶段二需要在不破坏上层消息封包逻辑的前提下，引入一个真正的 `KcpTransport`。

当前代码还存在两个约束。第一，`MessageManager` 收到 `OnReceive` 后会立刻解析 `Envelope` 并调用业务 handler，因此阶段二必须保证回调上来的已经是完整业务消息，而不是原始 UDP 数据报。第二，项目仓库里还没有可见的 KCP 传输实现代码，设计需要把 KCP 依赖和装配点描述清楚，同时避免把主线程派发、QoS 分流或旧类清理这些后续阶段内容混入本次改动。

## Goals / Non-Goals

**Goals:**
- 新增独立的 `KcpTransport` 实现 `ITransport`，保持现有消息层接口和 `MessageManager` 使用方式不变。
- 在客户端维护单一默认 `KcpSession`，在服务端按远端地址维护独立 `KcpSession`，使每个连接具备独立的 KCP 状态。
- 通过后台接收循环和更新循环驱动 `Kcp.Input`、`Kcp.Update`/`Check`、`Kcp.Recv`，并只在收到完整业务消息后触发 `OnReceive`。
- 将默认网络入口切换到 `KcpTransport`，并增加覆盖会话路由、完整消息交付和停止清理的编辑器测试。

**Non-Goals:**
- 删除 `ReliableUdpTransport`、`Packet.cs` 或阶段三里的旧可靠 UDP 清理工作。
- 将消息处理切换到 Unity 主线程队列。
- 为不同消息类型拆分 QoS 通道，或新增裸 UDP 的并行同步链路。
- 引入完整的连接重连、登录态状态机或新的业务握手协议。

## Decisions

### 1. 以新类 `KcpTransport` 落地，而不是继续修改 `ReliableUdpTransport`

新增 `Assets/Scripts/Network/NetworkTransport/KcpTransport.cs`，保留 `ReliableUdpTransport` 作为阶段一兼容实现，避免“纯 UDP 兼容类”和“KCP 会话传输类”在同一文件里继续叠加职责。这样阶段二可以单独验证 KCP 行为，阶段三再决定是否完全移除旧实现。

备选方案是在 `ReliableUdpTransport` 上继续加入 KCP 分支，但那会让命名、日志和生命周期控制再次变得模糊，不利于后续清理。

### 2. 用内部 `KcpSession` 封装每个远端的 KCP 状态

新增内部会话对象，至少保存以下状态：
- `IPEndPoint RemoteEndPoint`
- `uint Conv`
- KCP 实例
- `DateTime LastActivityUtc`
- 连接是否仍有效

客户端模式在构造时确定默认远端，并在 `StartAsync` 时创建默认会话；`Send(byte[])` 永远写入该默认会话。服务端模式按远端地址查找或创建会话，`SendTo(byte[], IPEndPoint)` 写入指定会话，`SendToAll(byte[])` 遍历当前有效会话逐个写入。为避免阶段二扩散到新的握手协议，本次使用统一配置的 `conv` 默认值；服务端仍以内网端点隔离不同连接，后续如需协商 `conv` 再通过新协议扩展。

备选方案是直接把 `Kcp` 对象散落在 `KcpTransport` 字典中而不包装会话对象，但这样会让活动时间、清理策略和发送入口分散在多个代码路径中。

### 3. 分离 UDP 接收循环和 KCP 更新循环

`KcpTransport` 维护两个后台任务：
- 接收循环：阻塞读取 `UdpClient.ReceiveAsync()`，按远端定位会话，调用 `session.Kcp.Input(...)`，随后持续从 `Kcp.Recv` 拉取完整业务消息，并以原始远端地址触发 `OnReceive`。
- 更新循环：周期性遍历活动会话，依据 `Kcp.Check`/`Kcp.Update` 推进重传、确认和 flush，确保即使没有新的入站 UDP 包，KCP 仍能推进超时与重发。

这种设计满足 KCP 对周期驱动的要求，同时将“收到原始 UDP 包”和“交付完整业务消息”明确分成两个层次。备选方案是仅在发送或收包时调用 `Update`，但那会让空闲期的重发和 flush 依赖外部流量，增加延迟和丢包恢复风险。

### 4. 保持 `ITransport` 契约稳定，仅替换默认实现

阶段二不扩展 `ITransport` 接口，也不要求 `MessageManager` 感知 KCP。`NetworkManager` 的改动限制为把 `new ReliableUdpTransport(...)` 替换成 `new KcpTransport(...)`，其余封包与 handler 注册逻辑保持不变。这样可以让编辑器测试继续围绕既有接口编写，并把接口扩展留给未来真正需要 `OnConnected`/`OnDisconnected`/`OnError` 的阶段。

备选方案是同步在本次 change 中扩展连接事件接口，但这会带来更多上层重构，不属于阶段二的最小交付面。

### 5. 通过可控测试桩验证 KCP 完整消息交付

测试重点放在三个方面：
- 客户端默认会话的 `Send` 走向配置远端。
- 服务端对多个远端维持独立会话，广播不会混淆会话状态。
- `OnReceive` 只在完整业务消息从 `Kcp.Recv` 取出后触发。

如果仓库内已有可引用的 KCP 程序集，编辑器测试可直接驱动真实 `KcpTransport`；如果当前工程缺失程序集引用，则先补齐插件接入，再用回环端口或可替代的 KCP 适配层完成测试。

## Risks / Trade-offs

- [KCP 程序集在仓库中不可见] → 在实现前确认插件来源与 asmdef 引用方式；若缺失，则先把依赖接入作为首个实现任务。
- [统一 `conv` 简化了阶段二，但不覆盖未来协商场景] → 设计中保留 `KcpSession.Conv` 和会话键扩展点，后续可在不推翻 `KcpTransport` 的前提下加入协商协议。
- [后台更新循环会引入额外线程与定时负担] → 将更新频率集中在 transport 内部，并确保 `Stop()` 能取消循环、关闭 socket、清理会话。
- [消息层仍在网络线程里执行业务 handler] → 本次只保证交付的是完整消息，线程切换问题留待后续阶段专门处理。

## Migration Plan

1. 接入或确认 KCP C# 依赖在 Unity 工程中可用。
2. 新增 `KcpTransport` 与内部 `KcpSession`，完成客户端和服务端的会话创建、发送、接收与更新循环。
3. 将 `NetworkManager` 默认实现切换到 `KcpTransport`，保持 `MessageManager` 和消息类型不变。
4. 增加编辑器测试，覆盖默认发送、服务端会话路由、完整消息交付和停止清理。
5. 如果集成验证失败，可临时切回 `ReliableUdpTransport` 入口，不影响 `ITransport` 和消息层接口。

## Open Questions

- 项目最终使用哪一个 KCP C# 实现，以及它在 Unity 中的程序集引用方式是什么？
- 阶段二是否需要立即加入空闲会话超时回收，还是只在 `Stop()` 时统一清理即可？
- 服务端是否已经有固定的监听入口需要同步替换为 `KcpTransport`，还是当前变更只覆盖客户端入口与共享 transport 代码？
