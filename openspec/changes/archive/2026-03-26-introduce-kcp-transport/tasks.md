## 1. KCP transport scaffolding

- [x] 1.1 确认并接入 Unity 工程可用的 KCP C# 依赖与程序集引用，保证 `Assets/Scripts/Network/NetworkTransport/` 可以实例化 KCP 对象
- [x] 1.2 新增 `KcpTransport` 与内部 `KcpSession` 基础结构，补齐客户端默认远端、服务端监听模式和会话状态容器

## 2. Core transport implementation

- [x] 2.1 实现客户端 `Send`、服务端 `SendTo`、`SendToAll` 的 KCP 编码路径，确保所有出站消息都通过对应会话发送 UDP 数据报
- [x] 2.2 实现 UDP 接收循环，将入站数据报路由到正确的 `KcpSession.Input`，并在 `Kcp.Recv` 拿到完整业务消息后触发 `OnReceive`
- [x] 2.3 实现会话更新与关闭流程，包括周期性 `Kcp.Check`/`Kcp.Update` 驱动、活动状态刷新、`Stop()` 时的循环停止和资源清理

## 3. Integration and verification

- [x] 3.1 将默认网络入口从 `ReliableUdpTransport` 切换到 `KcpTransport`，保持 `MessageManager` 和现有消息封包逻辑不变
- [x] 3.2 为 `KcpTransport` 增加编辑器测试，覆盖默认会话发送、多远端会话隔离、完整消息交付和停止清理行为
- [x] 3.3 运行相关网络编辑器测试并修正集成问题，确认阶段二 capability 达到 apply-ready 的实现标准
