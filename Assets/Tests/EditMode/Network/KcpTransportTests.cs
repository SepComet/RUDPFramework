using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Network.NetworkTransport;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.EditMode.Network
{
    public class KcpTransportTests
    {
        private const int DefaultTimeoutMs = 5000;

        [UnityTest]
        public IEnumerator ClientMode_SendRoutesThroughDefaultSession_AndDeliversCompletePayload()
        {
            return RunAsync(ClientMode_SendRoutesThroughDefaultSession_AndDeliversCompletePayloadAsync);
        }

        private static async Task ClientMode_SendRoutesThroughDefaultSession_AndDeliversCompletePayloadAsync()
        {
            var listenPort = GetAvailableUdpPort();
            var server = new KcpTransport(listenPort);
            var client = new KcpTransport(IPAddress.Loopback.ToString(), listenPort);
            var payload = CreatePayload(4096, seed: 11);
            var receiveCount = 0;
            var receivedTask = new TaskCompletionSource<(byte[] Payload, IPEndPoint Sender)>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnReceive += (data, sender) =>
            {
                receiveCount++;
                receivedTask.TrySetResult((data, sender));
            };

            try
            {
                await server.StartAsync();
                await client.StartAsync();

                Assert.That(GetActiveSessionCount(client), Is.EqualTo(1));

                client.Send(payload);

                var result = await WaitFor(receivedTask.Task, "Timed out waiting for server payload.");
                await Task.Delay(200);

                Assert.That(result.Payload, Is.EqualTo(payload));
                Assert.That(result.Sender.Address, Is.EqualTo(IPAddress.Loopback));
                Assert.That(receiveCount, Is.EqualTo(1));
            }
            finally
            {
                client.Stop();
                server.Stop();
            }
        }

        [UnityTest]
        public IEnumerator ServerMode_CreatesIndependentSessions_PerRemoteEndpoint()
        {
            return RunAsync(ServerMode_CreatesIndependentSessions_PerRemoteEndpointAsync);
        }

        private static async Task ServerMode_CreatesIndependentSessions_PerRemoteEndpointAsync()
        {
            var listenPort = GetAvailableUdpPort();
            var server = new KcpTransport(listenPort);
            var clientA = new KcpTransport(IPAddress.Loopback.ToString(), listenPort);
            var clientB = new KcpTransport(IPAddress.Loopback.ToString(), listenPort);
            var received = new ConcurrentQueue<(byte[] Payload, IPEndPoint Sender)>();
            var allMessagesTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnReceive += (data, sender) =>
            {
                received.Enqueue((data, sender));
                if (received.Count >= 2)
                {
                    allMessagesTask.TrySetResult(true);
                }
            };

            try
            {
                await server.StartAsync();
                await clientA.StartAsync();
                await clientB.StartAsync();

                clientA.Send(CreatePayload(128, seed: 21));
                clientB.Send(CreatePayload(256, seed: 37));

                await WaitFor(allMessagesTask.Task, "Timed out waiting for messages from both clients.");

                Assert.That(GetActiveSessionCount(server), Is.EqualTo(2));

                var senders = received.Select(item => item.Sender.ToString()).Distinct().ToArray();
                Assert.That(senders, Has.Length.EqualTo(2));

                var payloadLengths = received.Select(item => item.Payload.Length).OrderBy(length => length).ToArray();
                Assert.That(payloadLengths, Is.EqualTo(new[] { 128, 256 }));
            }
            finally
            {
                clientA.Stop();
                clientB.Stop();
                server.Stop();
            }
        }

        [UnityTest]
        public IEnumerator SendToAll_BroadcastsToEveryActiveSession()
        {
            return RunAsync(SendToAll_BroadcastsToEveryActiveSessionAsync);
        }

        private static async Task SendToAll_BroadcastsToEveryActiveSessionAsync()
        {
            var listenPort = GetAvailableUdpPort();
            var server = new KcpTransport(listenPort);
            var clientA = new KcpTransport(IPAddress.Loopback.ToString(), listenPort);
            var clientB = new KcpTransport(IPAddress.Loopback.ToString(), listenPort);
            var broadcastPayload = CreatePayload(2048, seed: 53);
            var clientAReceivedTask = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var clientBReceivedTask = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            clientA.OnReceive += (data, sender) => clientAReceivedTask.TrySetResult(data);
            clientB.OnReceive += (data, sender) => clientBReceivedTask.TrySetResult(data);

            try
            {
                await server.StartAsync();
                await clientA.StartAsync();
                await clientB.StartAsync();

                clientA.Send(CreatePayload(64, seed: 61));
                clientB.Send(CreatePayload(64, seed: 79));

                await WaitUntilAsync(() => GetActiveSessionCount(server) == 2, "Timed out waiting for server sessions.");

                server.SendToAll(broadcastPayload);

                var clientAResult = await WaitFor(clientAReceivedTask.Task, "Timed out waiting for broadcast on client A.");
                var clientBResult = await WaitFor(clientBReceivedTask.Task, "Timed out waiting for broadcast on client B.");

                Assert.That(clientAResult, Is.EqualTo(broadcastPayload));
                Assert.That(clientBResult, Is.EqualTo(broadcastPayload));
            }
            finally
            {
                clientA.Stop();
                clientB.Stop();
                server.Stop();
            }
        }

        [UnityTest]
        public IEnumerator Stop_ClearsAllActiveSessions()
        {
            return RunAsync(Stop_ClearsAllActiveSessionsAsync);
        }

        private static async Task Stop_ClearsAllActiveSessionsAsync()
        {
            var listenPort = GetAvailableUdpPort();
            var transport = new KcpTransport(IPAddress.Loopback.ToString(), listenPort);

            await transport.StartAsync();
            Assert.That(GetActiveSessionCount(transport), Is.EqualTo(1));

            transport.Stop();

            Assert.That(GetActiveSessionCount(transport), Is.EqualTo(0));
            Assert.DoesNotThrow(() => transport.Stop());
        }

        private static async Task<T> WaitFor<T>(Task<T> task, string failureMessage)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(DefaultTimeoutMs));
            if (completedTask != task)
            {
                Assert.Fail(failureMessage);
            }

            return await task;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string failureMessage)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(DefaultTimeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.Fail(failureMessage);
        }

        private static IEnumerator RunAsync(Func<Task> asyncAction)
        {
            var task = asyncAction();

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var exception = task.Exception?.InnerExceptions.Count == 1
                    ? task.Exception.InnerExceptions[0]
                    : task.Exception;
                ExceptionDispatchInfo.Capture(exception ?? new Exception("Async test failed.")).Throw();
            }

            if (task.IsCanceled)
            {
                Assert.Fail("Async test was canceled.");
            }
        }

        private static byte[] CreatePayload(int length, int seed)
        {
            var bytes = new byte[length];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)((index + seed) % byte.MaxValue);
            }

            return bytes;
        }

        private static int GetAvailableUdpPort()
        {
            using var client = new UdpClient(0);
            return ((IPEndPoint)client.Client.LocalEndPoint).Port;
        }

        private static int GetActiveSessionCount(KcpTransport transport)
        {
            var property = typeof(KcpTransport).GetProperty(
                "ActiveSessionCount",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (property == null)
            {
                throw new MissingMemberException(typeof(KcpTransport).FullName, "ActiveSessionCount");
            }

            return (int)property.GetValue(transport);
        }
    }
}
