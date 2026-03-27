## Why

阶段二已经把默认运行时切换到 `KcpTransport`，并且现有 `ReliableUdpTransport` 不再承载自定义 ACK、重传或乱序重组逻辑，而是退化成一个名称误导的 plain UDP 兼容实现。阶段三需要正式清理这层遗留概念，确保项目内不再并存“名义上的旧可靠 UDP”和 KCP 两套可靠传输入口，避免后续连接生命周期、主线程分发和同步优化继续建立在模糊的传输语义上。

## What Changes

- 删除或退役 `ReliableUdpTransport` 这一遗留可靠 UDP 命名与入口，避免运行时和调用方继续把它当成可靠传输实现。
- 明确 `KcpTransport` 是项目内唯一的可靠 `ITransport` 实现，所有可靠消息链路继续通过 KCP 会话收发。
- 清理与旧可靠 UDP 相关的残留代码、测试和文档表述，消除“双重可靠性机制仍然存在”的误导。
- 如项目仍需保留裸 UDP 能力，使用明确的非可靠命名与职责边界，而不是沿用 `ReliableUdpTransport` 兼容壳。

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `kcp-transport`: 扩展传输层要求，明确 KCP 是唯一可靠传输路径，并要求遗留的 `ReliableUdpTransport` 兼容入口不再作为可靠实现保留。

## Impact

- 受影响代码：`Assets/Scripts/Network/NetworkTransport/`、`Assets/Scripts/NetworkManager.cs`、`Assets/Tests/EditMode/Network/`
- 受影响接口：`ITransport` 形状保持不变，但可靠传输实现的选择与命名边界会进一步收紧
- 受影响系统：客户端登录、心跳、输入上行、状态下行等所有依赖可靠消息交付的链路
- 受影响文档：`CodeX-TODO.md` 对阶段三的实施结果、以及 OpenSpec 下的 `kcp-transport` 能力定义
