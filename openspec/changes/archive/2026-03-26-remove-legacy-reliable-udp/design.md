## Context

当前仓库已经完成阶段二的核心目标：`NetworkManager` 默认实例化 `KcpTransport`，`MessageManager` 仅依赖 `ITransport`，并且已有编辑器测试覆盖 KCP 的默认会话、多远端隔离、广播与停止清理行为。与 TODO 文档最初描述不同，仓库里已经没有旧的 ACK/重传/seq 实现残留在 `ReliableUdpTransport` 中，该类现状只是一个基于 `UdpClient` 的 plain UDP 收发器。

这让阶段三的真实问题从“拆掉旧可靠 UDP 算法”变成了“拆掉旧可靠 UDP 概念和错误入口”。如果继续保留 `ReliableUdpTransport` 这个名称，后续开发者会自然假定项目中仍然存在第二套可靠传输实现，导致连接状态、QoS 分流和后续主线程分发改造继续围绕错误前提展开。因此，本次设计重点是收紧传输层边界，而不是重新做一轮 KCP 集成。

## Goals / Non-Goals

**Goals:**
- 让 `KcpTransport` 成为项目内唯一的可靠 `ITransport` 实现，并在代码结构上消除对旧可靠 UDP 名称的依赖。
- 删除、退役或显式改名当前的 `ReliableUdpTransport` 兼容类，使其不再被误解为可靠传输实现。
- 保持 `ITransport`、`MessageManager` 和现有业务消息封包逻辑不变，避免阶段三重新扩散到消息层。
- 用测试和文档明确“可靠消息只走 KCP”这一边界，为阶段四后的连接、线程与同步优化提供稳定基线。

**Non-Goals:**
- 修改 `ITransport` 接口形状，或在本次变更中引入 `OnConnected`、`OnDisconnected`、`OnError` 等新事件。
- 处理主线程派发、会话超时、断线重连或心跳状态机。
- 为高频同步新增裸 UDP 并行通道；如果未来需要非可靠传输，本次只保留可扩展边界，不直接实现 QoS 分流。
- 变更 KCP 会话语义、`conv` 分配策略或既有 `KcpTransportTests` 已覆盖的阶段二行为。

## Decisions

### 1. 删除误导性的 `ReliableUdpTransport`，而不是继续保留兼容壳

当前 `ReliableUdpTransport` 不再提供任何可靠能力，继续保留它只会制造“项目中还有第二套可靠路径”的误解。阶段三应直接删除该类及其相关资产；如果后续确实需要裸 UDP 通道，应以明确的 `UdpTransport` 或其他非可靠命名重新引入，并在 capability 层单独建模。

备选方案是保留该类并加 `[Obsolete]` 标记。这个方案短期改动更小，但会长期留下错误命名和二义性，且 Unity 项目里 `Obsolete` 往往不足以阻止被继续引用，因此不作为首选。

### 2. 将“唯一可靠通道”写入 `kcp-transport` capability，而不是只作为实现细节

阶段三的核心价值在于建立新的架构边界：可靠消息只能通过 KCP 传输。这个约束会直接影响未来是否允许新增第二个可靠 transport、如何做 QoS 分流，以及如何理解登录/心跳/输入/状态链路。因此它需要进入 `openspec/specs/kcp-transport/spec.md` 的 delta，而不是只写在任务说明或代码注释里。

备选方案是新建一个独立 capability，例如 `transport-cleanup`。但本次没有新增对外能力，变化本质上是对现有 KCP 传输能力的边界补充，归并到 `kcp-transport` 更紧凑。

### 3. 仅保留稳定的 `ITransport` 抽象，不在阶段三暴露新的临时迁移接口

阶段三不会为了兼容旧类而引入工厂、别名接口或临时转发层。运行时代码已经通过 `ITransport` 与 `KcpTransport` 对接，说明迁移成本集中在删除遗留类与相关测试/文档，而不是上层调用点适配。保持接口不变有助于把本次改动约束在 transport 边界内完成。

备选方案是新增 transport factory，由 factory 负责决定 KCP 还是兼容 UDP。这会把一个已经完成切换的问题重新抽象化，没有当前收益。

### 4. 用测试验证“错误入口已消失”，而不重复验证阶段二已覆盖的 KCP 行为

阶段二已经有 `KcpTransportTests` 覆盖 KCP 可靠收发行为。阶段三新增或调整的测试应聚焦于：
- 运行时入口仍使用 `KcpTransport`
- 仓库中不再存在会被业务代码直接实例化的 `ReliableUdpTransport`
- 如保留非可靠 transport，新命名和语义与可靠链路明确区分

这样可以避免在同一 capability 上堆积重复测试，同时把测试成本投入到真正变化的边界上。

## Risks / Trade-offs

- [未来很快需要裸 UDP 高频同步通道] → 若需求出现，再以明确命名新增非可靠 transport，不复用 `ReliableUdpTransport` 这个遗留名称。
- [删除类后仍有隐藏引用未被搜索到] → 在实现任务中先全仓检索 `ReliableUdpTransport`，并用编译/测试确认没有残余引用。
- [TODO 文档与代码现实存在偏差] → 本次 proposal/design/spec 以当前代码为准，并在任务中同步更新相关文档表述，避免继续误导后续阶段。
- [仅靠规范无法阻止后续再引入第二个可靠 transport] → 在 spec 中明确约束，并通过代码评审和后续实现测试守住边界。

## Migration Plan

1. 全仓检索并确认 `ReliableUdpTransport` 的剩余引用点、测试覆盖点和文档提及位置。
2. 删除 `ReliableUdpTransport.cs` 及相关 `.meta`，或在确有非可靠需求时以明确的新名称替换。
3. 调整受影响测试与文档，确保默认运行时入口和 capability 说明都只指向 `KcpTransport`。
4. 运行网络相关测试与工程编译，确认没有因删除旧类导致的残余引用或 asmdef 资产问题。
5. 若删除旧类后出现未预期依赖，可临时恢复文件以定位引用来源，但不恢复“可靠 UDP”命名进入主线。

## Open Questions

- 当前仓库是否还有服务端入口或外部工具脚本在工作区外引用 `ReliableUdpTransport`？
- 如果阶段六需要非可靠同步通道，团队是否希望直接使用 `UdpTransport` 命名，还是通过更贴合业务的 QoS 名称引入？
