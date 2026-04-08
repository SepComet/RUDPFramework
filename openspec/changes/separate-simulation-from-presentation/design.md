## Context

当前 `MovementComponent` 的 `Reconcile()` 方法在收到服务器 PlayerState 后：
1. 强制 snap 到 server position
2. 重放 pending inputs（可能产生位移）
3. 用 bounded correction 从重放后位置收敛回 server position

问题：server position 每帧都在变化（服务器在广播），bounded correction 的收敛目标一直在变，导致永远追不上。

## Goals / Non-Goals

**Goals:**
- 将模拟层（服务器权威 truth）和表现层（纯视觉插值）解耦
- 表现层用 Lerp 替代 bounded correction，消除追赶震荡
- 模拟层只输出"目标位置"，表现层只管插值

**Non-Goals:**
- 不改变网络协议或服务器逻辑
- 不修改远程玩家的插值逻辑

## Decisions

### Decision: 表现层直接设置 rigid.position，不使用 MovePosition

**选择：表现层直接设置 `_rigid.position`**

- Rigidbody interpolation 设为 `None`
- 表现层直接写入 `_rigid.position` 和 `_rigid.rotation`
- 不经过 `Rigidbody.MovePosition`（避免物理引擎介入）

### Decision: 模拟层收到服务器状态时立即计算 target

收到服务器 PlayerState 时：
1. Acknowledge inputs（移除 tick <= AckTick 的输入）
2. 从 authoritative position 重放剩余 pending inputs
3. 计算 `target = authoritativePosition + replayDisplacement`
4. 判断 error：error > SnapThreshold → snap；else → 更新 `_presentationTarget`

**关键**：`_presentationTarget` 在两次收到服务器状态之间保持不变，表现层稳定 Lerp。

### Decision: 插值策略使用 Lerp

固定 alpha = 0.15~0.2。后续可根据 RTT 动态调整或改用 SmoothDamp。

## Risks / Trade-offs

[Risk] 插值延迟导致本地玩家看到的位置比服务器延迟
→ [Mitigation] Lerp 的延迟是固定的（不像 bounded correction 那样持续追赶），且不影响服务器权威性

[Risk] 删除 bounded correction 后无法平滑大误差
→ [Mitigation] Snap 阈值（0.5 unit）处理大误差，直接跳到目标；Lerp 处理小误差自然收敛
