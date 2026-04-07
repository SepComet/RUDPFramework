using System;
using Network.Defines;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Network
{
    /// <summary>
    /// 验证客户端 ApplyTankMovement 和服务端 IntegrateState
    /// 在相同输入下产生相同的移动结果。
    ///
    /// 使用纯函数对比，不依赖任何状态对象。
    /// </summary>
    public class MovementAlgorithmConsistencyTests
    {
        private const float MoveSpeed = 4f;
        private const float TurnSpeed = 180f;
        private const float DeltaTime = 0.05f; // 50ms

        [Test]
        public void ServerForward_ZeroRotation_MovesInPositiveZ()
        {
            // Arrange: 服务端 rotation=0，throttle=1
            float rotation = 0f;
            float throttleInput = 1f;

            // Act: 服务端 forward 计算（新公式）
            var rotationRadians = rotation * (MathF.PI / 180f);
            var forwardX = MathF.Sin(rotationRadians);  // 新公式
            var forwardZ = MathF.Cos(rotationRadians);  // 新公式
            var velocityX = forwardX * (throttleInput * MoveSpeed);
            var velocityZ = forwardZ * (throttleInput * MoveSpeed);

            // Assert: rotation=0° → forward=(0,0,1) = +Z
            Assert.That(forwardX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(forwardZ, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(velocityX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(velocityZ, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ServerForward_90DegreeRotation_MovesInPositiveX()
        {
            // Arrange: 服务端 rotation=90°，throttle=1
            float rotation = 90f;
            float throttleInput = 1f;

            // Act: 服务端 forward 计算（新公式）
            var rotationRadians = rotation * (MathF.PI / 180f);
            var forwardX = MathF.Sin(rotationRadians);  // 新公式
            var forwardZ = MathF.Cos(rotationRadians);  // 新公式
            var velocityX = forwardX * (throttleInput * MoveSpeed);
            var velocityZ = forwardZ * (throttleInput * MoveSpeed);

            // Assert: rotation=90° → forward=(1,0,0) = +X
            Assert.That(forwardX, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(forwardZ, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(velocityX, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(velocityZ, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ClientForward_UnityYawZero_MovesInPositiveZ()
        {
            // Arrange: Unity Yaw = 0°，客户端转换后 heading = 90°
            float unityYaw = 0f;
            float throttleInput = 1f;

            // Act: 客户端 heading 计算
            var heading = NormalizeDegrees(UnityYawToHeading(unityYaw));

            // 客户端 ResolveHeadingForward: forward = (cos, 0, sin)
            var rotationRadians = heading * Mathf.Deg2Rad;
            var forwardX = Mathf.Cos(rotationRadians);
            var forwardZ = Mathf.Sin(rotationRadians);
            var velocityX = forwardX * throttleInput * MoveSpeed;
            var velocityZ = forwardZ * throttleInput * MoveSpeed;

            // Assert: Unity Yaw=0 → heading=90° → forward=(cos(90°), sin(90°))=(0,1) = +Z
            Assert.That(heading, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(forwardX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(forwardZ, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(velocityX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(velocityZ, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ClientServer_IdenticalInputs_ProduceIdenticalOutput()
        {
            // 这个测试验证：相同的输入在客户端和服务端产生相同的最终位置
            //
            // 场景：初始位置 (0,0,0)，初始 heading=90°（Unity Yaw=0），
            // 向前移动 4 个 50ms 步长

            // ===== 共享参数 =====
            float moveSpeed = MoveSpeed;
            float turnSpeed = TurnSpeed;
            float dt = DeltaTime;

            // ===== 服务端计算 =====
            float serverPosX = 0f, serverPosZ = 0f;
            float serverRotation = 0f; // 服务端 rotation 直接是 heading
            float serverThrottle = 1f;

            for (int i = 0; i < 4; i++)
            {
                // 速度计算（服务端新 forward 公式：forwardX = sin, forwardZ = cos）
                var rotRad = serverRotation * (MathF.PI / 180f);
                var fwdX = MathF.Sin(rotRad);
                var fwdZ = MathF.Cos(rotRad);
                var velX = fwdX * (serverThrottle * moveSpeed);
                var velZ = fwdZ * (serverThrottle * moveSpeed);

                // 位置积分
                serverPosX += velX * dt;
                serverPosZ += velZ * dt;
            }

            // ===== 客户端计算 =====
            float clientPosX = 0f, clientPosZ = 0f;
            float clientUnityYaw = 0f; // Unity Yaw = 0 → heading = 90°
            float clientThrottle = 1f;

            for (int i = 0; i < 4; i++)
            {
                // 旋转（客户端需要转换）
                var heading = NormalizeDegrees(UnityYawToHeading(clientUnityYaw) + (0f * turnSpeed * dt));
                clientUnityYaw = HeadingToUnityYaw(heading);

                // 客户端 ResolveHeadingForward: forward = (cos, 0, sin)
                var headingRad = heading * Mathf.Deg2Rad;
                var fwdX = Mathf.Cos(headingRad);
                var fwdZ = Mathf.Sin(headingRad);
                var velX = fwdX * (clientThrottle * moveSpeed);
                var velZ = fwdZ * (clientThrottle * moveSpeed);

                // 位置积分
                clientPosX += velX * dt;
                clientPosZ += velZ * dt;
            }

            // ===== 对比 =====
            // 服务端期望：rotation=0° → forward=(0,0,1) → velocity=(0,0,4) → 每步 0.2
            // 4步后：pos = (0, 0, 0.8)
            Assert.That(serverPosX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(serverPosZ, Is.EqualTo(0.8f).Within(0.0001f));

            // 客户端应与服务端一致
            Assert.That(clientPosX, Is.EqualTo(serverPosX).Within(0.0001f),
                $"Client X ({clientPosX}) should match Server X ({serverPosX})");
            Assert.That(clientPosZ, Is.EqualTo(serverPosZ).Within(0.0001f),
                $"Client Z ({clientPosZ}) should match Server Z ({serverPosZ})");
        }

        [Test]
        public void ClientServer_TurnRight90Degrees_ThenMove_ProduceIdenticalOutput()
        {
            // 场景：初始向前走，然后右转90°，再走
            // 验证转向后的方向一致

            float moveSpeed = MoveSpeed;
            float turnSpeed = TurnSpeed;
            float dt = DeltaTime;

            // ===== 服务端计算 =====
            float serverPosX = 0f, serverPosZ = 0f;
            float serverRotation = 0f; // heading=0°，向前走
            float serverThrottle = 1f;
            float serverTurnInput = 1f; // 右转

            // 先走2步
            for (int i = 0; i < 2; i++)
            {
                var rotRad = serverRotation * (MathF.PI / 180f);
                var fwdX = MathF.Sin(rotRad);
                var fwdZ = MathF.Cos(rotRad);
                var velX = fwdX * serverThrottle * moveSpeed;
                var velZ = fwdZ * serverThrottle * moveSpeed;
                serverPosX += velX * dt;
                serverPosZ += velZ * dt;
            }

            // 右转1步（turnInput=1，转 180*0.05=9°）
            serverRotation = NormalizeDegreesServer(serverRotation + (serverTurnInput * turnSpeed * dt));

            // 再走2步
            for (int i = 0; i < 2; i++)
            {
                var rotRad = serverRotation * (MathF.PI / 180f);
                var fwdX = MathF.Sin(rotRad);
                var fwdZ = MathF.Cos(rotRad);
                var velX = fwdX * serverThrottle * moveSpeed;
                var velZ = fwdZ * serverThrottle * moveSpeed;
                serverPosX += velX * dt;
                serverPosZ += velZ * dt;
            }

            // ===== 客户端计算 =====
            float clientPosX = 0f, clientPosZ = 0f;
            float clientUnityYaw = 0f; // 初始朝前
            float clientThrottle = 1f;
            float clientTurnInput = -1f; // Unity 中右转 = -input.x

            // 先走2步
            for (int i = 0; i < 2; i++)
            {
                var heading = NormalizeDegrees(UnityYawToHeading(clientUnityYaw));
                var headingRad = heading * Mathf.Deg2Rad;
                var fwdX = Mathf.Cos(headingRad);
                var fwdZ = Mathf.Sin(headingRad);
                var velX = fwdX * clientThrottle * moveSpeed;
                var velZ = fwdZ * clientThrottle * moveSpeed;
                clientPosX += velX * dt;
                clientPosZ += velZ * dt;
            }

            // 右转1步
            var newHeading = NormalizeDegrees(UnityYawToHeading(clientUnityYaw) + (clientTurnInput * turnSpeed * dt));
            clientUnityYaw = HeadingToUnityYaw(newHeading);

            // 再走2步
            for (int i = 0; i < 2; i++)
            {
                var heading = NormalizeDegrees(UnityYawToHeading(clientUnityYaw));
                var headingRad = heading * Mathf.Deg2Rad;
                var fwdX = Mathf.Cos(headingRad);
                var fwdZ = Mathf.Sin(headingRad);
                var velX = fwdX * clientThrottle * moveSpeed;
                var velZ = fwdZ * clientThrottle * moveSpeed;
                clientPosX += velX * dt;
                clientPosZ += velZ * dt;
            }

            // ===== 对比 =====
            Assert.That(clientPosX, Is.EqualTo(serverPosX).Within(0.0001f),
                $"Client X ({clientPosX}) should match Server X ({serverPosX})");
            Assert.That(clientPosZ, Is.EqualTo(serverPosZ).Within(0.0001f),
                $"Client Z ({clientPosZ}) should match Server Z ({serverPosZ})");
        }

        // ===== 辅助方法（从 MovementComponent 复制） =====

        private static float UnityYawToHeading(float unityYawDegrees)
        {
            return NormalizeDegrees(90f - unityYawDegrees);
        }

        private static float HeadingToUnityYaw(float headingDegrees)
        {
            return NormalizeDegrees(90f - headingDegrees);
        }

        private static float NormalizeDegrees(float degrees)
        {
            var normalized = degrees % 360f;
            if (normalized < 0f)
            {
                normalized += 360f;
            }
            return normalized;
        }

        private static float NormalizeDegreesServer(float degrees)
        {
            var normalized = degrees % 360f;
            if (normalized <= -180f)
            {
                normalized += 360f;
            }
            else if (normalized > 180f)
            {
                normalized -= 360f;
            }
            return normalized;
        }
    }
}
