## 1. Remove legacy transport entry points

- [x] 1.1 全仓检索 `ReliableUdpTransport` 的代码、测试和文档引用，确认删除或替换范围
- [x] 1.2 删除 `Assets/Scripts/Network/NetworkTransport/ReliableUdpTransport.cs` 及其相关资产；如果必须保留裸 UDP，则以明确的非可靠命名重建

## 2. Reconcile runtime and specifications

- [x] 2.1 确认运行时入口和网络层装配仅使用 `KcpTransport` 作为可靠 `ITransport` 实现
- [x] 2.2 更新受影响的 OpenSpec、TODO 或内联说明，明确项目不再保留旧可靠 UDP 入口

## 3. Verification

- [x] 3.1 调整或新增测试，验证默认可靠传输路径仍然是 `KcpTransport`，且不存在可直接使用的旧可靠 UDP 入口
- [x] 3.2 运行网络相关测试与工程编译，确认删除遗留 transport 后无残余引用或资源错误


