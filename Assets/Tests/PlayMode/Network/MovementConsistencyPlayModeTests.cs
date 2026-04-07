using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkHost;
using Network.NetworkTransport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vector3 = UnityEngine.Vector3;

namespace Tests.PlayMode.Network
{
    /// <summary>
    /// PlayMode 测试：使用协程控制帧推进，验证客户端移动与服务端 authoritative state 的一致性。
    /// 使用真实的 ServerRuntimeEntryPoint，直接获取服务端的 authoritative state 进行对比。
    /// </summary>
    public class MovementConsistencyPlayModeTests
    {
        private static readonly IPEndPoint ClientPeer = new(IPAddress.Loopback, 9701);

        // 测试参数
        private const float MoveSpeed = 4f;
        private const float TurnSpeed = 180f;
        private const float DeltaTime = 0.05f; // 50ms 模拟步长

        [UnityTest]
        public IEnumerator OneFrame_MoveForward_ClientAndServerPositionMatch()
        {
            yield return RunMovementTest(
                playerId: "player-1frame",
                inputSequence: new[] { (0f, 1f) }, // 1帧：前进
                expectedTotalMovement: MoveSpeed * DeltaTime // 4 * 0.05 = 0.2
            );
        }

        [UnityTest]
        public IEnumerator FiveFrames_MoveForward_ClientAndServerPositionMatch()
        {
            yield return RunMovementTest(
                playerId: "player-5frames",
                inputSequence: new[] { (0f, 1f), (0f, 1f), (0f, 1f), (0f, 1f), (0f, 1f) }, // 5帧：连续前进
                expectedTotalMovement: MoveSpeed * DeltaTime * 5 // 4 * 0.05 * 5 = 1.0
            );
        }

        [UnityTest]
        public IEnumerator IntermittentMovement_StartStopStart_ClientAndServerPositionMatch()
        {
            // 场景：前进2帧 -> 停止1帧 -> 前进2帧
            yield return RunMovementTest(
                playerId: "player-intermittent",
                inputSequence: new[]
                {
                    (0f, 1f),  // 帧1：前进
                    (0f, 1f),  // 帧2：前进
                    (0f, 0f),  // 帧3：停止
                    (0f, 1f),  // 帧4：前进
                    (0f, 1f),  // 帧5：前进
                },
                expectedTotalMovement: MoveSpeed * DeltaTime * 4 // 4 * 0.05 * 4 = 0.8（只有4帧在移动）
            );
        }

        private IEnumerator RunMovementTest(
            string playerId,
            (float turn, float throttle)[] inputSequence,
            float expectedTotalMovement)
        {
            // ========== 创建服务器 ==========
            var serverTransports = new Dictionary<int, FakeTestTransport>();
            var configuration = new ServerRuntimeConfiguration(9700)
            {
                SyncPort = 9701,
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port =>
                {
                    var transport = new FakeTestTransport();
                    serverTransports[port] = transport;
                    return transport;
                },
                AuthoritativeMovement = new ServerAuthoritativeMovementConfiguration
                {
                    MoveSpeed = MoveSpeed,
                    SimulationInterval = TimeSpan.FromMilliseconds(50),
                    BroadcastInterval = TimeSpan.FromMilliseconds(50)
                }
            };

            var serverRuntime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            // ========== 登录 ==========
            serverRuntime.Host.NotifyLoginStarted(ClientPeer);
            serverRuntime.Host.NotifyLoginSucceeded(ClientPeer, playerId, MoveSpeed);

            yield return null; // 让服务器初始化
            serverRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            serverRuntime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));

            Debug.Log($"[Setup] Login complete. BroadcastMessages on sync transport: {serverTransports[9701].BroadcastMessages.Count}");

            // ========== 客户端本地计算状态 ==========
            float clientRotation = 0f; // Unity Yaw = 0 -> heading = 90
            Vector3 clientPosition = Vector3.zero;

            // ========== 运行帧模拟 ==========
            int tick = 1;
            List<Vector3> serverPositions = new List<Vector3>();
            List<Vector3> clientPositions = new List<Vector3>();

            foreach (var (turnInput, throttleInput) in inputSequence)
            {
                // --- 构造 MoveInput ---
                var moveInput = new MoveInput
                {
                    PlayerId = playerId,
                    Tick = tick,
                    TurnInput = turnInput,
                    ThrottleInput = throttleInput
                };

                // --- 发送到服务器 ---
                var envelope = new Envelope
                {
                    Type = (int)MessageType.MoveInput,
                    Payload = ByteString.CopyFrom(moveInput.ToByteArray())
                };
                Debug.Log($"[Tick {tick}] EmitReceive MoveInput to sync transport (turn={turnInput}, throttle={throttleInput}), BroadcastMessages before: {serverTransports[9701].BroadcastMessages.Count}");
                serverTransports[9701].EmitReceive(envelope.ToByteArray(), ClientPeer);

                // --- 服务器处理 ---
                Debug.Log($"[Tick {tick}] Before DrainPendingMessagesAsync, BroadcastMessages: {serverTransports[9701].BroadcastMessages.Count}");
                serverRuntime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
                serverRuntime.UpdateAuthoritativeMovement(TimeSpan.FromMilliseconds(50));
                Debug.Log($"[Tick {tick}] After DrainPendingMessagesAsync + UpdateAuthoritativeMovement, BroadcastMessages: {serverTransports[9701].BroadcastMessages.Count}");

                // --- 获取服务端 authoritative state（不需要等待广播）---
                Assert.That(serverRuntime.TryGetAuthoritativeMovementState(ClientPeer, out var serverState), Is.True);
                var serverPos = new Vector3(serverState.PositionX, serverState.PositionY, serverState.PositionZ);
                serverPositions.Add(serverPos);

                // --- 客户端本地计算（与服务端相同的算法） ---
                var clampedTurnInput = Mathf.Clamp(turnInput, -1f, 1f);
                var clampedThrottleInput = Mathf.Clamp(throttleInput, -1f, 1f);

                // 旋转（客户端 Tank Control）
                var heading = NormalizeDegrees(UnityYawToHeading(clientRotation) + (clampedTurnInput * TurnSpeed * DeltaTime));
                clientRotation = HeadingToUnityYaw(heading);

                // 速度（客户端 ResolveHeadingForward: forward = (cos, 0, sin)）
                var headingRad = heading * Mathf.Deg2Rad;
                var forwardX = Mathf.Cos(headingRad);
                var forwardZ = Mathf.Sin(headingRad);
                var velocityX = forwardX * (clampedThrottleInput * MoveSpeed);
                var velocityZ = forwardZ * (clampedThrottleInput * MoveSpeed);

                clientPosition.x += velocityX * DeltaTime;
                clientPosition.z += velocityZ * DeltaTime;
                clientPositions.Add(clientPosition);

                Debug.Log($"[Tick {tick}] Turn={turnInput}, Throttle={throttleInput} | " +
                          $"Client=({clientPosition.x:F3}, {clientPosition.y:F3}, {clientPosition.z:F3}) | " +
                          $"Server=({serverPos.x:F3}, {serverPos.y:F3}, {serverPos.z:F3})");

                tick++;
                yield return null; // 推进一帧
            }

            // ========== 验证 ==========
            var finalClientPos = clientPositions[^1];
            var finalServerPos = serverPositions[^1];

            Debug.Log($"========================================");
            Debug.Log($"[Test: {playerId}]");
            Debug.Log($"Expected Z Movement: {expectedTotalMovement}");
            Debug.Log($"Client Final Z: {finalClientPos.z:F4}");
            Debug.Log($"Server Final Z: {finalServerPos.z:F4}");
            Debug.Log($"Server Final Full: ({finalServerPos.x:F4}, {finalServerPos.y:F4}, {finalServerPos.z:F4})");
            Debug.Log($"========================================");

            float clientMovement = finalClientPos.z;
            float serverMovement = finalServerPos.z;

            Assert.That(Math.Abs(clientMovement - serverMovement), Is.LessThan(0.01f),
                $"Client Z movement ({clientMovement:F4}) should match Server Z movement ({serverMovement:F4})");

            Assert.That(Math.Abs(clientMovement - expectedTotalMovement), Is.LessThan(0.01f),
                $"Movement ({clientMovement:F4}) should match expected ({expectedTotalMovement:F4})");

            // ========== 清理 ==========
            serverRuntime.Stop();
        }

        private static float NormalizeDegrees(float degrees)
        {
            var normalized = degrees % 360f;
            if (normalized < 0f) normalized += 360f;
            return normalized;
        }

        private static float UnityYawToHeading(float unityYawDegrees)
        {
            return NormalizeDegrees(90f - unityYawDegrees);
        }

        private static float HeadingToUnityYaw(float headingDegrees)
        {
            return NormalizeDegrees(90f - headingDegrees);
        }
    }

    // ========== 测试辅助类 ==========

    /// <summary>
    /// 用于 PlayMode 测试的 FakeTransport
    /// </summary>
    public class FakeTestTransport : ITransport
    {
        private readonly List<byte[]> _receivedMessages = new();
        public List<byte[]> BroadcastMessages { get; } = new();
        public event Action<byte[], IPEndPoint> OnReceive;

        public void EmitReceive(byte[] data, IPEndPoint sender)
        {
            _receivedMessages.Add(data);
            OnReceive?.Invoke(data, sender);
        }

        public void Send(byte[] data) { }
        public void SendTo(byte[] data, IPEndPoint target) { }
        public void SendToAll(byte[] data)
        {
            BroadcastMessages.Add(data);
            OnReceive?.Invoke(data, new IPEndPoint(IPAddress.Loopback, 0));
        }

        public Task StartAsync() => Task.CompletedTask;
        public void Stop() { }
    }
}
