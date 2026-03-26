## Why

阶段一已经把网络抽象收口到 `ITransport`，但当前运行时仍然只有一个名为 `ReliableUdpTransport` 的纯 UDP 实现，阶段二目标里要求的 KCP 可靠收发、会话维护和替换自定义可靠 UDP 结构还没有真正落地。继续在兼容类上堆补丁会再次混淆 UDP socket、会话状态和消息交付职责，因此需要单独提出 `KcpTransport` 变更，作为后续移除旧可靠 UDP 结构的前置条件。

## What Changes

- 新增 `KcpTransport`，以 `UdpClient` 承载原始数据报，以 KCP 负责可靠、有序的消息交付，并完整实现现有 `ITransport` 接口。
- 新增面向 KCP 的会话模型，用于管理客户端默认会话和服务端按远端地址维护的会话实例、活动时间与更新驱动。
- 将网络入口装配从 `ReliableUdpTransport` 切换为 `KcpTransport`，保持 `MessageManager` 的发送与接收契约不变。
- 为 KCP 传输与会话路由补充编辑器测试，验证完整业务消息能够经 `KcpTransport` 可靠收发。

## Capabilities

### New Capabilities
- `kcp-transport`: 提供基于 UDP + KCP 的 `ITransport` 实现，支持客户端默认会话和服务端多会话场景下的完整消息收发。

### Modified Capabilities

None.

## Impact

- 受影响代码：`Assets/Scripts/Network/NetworkTransport/`、`Assets/Scripts/NetworkManager.cs`、`Assets/Tests/EditMode/Network/`
- 受影响接口：`ITransport` 的调用方式保持不变，但默认运行时实现从 `ReliableUdpTransport` 切换到 `KcpTransport`
- 受影响依赖：需要项目中可用的 KCP C# 实现和对应程序集引用
- 受影响系统：客户端登录、心跳、输入上行、状态下行等所有通过 `MessageManager` 封包的网络消息链路
