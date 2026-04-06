#### MoveSpeed 对齐 ✅ 已确认无问题
                                                                                                                                                                         
| 环节                                                      | 值           | 说明                                                                    |
| --------------------------------------------------------- | ------------ | ----------------------------------------------------------------------- |
| Server ServerAuthoritativeMovementConfiguration.MoveSpeed | 5f（默认值） | CreateRuntimeConfiguration() 未设置，使用 null → 默认 5f                |
| Server → Client LoginResponse.Speed                       | 5            | BuildLoginResponse 中 (int)MathF.Round(host.AuthoritativeMoveSpeed) = 5 |
| Client MovementComponent._speed                           | 5            | Init(true, master, bootstrap.AuthoritativeMoveSpeed, ...) = 5           |

结论：MoveSpeed 实际上是对齐的。bootstrap.AuthoritativeMoveSpeed = 5，不是 check.md 中担心的默认值 2。

---
#### TurnSpeedDegreesPerSecond ✅ 一致

Server: 180f，Client: 180f。

---
#### SimulationInterval / BroadcastInterval ✅ 一致

均为 50ms。

---
#### AcknowledgedMoveTick 设置逻辑 ✅ 正确

Server HandleMoveInputAsync  → state.LastAcceptedMoveTick = input.Tick
Server BuildPlayerState      → AcknowledgedMoveTick = state.LastAcceptedMoveTick
Client TryApplyAuthoritativeState → pendingInputs.RemoveAll(tick <= AcknowledgedMoveTick)

路径正确。

---
#### Message Delivery Policy ✅ 一致

MoveInput   → HighFrequencySync
PlayerState → HighFrequencySync

DefaultMessageDeliveryPolicyResolver 中的策略映射与 check.md 描述完全吻合。

---
#### Server Update Cadence

DedicatedServerApplication.RunMainLoop() 以固定 50ms 为周期调用 UpdateAuthoritativeMovement，逻辑上是稳定的。但如果物理机负载高，可能产生波动。

---
#### SyncSequenceTracker 的潜在影响

SyncSequenceTracker 对 MoveInput 的过滤逻辑是：
streamKey = "input:{sender}:{playerId}"
sequence = input.Tick

如果客户端 MoveInput(Tick=N) 被丢弃，下一次 AcknowledgedMoveTick 会跳过 N，导致客户端的 pending inputs 被多删。这本身是正确的（服务端只认可它接受的 tick），但如果频繁丢弃，客户端会不断看到跳帧式的 correction。

建议：在客户端日志中过滤关键词 [MessageManager] 丢弃过期同步消息，确认是否有大量丢弃。

---
#### 总结

根据 check.md 列举的所有检查项，服务端实现均已对齐。最可能的抖动根因不在服务端配置层面，而在：

1. SyncSequenceTracker 的丢弃频率 — 可通过日志确认
2. SetServerTick 的自适应发送间隔振荡 — 48ms/52ms 的快速切换可能导致发送节奏不稳定
3. 客户端 ControlledPlayerCorrection 的 hard snap 阈值 — 如果 positionError > SnapPositionThreshold（默认 3 * 50ms * speed），会触发瞬移