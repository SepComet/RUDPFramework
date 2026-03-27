using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Network.NetworkTransport
{
    public interface ITransportMetricsModule
    {
        void BeginRun(TransportRunDescriptor descriptor);
        void RecordSessionOpened(IPEndPoint remoteEndPoint);
        void RecordSessionClosed(IPEndPoint remoteEndPoint);
        void RecordPayloadSent(IPEndPoint remoteEndPoint, int bytes);
        void RecordPayloadReceived(IPEndPoint remoteEndPoint, int bytes);
        void RecordDatagramSent(IPEndPoint remoteEndPoint, int bytes);
        void RecordDatagramReceived(IPEndPoint remoteEndPoint, int bytes);
        void RecordError(string stage, IPEndPoint remoteEndPoint, string detail = null);
        TransportMetricsSnapshot GetCurrentSnapshot();
        TransportMetricsSnapshot CompleteRun();
    }

    public sealed class TransportRunDescriptor
    {
        public TransportRunDescriptor(string transportName, bool isServer, IPEndPoint defaultRemoteEndPoint = null)
        {
            if (string.IsNullOrWhiteSpace(transportName))
            {
                throw new ArgumentException("Transport name is required.", nameof(transportName));
            }

            TransportName = transportName;
            IsServer = isServer;
            DefaultRemoteEndPoint = defaultRemoteEndPoint == null
                ? null
                : new IPEndPoint(defaultRemoteEndPoint.Address, defaultRemoteEndPoint.Port);
        }

        public string TransportName { get; }
        public bool IsServer { get; }
        public IPEndPoint DefaultRemoteEndPoint { get; }
    }

    [DataContract]
    public sealed class TransportMetricsSnapshot
    {
        [DataMember(Order = 1)] public string RunId { get; set; }
        [DataMember(Order = 2)] public string TransportName { get; set; }
        [DataMember(Order = 3)] public string Mode { get; set; }
        [DataMember(Order = 4)] public string DefaultRemoteEndPoint { get; set; }
        [DataMember(Order = 5)] public DateTimeOffset? StartedAtUtc { get; set; }
        [DataMember(Order = 6)] public DateTimeOffset? CompletedAtUtc { get; set; }
        [DataMember(Order = 7)] public long DurationMs { get; set; }
        [DataMember(Order = 8)] public string ReportPath { get; set; }
        [DataMember(Order = 9)] public int ActiveSessions { get; set; }
        [DataMember(Order = 10)] public int PeakActiveSessions { get; set; }
        [DataMember(Order = 11)] public long SessionsCreated { get; set; }
        [DataMember(Order = 12)] public long SessionsClosed { get; set; }
        [DataMember(Order = 13)] public long PayloadMessagesSent { get; set; }
        [DataMember(Order = 14)] public long PayloadBytesSent { get; set; }
        [DataMember(Order = 15)] public long PayloadMessagesReceived { get; set; }
        [DataMember(Order = 16)] public long PayloadBytesReceived { get; set; }
        [DataMember(Order = 17)] public long DatagramsSent { get; set; }
        [DataMember(Order = 18)] public long DatagramBytesSent { get; set; }
        [DataMember(Order = 19)] public long DatagramsReceived { get; set; }
        [DataMember(Order = 20)] public long DatagramBytesReceived { get; set; }
        [DataMember(Order = 21)] public long SendErrors { get; set; }
        [DataMember(Order = 22)] public long ReceiveErrors { get; set; }
        [DataMember(Order = 23)] public long OtherErrors { get; set; }
        [DataMember(Order = 24)] public Dictionary<string, long> ErrorCountsByStage { get; set; } = new(StringComparer.Ordinal);
        [DataMember(Order = 25)] public List<TransportPeerMetricsSnapshot> PeerSummaries { get; set; } = new();
    }

    [DataContract]
    public sealed class TransportPeerMetricsSnapshot
    {
        [DataMember(Order = 1)] public string RemoteEndPoint { get; set; }
        [DataMember(Order = 2)] public DateTimeOffset? FirstSeenUtc { get; set; }
        [DataMember(Order = 3)] public DateTimeOffset? LastActivityUtc { get; set; }
        [DataMember(Order = 4)] public long SessionOpens { get; set; }
        [DataMember(Order = 5)] public long SessionCloses { get; set; }
        [DataMember(Order = 6)] public long PayloadMessagesSent { get; set; }
        [DataMember(Order = 7)] public long PayloadBytesSent { get; set; }
        [DataMember(Order = 8)] public long PayloadMessagesReceived { get; set; }
        [DataMember(Order = 9)] public long PayloadBytesReceived { get; set; }
        [DataMember(Order = 10)] public long DatagramsSent { get; set; }
        [DataMember(Order = 11)] public long DatagramBytesSent { get; set; }
        [DataMember(Order = 12)] public long DatagramsReceived { get; set; }
        [DataMember(Order = 13)] public long DatagramBytesReceived { get; set; }
        [DataMember(Order = 14)] public Dictionary<string, long> ErrorCountsByStage { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class DefaultTransportMetricsModule : ITransportMetricsModule
    {
        private readonly object gate = new();
        private readonly Func<DateTimeOffset> utcNowProvider;
        private readonly TextWriter consoleWriter;
        private readonly string reportDirectory;
        private readonly Dictionary<string, PeerAccumulator> peers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> errorCountsByStage = new(StringComparer.Ordinal);

        private string runId;
        private string transportName;
        private string mode;
        private string defaultRemoteEndPoint;
        private DateTimeOffset? startedAtUtc;
        private DateTimeOffset? completedAtUtc;
        private string reportPath;
        private bool hasRun;
        private bool completed;
        private long sessionsCreated;
        private long sessionsClosed;
        private int activeSessions;
        private int peakActiveSessions;
        private long payloadMessagesSent;
        private long payloadBytesSent;
        private long payloadMessagesReceived;
        private long payloadBytesReceived;
        private long datagramsSent;
        private long datagramBytesSent;
        private long datagramsReceived;
        private long datagramBytesReceived;
        private long sendErrors;
        private long receiveErrors;
        private long otherErrors;
        private TransportMetricsSnapshot completedSnapshot;

        public DefaultTransportMetricsModule(string reportDirectory = null, Func<DateTimeOffset> utcNowProvider = null, TextWriter consoleWriter = null)
        {
            this.reportDirectory = string.IsNullOrWhiteSpace(reportDirectory)
                ? Path.Combine(Directory.GetCurrentDirectory(), "Logs", "transport-metrics")
                : reportDirectory;
            this.utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
            this.consoleWriter = consoleWriter ?? Console.Out;
        }

        public void BeginRun(TransportRunDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            lock (gate)
            {
                peers.Clear();
                errorCountsByStage.Clear();
                runId = Guid.NewGuid().ToString("N");
                transportName = descriptor.TransportName;
                mode = descriptor.IsServer ? "server" : "client";
                defaultRemoteEndPoint = FormatEndPoint(descriptor.DefaultRemoteEndPoint);
                startedAtUtc = utcNowProvider();
                completedAtUtc = null;
                reportPath = null;
                hasRun = true;
                completed = false;
                sessionsCreated = 0;
                sessionsClosed = 0;
                activeSessions = 0;
                peakActiveSessions = 0;
                payloadMessagesSent = 0;
                payloadBytesSent = 0;
                payloadMessagesReceived = 0;
                payloadBytesReceived = 0;
                datagramsSent = 0;
                datagramBytesSent = 0;
                datagramsReceived = 0;
                datagramBytesReceived = 0;
                sendErrors = 0;
                receiveErrors = 0;
                otherErrors = 0;
                completedSnapshot = null;
            }
        }

        public void RecordSessionOpened(IPEndPoint remoteEndPoint) => Update(remoteEndPoint, peer =>
        {
            sessionsCreated++;
            activeSessions++;
            peakActiveSessions = Math.Max(peakActiveSessions, activeSessions);
            peer.SessionOpens++;
        });

        public void RecordSessionClosed(IPEndPoint remoteEndPoint) => Update(remoteEndPoint, peer =>
        {
            sessionsClosed++;
            activeSessions = Math.Max(0, activeSessions - 1);
            peer.SessionCloses++;
        });

        public void RecordPayloadSent(IPEndPoint remoteEndPoint, int bytes) => Update(remoteEndPoint, peer =>
        {
            payloadMessagesSent++;
            payloadBytesSent += bytes;
            peer.PayloadMessagesSent++;
            peer.PayloadBytesSent += bytes;
        });

        public void RecordPayloadReceived(IPEndPoint remoteEndPoint, int bytes) => Update(remoteEndPoint, peer =>
        {
            payloadMessagesReceived++;
            payloadBytesReceived += bytes;
            peer.PayloadMessagesReceived++;
            peer.PayloadBytesReceived += bytes;
        });

        public void RecordDatagramSent(IPEndPoint remoteEndPoint, int bytes) => Update(remoteEndPoint, peer =>
        {
            datagramsSent++;
            datagramBytesSent += bytes;
            peer.DatagramsSent++;
            peer.DatagramBytesSent += bytes;
        });

        public void RecordDatagramReceived(IPEndPoint remoteEndPoint, int bytes) => Update(remoteEndPoint, peer =>
        {
            datagramsReceived++;
            datagramBytesReceived += bytes;
            peer.DatagramsReceived++;
            peer.DatagramBytesReceived += bytes;
        });

        public void RecordError(string stage, IPEndPoint remoteEndPoint, string detail = null)
        {
            lock (gate)
            {
                if (!hasRun)
                {
                    return;
                }

                Increment(errorCountsByStage, stage);
                if (remoteEndPoint != null)
                {
                    var peer = GetOrCreatePeer(remoteEndPoint);
                    Increment(peer.ErrorCountsByStage, stage);
                    peer.LastActivityUtc = utcNowProvider();
                }

                if (!string.IsNullOrWhiteSpace(stage) && stage.IndexOf("send", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sendErrors++;
                }
                else if (!string.IsNullOrWhiteSpace(stage) && (stage.IndexOf("receive", StringComparison.OrdinalIgnoreCase) >= 0 || stage.IndexOf("recv", StringComparison.OrdinalIgnoreCase) >= 0 || stage.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    receiveErrors++;
                }
                else
                {
                    otherErrors++;
                }

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    consoleWriter.WriteLine($"[TransportMetrics] {stage}: {detail}");
                }
            }
        }

        public TransportMetricsSnapshot GetCurrentSnapshot()
        {
            lock (gate)
            {
                return completed && completedSnapshot != null ? completedSnapshot : BuildSnapshot();
            }
        }

        public TransportMetricsSnapshot CompleteRun()
        {
            lock (gate)
            {
                if (!hasRun)
                {
                    return completedSnapshot ?? new TransportMetricsSnapshot();
                }

                if (completed && completedSnapshot != null)
                {
                    return completedSnapshot;
                }

                completedAtUtc = utcNowProvider();
                completed = true;
                completedSnapshot = BuildSnapshot();
                completedSnapshot.ReportPath = WriteJsonReport(completedSnapshot);
                reportPath = completedSnapshot.ReportPath;
                consoleWriter.WriteLine($"[TransportMetrics] {completedSnapshot.TransportName} mode={completedSnapshot.Mode} run={completedSnapshot.RunId} durationMs={completedSnapshot.DurationMs} peak={completedSnapshot.PeakActiveSessions} payloadTx={completedSnapshot.PayloadMessagesSent}/{completedSnapshot.PayloadBytesSent}B payloadRx={completedSnapshot.PayloadMessagesReceived}/{completedSnapshot.PayloadBytesReceived}B datagramTx={completedSnapshot.DatagramsSent}/{completedSnapshot.DatagramBytesSent}B datagramRx={completedSnapshot.DatagramsReceived}/{completedSnapshot.DatagramBytesReceived}B errors={completedSnapshot.SendErrors + completedSnapshot.ReceiveErrors + completedSnapshot.OtherErrors} report={completedSnapshot.ReportPath ?? "none"}");
                return completedSnapshot;
            }
        }

        private void Update(IPEndPoint remoteEndPoint, Action<PeerAccumulator> update)
        {
            lock (gate)
            {
                if (!hasRun)
                {
                    return;
                }

                var peer = GetOrCreatePeer(remoteEndPoint);
                update(peer);
                peer.LastActivityUtc = utcNowProvider();
            }
        }

        private TransportMetricsSnapshot BuildSnapshot()
        {
            var end = completedAtUtc ?? utcNowProvider();
            return new TransportMetricsSnapshot
            {
                RunId = runId,
                TransportName = transportName,
                Mode = mode,
                DefaultRemoteEndPoint = defaultRemoteEndPoint,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                DurationMs = startedAtUtc.HasValue ? Math.Max(0L, (long)(end - startedAtUtc.Value).TotalMilliseconds) : 0L,
                ReportPath = reportPath,
                ActiveSessions = activeSessions,
                PeakActiveSessions = peakActiveSessions,
                SessionsCreated = sessionsCreated,
                SessionsClosed = sessionsClosed,
                PayloadMessagesSent = payloadMessagesSent,
                PayloadBytesSent = payloadBytesSent,
                PayloadMessagesReceived = payloadMessagesReceived,
                PayloadBytesReceived = payloadBytesReceived,
                DatagramsSent = datagramsSent,
                DatagramBytesSent = datagramBytesSent,
                DatagramsReceived = datagramsReceived,
                DatagramBytesReceived = datagramBytesReceived,
                SendErrors = sendErrors,
                ReceiveErrors = receiveErrors,
                OtherErrors = otherErrors,
                ErrorCountsByStage = errorCountsByStage.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                PeerSummaries = peers.Values.Select(peer => peer.ToSnapshot()).OrderBy(peer => peer.RemoteEndPoint, StringComparer.Ordinal).ToList()
            };
        }

        private string WriteJsonReport(TransportMetricsSnapshot snapshot)
        {
            try
            {
                Directory.CreateDirectory(reportDirectory);
                var timestamp = (snapshot.CompletedAtUtc ?? utcNowProvider()).ToString("yyyyMMdd-HHmmssfff");
                var filePath = Path.Combine(reportDirectory, $"{snapshot.Mode}-{snapshot.TransportName}-{timestamp}-{snapshot.RunId}.json");
                using var stream = new MemoryStream();
                var serializer = new DataContractJsonSerializer(typeof(TransportMetricsSnapshot));
                serializer.WriteObject(stream, snapshot);
                var json = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(filePath, FormatJson(json), new UTF8Encoding(false));
                return filePath;
            }
            catch (Exception exception)
            {
                consoleWriter.WriteLine($"[TransportMetrics] Failed to write report: {exception.Message}");
                return null;
            }
        }

        private PeerAccumulator GetOrCreatePeer(IPEndPoint remoteEndPoint)
        {
            var key = FormatEndPoint(remoteEndPoint) ?? "unknown";
            if (!peers.TryGetValue(key, out var peer))
            {
                peer = new PeerAccumulator(key, utcNowProvider());
                peers.Add(key, peer);
            }

            return peer;
        }

        private static string FormatEndPoint(IPEndPoint remoteEndPoint)
        {
            return remoteEndPoint == null ? null : new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port).ToString();
        }

        private static void Increment(IDictionary<string, long> counts, string stage)
        {
            var key = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
            counts.TryGetValue(key, out var current);
            counts[key] = current + 1;
        }

        private static string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }

            var builder = new StringBuilder(json.Length + 128);
            var indentation = 0;
            var inString = false;
            var isEscaped = false;

            foreach (var character in json)
            {
                if (isEscaped)
                {
                    builder.Append(character);
                    isEscaped = false;
                    continue;
                }

                if (character == '\\' && inString)
                {
                    builder.Append(character);
                    isEscaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inString = !inString;
                    builder.Append(character);
                    continue;
                }

                if (inString)
                {
                    builder.Append(character);
                    continue;
                }

                switch (character)
                {
                    case '{':
                    case '[':
                        builder.Append(character);
                        builder.AppendLine();
                        indentation++;
                        builder.Append(new string(' ', indentation * 2));
                        break;
                    case '}':
                    case ']':
                        builder.AppendLine();
                        indentation = Math.Max(0, indentation - 1);
                        builder.Append(new string(' ', indentation * 2));
                        builder.Append(character);
                        break;
                    case ',':
                        builder.Append(character);
                        builder.AppendLine();
                        builder.Append(new string(' ', indentation * 2));
                        break;
                    case ':':
                        builder.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(character))
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        private sealed class PeerAccumulator
        {
            public PeerAccumulator(string remoteEndPoint, DateTimeOffset now)
            {
                RemoteEndPoint = remoteEndPoint;
                FirstSeenUtc = now;
                LastActivityUtc = now;
            }

            public string RemoteEndPoint { get; }
            public DateTimeOffset FirstSeenUtc { get; }
            public DateTimeOffset LastActivityUtc { get; set; }
            public long SessionOpens { get; set; }
            public long SessionCloses { get; set; }
            public long PayloadMessagesSent { get; set; }
            public long PayloadBytesSent { get; set; }
            public long PayloadMessagesReceived { get; set; }
            public long PayloadBytesReceived { get; set; }
            public long DatagramsSent { get; set; }
            public long DatagramBytesSent { get; set; }
            public long DatagramsReceived { get; set; }
            public long DatagramBytesReceived { get; set; }
            public Dictionary<string, long> ErrorCountsByStage { get; } = new(StringComparer.Ordinal);

            public TransportPeerMetricsSnapshot ToSnapshot()
            {
                return new TransportPeerMetricsSnapshot
                {
                    RemoteEndPoint = RemoteEndPoint,
                    FirstSeenUtc = FirstSeenUtc,
                    LastActivityUtc = LastActivityUtc,
                    SessionOpens = SessionOpens,
                    SessionCloses = SessionCloses,
                    PayloadMessagesSent = PayloadMessagesSent,
                    PayloadBytesSent = PayloadBytesSent,
                    PayloadMessagesReceived = PayloadMessagesReceived,
                    PayloadBytesReceived = PayloadBytesReceived,
                    DatagramsSent = DatagramsSent,
                    DatagramBytesSent = DatagramBytesSent,
                    DatagramsReceived = DatagramsReceived,
                    DatagramBytesReceived = DatagramBytesReceived,
                    ErrorCountsByStage = ErrorCountsByStage.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                };
            }
        }
    }
}
