using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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

        [Test]
        public void DefaultTransportMetricsModule_CompleteRun_IsIdempotentAndWritesSingleReport()
        {
            var reportDirectory = CreateReportDirectory();
            var consoleWriter = new StringWriter();

            try
            {
                var module = new DefaultTransportMetricsModule(reportDirectory, consoleWriter: consoleWriter);
                var remote = new IPEndPoint(IPAddress.Loopback, 5001);

                module.BeginRun(new TransportRunDescriptor(nameof(KcpTransport), isServer: false, defaultRemoteEndPoint: remote));
                module.RecordSessionOpened(remote);
                module.RecordSessionDiagnostics(remote, new TransportSessionDiagnosticsSnapshot
                {
                    LifecycleState = "active",
                    ObservedAtUtc = DateTimeOffset.UtcNow,
                    SmoothedRttMs = 18,
                    RetransmissionTimeoutMs = 45,
                    WaitSendCount = 3,
                    SendQueueCount = 2,
                    SendBufferCount = 1,
                    ReceiveQueueCount = 0,
                    ReceiveBufferCount = 0,
                    RetransmittedSegmentsInFlight = 1,
                    ObservedRetransmissionSends = 4,
                    ObservedLossSignals = 4
                });
                module.RecordApplicationSessionSnapshot(new TransportApplicationSessionSnapshot
                {
                    Scope = "shared-runtime",
                    ConnectionState = "LoggedIn",
                    CanSendHeartbeat = true,
                    LastRoundTripTimeMs = 18,
                    CurrentServerTick = 321,
                    ObservedAtUtc = DateTimeOffset.UtcNow
                });
                module.RecordPayloadSent(remote, 64);
                module.RecordDatagramSent(remote, 96);
                module.RecordError("socket-send", remote, "simulated");
                module.RecordSessionClosed(remote);

                var first = module.CompleteRun();
                var second = module.CompleteRun();
                var reportFiles = Directory.GetFiles(reportDirectory, "*.json");
                var summaryFiles = Directory.GetFiles(reportDirectory, "*.summary.txt");
                var diagnosisFiles = Directory.GetFiles(reportDirectory, "*.diagnosis.txt");
                var reportText = File.ReadAllText(reportFiles[0]);
                var summaryText = File.ReadAllText(summaryFiles[0]);
                var diagnosisText = File.ReadAllText(diagnosisFiles[0]);

                Assert.That(reportFiles, Has.Length.EqualTo(1));
                Assert.That(summaryFiles, Has.Length.EqualTo(1));
                Assert.That(diagnosisFiles, Has.Length.EqualTo(1));
                Assert.That(first.ReportPath, Is.EqualTo(reportFiles[0]));
                Assert.That(first.SummaryPath, Is.EqualTo(summaryFiles[0]));
                Assert.That(second.ReportPath, Is.EqualTo(first.ReportPath));
                Assert.That(first.SessionsCreated, Is.EqualTo(1));
                Assert.That(first.SessionsClosed, Is.EqualTo(1));
                Assert.That(first.SessionsWithDiagnostics, Is.EqualTo(1));
                Assert.That(first.AverageSmoothedRttMs, Is.EqualTo(18).Within(0.01));
                Assert.That(first.TotalObservedRetransmissionSends, Is.EqualTo(4));
                Assert.That(first.TotalObservedLossSignals, Is.EqualTo(4));
                Assert.That(first.SessionStateCounts["closed"], Is.EqualTo(1));
                Assert.That(first.ApplicationSessionsTracked, Is.EqualTo(1));
                Assert.That(first.ApplicationSessionStateCounts["LoggedIn"], Is.EqualTo(1));
                Assert.That(first.ErrorCountsByStage["socket-send"], Is.EqualTo(1));
                Assert.That(first.ReadableSummary.Headline, Does.Contain("finished"));
                Assert.That(first.ReadableSummary.LifecycleSummary, Does.Contain("states=LoggedIn=1"));
                Assert.That(first.ReadableSummary.HealthSummary, Does.Contain("avgRtt=18.0 ms"));
                Assert.That(first.ReadableSummary.HealthSummary, Does.Contain("observedRetransmissions=4"));
                Assert.That(reportText, Does.Contain(Environment.NewLine));
                Assert.That(reportText, Does.Contain("  \"RunId\""));
                Assert.That(reportText, Does.Contain("  \"ReadableSummary\""));
                Assert.That(summaryText, Does.Contain("Transport Metrics Summary"));
                Assert.That(summaryText, Does.Contain("English Summary"));
                Assert.That(summaryText, Does.Contain("Chinese Summary"));
                Assert.That(summaryText, Does.Contain("Top Peers:"));
                Assert.That(summaryText, Does.Contain("states=LoggedIn=1"));
                Assert.That(summaryText, Does.Contain("avgRtt=18.0 ms"));
                Assert.That(diagnosisText, Does.Contain("传输诊断结论"));
                Assert.That(diagnosisText, Does.Contain("网络质量存在明显风险"));
                Assert.That(diagnosisText, Does.Contain("共享会话已跟踪 1 个"));
                Assert.That(summaryText, Does.Contain("重点对端:"));
                Assert.That(consoleWriter.ToString(), Does.Contain("[TransportMetrics] English Summary"));
                Assert.That(consoleWriter.ToString(), Does.Contain("[TransportMetrics] Chinese Summary"));
            }
            finally
            {
                DeleteDirectory(reportDirectory);
            }
        }

        [UnityTest]
        public IEnumerator MetricsSnapshot_TracksMultiplePeers_AndExportsOnceOnStop()
        {
            return RunAsync(MetricsSnapshot_TracksMultiplePeers_AndExportsOnceOnStopAsync);
        }

        private static async Task MetricsSnapshot_TracksMultiplePeers_AndExportsOnceOnStopAsync()
        {
            var listenPort = GetAvailableUdpPort();
            var serverReportDirectory = CreateReportDirectory();
            var clientAReportDirectory = CreateReportDirectory();
            var clientBReportDirectory = CreateReportDirectory();
            var serverMetrics = new DefaultTransportMetricsModule(serverReportDirectory, consoleWriter: new StringWriter());
            var clientAMetrics = new DefaultTransportMetricsModule(clientAReportDirectory, consoleWriter: new StringWriter());
            var clientBMetrics = new DefaultTransportMetricsModule(clientBReportDirectory, consoleWriter: new StringWriter());
            var server = new KcpTransport(listenPort, metricsModule: serverMetrics);
            var clientA = new KcpTransport(IPAddress.Loopback.ToString(), listenPort, metricsModule: clientAMetrics);
            var clientB = new KcpTransport(IPAddress.Loopback.ToString(), listenPort, metricsModule: clientBMetrics);
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

                clientA.Send(CreatePayload(128, seed: 91));
                clientB.Send(CreatePayload(256, seed: 117));

                await WaitFor(allMessagesTask.Task, "Timed out waiting for metrics traffic from both clients.");
                await Task.Delay(200);

                var liveSnapshot = server.GetMetricsSnapshot();
                Assert.That(liveSnapshot.PayloadMessagesReceived, Is.EqualTo(2));
                Assert.That(liveSnapshot.PeakActiveSessions, Is.EqualTo(2));
                Assert.That(liveSnapshot.PeerSummaries, Has.Count.EqualTo(2));
                Assert.That(liveSnapshot.PeerSummaries.Sum(peer => peer.PayloadMessagesReceived), Is.EqualTo(2));
                Assert.That(liveSnapshot.PeerSummaries.Select(peer => peer.RemoteEndPoint).Distinct().Count(), Is.EqualTo(2));
                Assert.That(liveSnapshot.SessionsWithDiagnostics, Is.EqualTo(2));
                Assert.That(liveSnapshot.PeerSummaries.All(peer => peer.SessionDiagnostics.ObservedAtUtc.HasValue), Is.True);
                Assert.That(liveSnapshot.PeerSummaries.All(peer => peer.SessionLifecycleState == "active"), Is.True);
                Assert.That(liveSnapshot.TotalSendQueueCount, Is.GreaterThanOrEqualTo(0));
                Assert.That(liveSnapshot.TotalObservedRetransmissionSends, Is.GreaterThanOrEqualTo(0));

                server.Stop();
                clientA.Stop();
                clientB.Stop();

                var completedSnapshot = server.GetMetricsSnapshot();
                Assert.That(completedSnapshot.ReportPath, Is.Not.Null.And.Not.Empty);
                Assert.That(completedSnapshot.SummaryPath, Is.Not.Null.And.Not.Empty);
                Assert.That(Directory.GetFiles(serverReportDirectory, "*.json"), Has.Length.EqualTo(1));
                Assert.That(Directory.GetFiles(serverReportDirectory, "*.summary.txt"), Has.Length.EqualTo(1));
                Assert.That(Directory.GetFiles(serverReportDirectory, "*.diagnosis.txt"), Has.Length.EqualTo(1));
                Assert.That(completedSnapshot.ActiveSessions, Is.EqualTo(0));
                Assert.That(completedSnapshot.SessionsClosed, Is.EqualTo(2));
                Assert.That(completedSnapshot.SessionStateCounts["closed"], Is.EqualTo(2));
                Assert.That(completedSnapshot.ReadableSummary.HealthSummary, Does.Contain("states=closed=2"));
            }
            finally
            {
                clientA.Stop();
                clientB.Stop();
                server.Stop();
                DeleteDirectory(serverReportDirectory);
                DeleteDirectory(clientAReportDirectory);
                DeleteDirectory(clientBReportDirectory);
            }
        }

        [Test]
        public void DefaultTransportMetricsModule_DisabledReports_SkipsFilesAndConsole()
        {
            var reportDirectory = CreateReportDirectory();
            var consoleWriter = new StringWriter();
            var options = new TransportMetricsOptions
            {
                ReportDirectory = reportDirectory,
                ConsoleWriter = consoleWriter,
                WriteJsonReport = false,
                WriteTextSummaryReport = false,
                WriteDiagnosisReport = false,
                EmitConsoleSummary = false
            };

            try
            {
                var module = new DefaultTransportMetricsModule(options);
                var remote = new IPEndPoint(IPAddress.Loopback, 5001);

                module.BeginRun(new TransportRunDescriptor(nameof(KcpTransport), isServer: true, defaultRemoteEndPoint: remote));
                module.RecordPayloadReceived(remote, 32);
                var snapshot = module.CompleteRun();

                Assert.That(snapshot.ReportPath, Is.Null);
                Assert.That(snapshot.SummaryPath, Is.Null);
                Assert.That(Directory.Exists(reportDirectory), Is.False);
                Assert.That(consoleWriter.ToString(), Is.Empty);
            }
            finally
            {
                DeleteDirectory(reportDirectory);
            }
        }

        [Test]
        public void TransportMetricsDiagnosisFormatter_HighlightsReconnectAndBacklogRisks()
        {
            var snapshot = new TransportMetricsSnapshot
            {
                TransportName = nameof(KcpTransport),
                Mode = "server",
                DurationMs = 2400,
                AverageSmoothedRttMs = 188.5,
                PeakSmoothedRttMs = 320,
                TotalWaitSendCount = 9,
                TotalSendQueueCount = 4,
                TotalSendBufferCount = 2,
                TotalRetransmittedSegmentsInFlight = 3,
                TotalObservedRetransmissionSends = 7,
                SendErrors = 1,
                ErrorCountsByStage = new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["socket-send"] = 1
                },
                ApplicationSessionsTracked = 2,
                ApplicationSessionStateCounts = new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["LoggedIn"] = 1,
                    ["ReconnectPending"] = 1
                },
                ApplicationSessionSummaries = new List<TransportApplicationSessionSnapshot>
                {
                    new()
                    {
                        Scope = "server-host",
                        RemoteEndPoint = "127.0.0.1:5001",
                        ConnectionState = "ReconnectPending",
                        NextReconnectAtUtc = DateTimeOffset.UtcNow.AddSeconds(2)
                    },
                    new()
                    {
                        Scope = "server-host",
                        RemoteEndPoint = "127.0.0.1:5002",
                        ConnectionState = "LoggedIn",
                        CanSendHeartbeat = true,
                        LastRoundTripTimeMs = 188
                    }
                },
                SessionsWithDiagnostics = 2,
                PeerSummaries = new List<TransportPeerMetricsSnapshot>
                {
                    new()
                    {
                        RemoteEndPoint = "127.0.0.1:5001",
                        SessionLifecycleState = "active",
                        ObservedRetransmissionSends = 7,
                        SessionDiagnostics = new TransportSessionDiagnosticsSnapshot
                        {
                            WaitSendCount = 9
                        }
                    }
                }
            };

            var diagnosis = TransportMetricsDiagnosisFormatter.BuildChineseDiagnosis(snapshot);

            Assert.That(diagnosis, Does.Contain("已出现会话不稳定迹象"));
            Assert.That(diagnosis, Does.Contain("存在发送侧堆积迹象"));
            Assert.That(diagnosis, Does.Contain("存在重传迹象"));
            Assert.That(diagnosis, Does.Contain("ReconnectPending=1"));
        }

        [Test]
        public void TransportMetricsReportLocator_ReturnsMostRecentDiagnosisFile()
        {
            var reportDirectory = CreateReportDirectory();

            try
            {
                Directory.CreateDirectory(reportDirectory);
                var olderPath = Path.Combine(reportDirectory, "older.diagnosis.txt");
                var newerPath = Path.Combine(reportDirectory, "newer.diagnosis.txt");
                File.WriteAllText(olderPath, "older");
                File.WriteAllText(newerPath, "newer");
                File.SetLastWriteTimeUtc(olderPath, new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newerPath, new DateTime(2026, 3, 27, 0, 0, 5, DateTimeKind.Utc));

                var latestPath = TransportMetricsReportLocator.TryGetLatestDiagnosisPath(reportDirectory);
                var latestText = TransportMetricsReportLocator.ReadLatestDiagnosisText(reportDirectory);

                Assert.That(latestPath, Is.EqualTo(newerPath));
                Assert.That(latestText, Is.EqualTo("newer"));
            }
            finally
            {
                DeleteDirectory(reportDirectory);
            }
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

        private static string CreateReportDirectory()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Logs", "transport-metrics-tests", Guid.NewGuid().ToString("N"));
        }

        private static void DeleteDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
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
