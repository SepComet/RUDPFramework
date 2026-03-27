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
        void RecordSessionDiagnostics(IPEndPoint remoteEndPoint, TransportSessionDiagnosticsSnapshot diagnostics);
        void RecordApplicationSessionSnapshot(TransportApplicationSessionSnapshot snapshot);
        void RecordPayloadSent(IPEndPoint remoteEndPoint, int bytes);
        void RecordPayloadReceived(IPEndPoint remoteEndPoint, int bytes);
        void RecordDatagramSent(IPEndPoint remoteEndPoint, int bytes);
        void RecordDatagramReceived(IPEndPoint remoteEndPoint, int bytes);
        void RecordError(string stage, IPEndPoint remoteEndPoint, string detail = null);
        TransportMetricsSnapshot GetCurrentSnapshot();
        TransportMetricsSnapshot CompleteRun();
    }

    public interface ITransportMetricsSink
    {
        void RecordApplicationSessionSnapshot(TransportApplicationSessionSnapshot snapshot);
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
        [DataMember(Order = 9)] public string SummaryPath { get; set; }
        [DataMember(Order = 10)] public TransportMetricsReadableSummary ReadableSummary { get; set; } = new();
        [DataMember(Order = 11)] public int ActiveSessions { get; set; }
        [DataMember(Order = 12)] public int PeakActiveSessions { get; set; }
        [DataMember(Order = 13)] public long SessionsCreated { get; set; }
        [DataMember(Order = 14)] public long SessionsClosed { get; set; }
        [DataMember(Order = 15)] public long PayloadMessagesSent { get; set; }
        [DataMember(Order = 16)] public long PayloadBytesSent { get; set; }
        [DataMember(Order = 17)] public long PayloadMessagesReceived { get; set; }
        [DataMember(Order = 18)] public long PayloadBytesReceived { get; set; }
        [DataMember(Order = 19)] public long DatagramsSent { get; set; }
        [DataMember(Order = 20)] public long DatagramBytesSent { get; set; }
        [DataMember(Order = 21)] public long DatagramsReceived { get; set; }
        [DataMember(Order = 22)] public long DatagramBytesReceived { get; set; }
        [DataMember(Order = 23)] public long SendErrors { get; set; }
        [DataMember(Order = 24)] public long ReceiveErrors { get; set; }
        [DataMember(Order = 25)] public long OtherErrors { get; set; }
        [DataMember(Order = 26)] public int SessionsWithDiagnostics { get; set; }
        [DataMember(Order = 27)] public double AverageSmoothedRttMs { get; set; }
        [DataMember(Order = 28)] public int PeakSmoothedRttMs { get; set; }
        [DataMember(Order = 29)] public double AverageRetransmissionTimeoutMs { get; set; }
        [DataMember(Order = 30)] public int PeakRetransmissionTimeoutMs { get; set; }
        [DataMember(Order = 31)] public long TotalWaitSendCount { get; set; }
        [DataMember(Order = 32)] public long PeakWaitSendCount { get; set; }
        [DataMember(Order = 33)] public long TotalSendQueueCount { get; set; }
        [DataMember(Order = 34)] public long TotalSendBufferCount { get; set; }
        [DataMember(Order = 35)] public long TotalReceiveQueueCount { get; set; }
        [DataMember(Order = 36)] public long TotalReceiveBufferCount { get; set; }
        [DataMember(Order = 37)] public long TotalRetransmittedSegmentsInFlight { get; set; }
        [DataMember(Order = 38)] public long PeakRetransmittedSegmentsInFlight { get; set; }
        [DataMember(Order = 39)] public long TotalObservedRetransmissionSends { get; set; }
        [DataMember(Order = 40)] public long TotalObservedLossSignals { get; set; }
        [DataMember(Order = 41)] public Dictionary<string, long> SessionStateCounts { get; set; } = new(StringComparer.Ordinal);
        [DataMember(Order = 42)] public int ApplicationSessionsTracked { get; set; }
        [DataMember(Order = 43)] public Dictionary<string, long> ApplicationSessionStateCounts { get; set; } = new(StringComparer.Ordinal);
        [DataMember(Order = 44)] public List<TransportApplicationSessionSnapshot> ApplicationSessionSummaries { get; set; } = new();
        [DataMember(Order = 45)] public Dictionary<string, long> ErrorCountsByStage { get; set; } = new(StringComparer.Ordinal);
        [DataMember(Order = 46)] public List<TransportPeerMetricsSnapshot> PeerSummaries { get; set; } = new();
    }

    [DataContract]
    public sealed class TransportMetricsReadableSummary
    {
        [DataMember(Order = 1)] public string Headline { get; set; }
        [DataMember(Order = 2)] public string SessionSummary { get; set; }
        [DataMember(Order = 3)] public string TrafficSummary { get; set; }
        [DataMember(Order = 4)] public string ErrorSummary { get; set; }
        [DataMember(Order = 5)] public string LifecycleSummary { get; set; }
        [DataMember(Order = 6)] public string HealthSummary { get; set; }
        [DataMember(Order = 7)] public List<string> TopPeerHighlights { get; set; } = new();
    }

    [DataContract]
    public sealed class TransportApplicationSessionSnapshot
    {
        [DataMember(Order = 1)] public string Scope { get; set; }
        [DataMember(Order = 2)] public string RemoteEndPoint { get; set; }
        [DataMember(Order = 3)] public string ConnectionState { get; set; }
        [DataMember(Order = 4)] public bool CanSendHeartbeat { get; set; }
        [DataMember(Order = 5)] public long? LastRoundTripTimeMs { get; set; }
        [DataMember(Order = 6)] public string LastFailureReason { get; set; }
        [DataMember(Order = 7)] public DateTimeOffset? LastLivenessUtc { get; set; }
        [DataMember(Order = 8)] public DateTimeOffset? LastHeartbeatSentUtc { get; set; }
        [DataMember(Order = 9)] public DateTimeOffset? NextReconnectAtUtc { get; set; }
        [DataMember(Order = 10)] public long? CurrentServerTick { get; set; }
        [DataMember(Order = 11)] public DateTimeOffset? ObservedAtUtc { get; set; }
    }

    [DataContract]
    public sealed class TransportSessionDiagnosticsSnapshot
    {
        [DataMember(Order = 1)] public string LifecycleState { get; set; }
        [DataMember(Order = 2)] public DateTimeOffset? ObservedAtUtc { get; set; }
        [DataMember(Order = 3)] public long IdleMs { get; set; }
        [DataMember(Order = 4)] public int KcpStateCode { get; set; }
        [DataMember(Order = 5)] public int SmoothedRttMs { get; set; }
        [DataMember(Order = 6)] public int RttVarianceMs { get; set; }
        [DataMember(Order = 7)] public int RetransmissionTimeoutMs { get; set; }
        [DataMember(Order = 8)] public int LocalSendWindow { get; set; }
        [DataMember(Order = 9)] public int LocalReceiveWindow { get; set; }
        [DataMember(Order = 10)] public int RemoteWindow { get; set; }
        [DataMember(Order = 11)] public int CongestionWindow { get; set; }
        [DataMember(Order = 12)] public int WaitSendCount { get; set; }
        [DataMember(Order = 13)] public int SendQueueCount { get; set; }
        [DataMember(Order = 14)] public int SendBufferCount { get; set; }
        [DataMember(Order = 15)] public int ReceiveQueueCount { get; set; }
        [DataMember(Order = 16)] public int ReceiveBufferCount { get; set; }
        [DataMember(Order = 17)] public int DeadLinkThreshold { get; set; }
        [DataMember(Order = 18)] public long SegmentTransmitCount { get; set; }
        [DataMember(Order = 19)] public int RetransmittedSegmentsInFlight { get; set; }
        [DataMember(Order = 20)] public long ObservedRetransmissionSends { get; set; }
        [DataMember(Order = 21)] public long ObservedLossSignals { get; set; }
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
        [DataMember(Order = 14)] public string SessionLifecycleState { get; set; }
        [DataMember(Order = 15)] public TransportSessionDiagnosticsSnapshot SessionDiagnostics { get; set; } = new();
        [DataMember(Order = 16)] public int PeakSmoothedRttMs { get; set; }
        [DataMember(Order = 17)] public int PeakRetransmissionTimeoutMs { get; set; }
        [DataMember(Order = 18)] public int PeakWaitSendCount { get; set; }
        [DataMember(Order = 19)] public int PeakRetransmittedSegmentsInFlight { get; set; }
        [DataMember(Order = 20)] public long ObservedRetransmissionSends { get; set; }
        [DataMember(Order = 21)] public long ObservedLossSignals { get; set; }
        [DataMember(Order = 22)] public Dictionary<string, long> ErrorCountsByStage { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class DefaultTransportMetricsModule : ITransportMetricsModule
    {
        private readonly object gate = new();
        private readonly Func<DateTimeOffset> utcNowProvider;
        private readonly TextWriter consoleWriter;
        private readonly string reportDirectory;
        private readonly bool writeJsonReport;
        private readonly bool writeTextSummaryReport;
        private readonly bool writeDiagnosisReport;
        private readonly bool emitConsoleSummary;
        private readonly int maxPeerSummariesInTextReport;
        private readonly int maxPeerSummariesInConsole;
        private readonly Dictionary<string, PeerAccumulator> peers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TransportApplicationSessionSnapshot> applicationSessions = new(StringComparer.Ordinal);
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
            : this(
                new TransportMetricsOptions
                {
                    ReportDirectory = reportDirectory,
                    ConsoleWriter = consoleWriter
                },
                utcNowProvider)
        {
        }

        public DefaultTransportMetricsModule(TransportMetricsOptions options, Func<DateTimeOffset> utcNowProvider = null)
        {
            options ??= TransportMetricsOptions.Default;
            reportDirectory = options.ReportDirectory;
            this.reportDirectory = string.IsNullOrWhiteSpace(reportDirectory)
                ? Path.Combine(Directory.GetCurrentDirectory(), "Logs", "transport-metrics")
                : reportDirectory;
            this.utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
            consoleWriter = options.ConsoleWriter;
            this.consoleWriter = consoleWriter ?? Console.Out;
            writeJsonReport = options.WriteJsonReport;
            writeTextSummaryReport = options.WriteTextSummaryReport;
            writeDiagnosisReport = options.WriteDiagnosisReport;
            emitConsoleSummary = options.EmitConsoleSummary;
            maxPeerSummariesInTextReport = Math.Max(0, options.MaxPeerSummariesInTextReport);
            maxPeerSummariesInConsole = Math.Max(0, options.MaxPeerSummariesInConsole);
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
                applicationSessions.Clear();
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
            peer.SessionLifecycleState = "active";
        });

        public void RecordSessionClosed(IPEndPoint remoteEndPoint) => Update(remoteEndPoint, peer =>
        {
            sessionsClosed++;
            activeSessions = Math.Max(0, activeSessions - 1);
            peer.SessionCloses++;
            peer.SessionLifecycleState = "closed";
        });

        public void RecordSessionDiagnostics(IPEndPoint remoteEndPoint, TransportSessionDiagnosticsSnapshot diagnostics) => Update(remoteEndPoint, peer =>
        {
            if (diagnostics == null)
            {
                return;
            }

            peer.RecordDiagnostics(diagnostics);
        });

        public void RecordApplicationSessionSnapshot(TransportApplicationSessionSnapshot snapshot)
        {
            lock (gate)
            {
                if (!hasRun || snapshot == null)
                {
                    return;
                }

                var key = BuildApplicationSessionKey(snapshot.Scope, snapshot.RemoteEndPoint);
                applicationSessions[key] = Clone(snapshot);
            }
        }

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
                completedSnapshot.SummaryPath = WriteTextSummaryReport(completedSnapshot);
                WriteDiagnosisReport(completedSnapshot);
                reportPath = completedSnapshot.ReportPath;
                if (emitConsoleSummary)
                {
                    WriteConsoleSummary(completedSnapshot);
                }

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
            var peerSnapshots = peers.Values.Select(peer => peer.ToSnapshot()).OrderBy(peer => peer.RemoteEndPoint, StringComparer.Ordinal).ToList();
            var peersWithDiagnostics = peerSnapshots.Where(peer => peer.SessionDiagnostics?.ObservedAtUtc != null).ToList();
            var averageSmoothedRttMs = peersWithDiagnostics.Count == 0
                ? 0d
                : peersWithDiagnostics.Average(peer => peer.SessionDiagnostics.SmoothedRttMs);
            var averageRetransmissionTimeoutMs = peersWithDiagnostics.Count == 0
                ? 0d
                : peersWithDiagnostics.Average(peer => peer.SessionDiagnostics.RetransmissionTimeoutMs);
            var sessionStateCounts = peerSnapshots
                .GroupBy(peer => string.IsNullOrWhiteSpace(peer.SessionLifecycleState) ? "unknown" : peer.SessionLifecycleState, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.Ordinal);
            var applicationSessionSnapshots = applicationSessions.Values
                .Select(Clone)
                .OrderBy(snapshot => snapshot.Scope, StringComparer.Ordinal)
                .ThenBy(snapshot => snapshot.RemoteEndPoint, StringComparer.Ordinal)
                .ToList();
            var applicationSessionStateCounts = applicationSessionSnapshots
                .GroupBy(snapshot => string.IsNullOrWhiteSpace(snapshot.ConnectionState) ? "unknown" : snapshot.ConnectionState, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.Ordinal);

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
                SummaryPath = null,
                ReadableSummary = BuildReadableSummary(),
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
                SessionsWithDiagnostics = peersWithDiagnostics.Count,
                AverageSmoothedRttMs = averageSmoothedRttMs,
                PeakSmoothedRttMs = peerSnapshots.Count == 0 ? 0 : peerSnapshots.Max(peer => peer.PeakSmoothedRttMs),
                AverageRetransmissionTimeoutMs = averageRetransmissionTimeoutMs,
                PeakRetransmissionTimeoutMs = peerSnapshots.Count == 0 ? 0 : peerSnapshots.Max(peer => peer.PeakRetransmissionTimeoutMs),
                TotalWaitSendCount = peerSnapshots.Sum(peer => (long)peer.SessionDiagnostics.WaitSendCount),
                PeakWaitSendCount = peerSnapshots.Count == 0 ? 0 : peerSnapshots.Max(peer => (long)peer.PeakWaitSendCount),
                TotalSendQueueCount = peerSnapshots.Sum(peer => (long)peer.SessionDiagnostics.SendQueueCount),
                TotalSendBufferCount = peerSnapshots.Sum(peer => (long)peer.SessionDiagnostics.SendBufferCount),
                TotalReceiveQueueCount = peerSnapshots.Sum(peer => (long)peer.SessionDiagnostics.ReceiveQueueCount),
                TotalReceiveBufferCount = peerSnapshots.Sum(peer => (long)peer.SessionDiagnostics.ReceiveBufferCount),
                TotalRetransmittedSegmentsInFlight = peerSnapshots.Sum(peer => (long)peer.SessionDiagnostics.RetransmittedSegmentsInFlight),
                PeakRetransmittedSegmentsInFlight = peerSnapshots.Count == 0 ? 0 : peerSnapshots.Max(peer => (long)peer.PeakRetransmittedSegmentsInFlight),
                TotalObservedRetransmissionSends = peerSnapshots.Sum(peer => peer.ObservedRetransmissionSends),
                TotalObservedLossSignals = peerSnapshots.Sum(peer => peer.ObservedLossSignals),
                SessionStateCounts = sessionStateCounts,
                ApplicationSessionsTracked = applicationSessionSnapshots.Count,
                ApplicationSessionStateCounts = applicationSessionStateCounts,
                ApplicationSessionSummaries = applicationSessionSnapshots,
                ErrorCountsByStage = errorCountsByStage.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                PeerSummaries = peerSnapshots
            };
        }

        private TransportMetricsReadableSummary BuildReadableSummary()
        {
            var topPeers = peers.Values
                .OrderByDescending(peer => peer.PayloadBytesSent + peer.PayloadBytesReceived)
                .ThenBy(peer => peer.RemoteEndPoint, StringComparer.Ordinal)
                .Take(Math.Max(maxPeerSummariesInTextReport, maxPeerSummariesInConsole))
                .Select(peer => $"{peer.RemoteEndPoint}: payload={peer.PayloadMessagesSent + peer.PayloadMessagesReceived} msgs, datagram={peer.DatagramsSent + peer.DatagramsReceived} packets, errors={peer.ErrorCountsByStage.Values.Sum()}")
                .ToList();

            var totalErrors = sendErrors + receiveErrors + otherErrors;
            var busiestErrorStage = errorCountsByStage.Count == 0
                ? "none"
                : errorCountsByStage.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).First().Key;

            return new TransportMetricsReadableSummary
            {
                Headline = $"{transportName} {mode} run {runId} finished in {Math.Max(0L, startedAtUtc.HasValue ? (long)((completedAtUtc ?? utcNowProvider()) - startedAtUtc.Value).TotalMilliseconds : 0L)} ms.",
                SessionSummary = $"Sessions active={activeSessions}, peak={peakActiveSessions}, opened={sessionsCreated}, closed={sessionsClosed}, peers={peers.Count}.",
                TrafficSummary = $"Payload tx/rx={payloadMessagesSent}/{payloadMessagesReceived} msgs ({payloadBytesSent}/{payloadBytesReceived} B), datagram tx/rx={datagramsSent}/{datagramsReceived} ({datagramBytesSent}/{datagramBytesReceived} B).",
                ErrorSummary = totalErrors == 0
                    ? "No transport errors were recorded."
                    : $"Errors total={totalErrors}, send={sendErrors}, receive={receiveErrors}, other={otherErrors}, busiestStage={busiestErrorStage}.",
                LifecycleSummary = BuildLifecycleSummary(),
                HealthSummary = BuildHealthSummary(),
                TopPeerHighlights = topPeers
            };
        }

        private string BuildLifecycleSummary()
        {
            if (applicationSessions.Count == 0)
            {
                return "No shared session lifecycle snapshots were captured.";
            }

            var stateSummary = string.Join(
                ", ",
                applicationSessions.Values
                    .GroupBy(snapshot => string.IsNullOrWhiteSpace(snapshot.ConnectionState) ? "unknown" : snapshot.ConnectionState, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key}={group.Count()}"));

            var heartbeatReady = applicationSessions.Values.Count(snapshot => snapshot.CanSendHeartbeat);
            var pendingReconnects = applicationSessions.Values.Count(snapshot => snapshot.NextReconnectAtUtc.HasValue);
            return $"Lifecycle tracked={applicationSessions.Count}, heartbeatReady={heartbeatReady}, reconnectPending={pendingReconnects}, states={stateSummary}.";
        }

        private string BuildHealthSummary()
        {
            var diagnosticsPeers = peers.Values.Where(peer => peer.SessionDiagnostics?.ObservedAtUtc != null).ToList();
            if (diagnosticsPeers.Count == 0)
            {
                return "No KCP session diagnostics were captured.";
            }

            var averageRtt = diagnosticsPeers.Average(peer => peer.SessionDiagnostics.SmoothedRttMs);
            var peakRtt = diagnosticsPeers.Max(peer => peer.PeakSmoothedRttMs);
            var totalWaitSend = diagnosticsPeers.Sum(peer => peer.SessionDiagnostics.WaitSendCount);
            var totalRetransmittedSegments = diagnosticsPeers.Sum(peer => peer.SessionDiagnostics.RetransmittedSegmentsInFlight);
            var totalObservedRetransmissions = diagnosticsPeers.Sum(peer => peer.ObservedRetransmissionSends);
            var stateSummary = string.Join(
                ", ",
                diagnosticsPeers
                    .GroupBy(peer => string.IsNullOrWhiteSpace(peer.SessionLifecycleState) ? "unknown" : peer.SessionLifecycleState, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key}={group.Count()}"));

            return $"Health avgRtt={averageRtt:F1} ms, peakRtt={peakRtt} ms, waitSnd={totalWaitSend}, retransInFlight={totalRetransmittedSegments}, observedRetransmissions={totalObservedRetransmissions}, states={stateSummary}.";
        }

        private string WriteJsonReport(TransportMetricsSnapshot snapshot)
        {
            if (!writeJsonReport)
            {
                return null;
            }

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

        private string WriteTextSummaryReport(TransportMetricsSnapshot snapshot)
        {
            if (!writeTextSummaryReport)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(reportDirectory);
                var timestamp = (snapshot.CompletedAtUtc ?? utcNowProvider()).ToString("yyyyMMdd-HHmmssfff");
                var filePath = Path.Combine(reportDirectory, $"{snapshot.Mode}-{snapshot.TransportName}-{timestamp}-{snapshot.RunId}.summary.txt");
                File.WriteAllText(filePath, BuildReadableSummaryText(snapshot), new UTF8Encoding(false));
                return filePath;
            }
            catch (Exception exception)
            {
                consoleWriter.WriteLine($"[TransportMetrics] Failed to write summary: {exception.Message}");
                return null;
            }
        }

        private string WriteDiagnosisReport(TransportMetricsSnapshot snapshot)
        {
            if (!writeDiagnosisReport)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(reportDirectory);
                var timestamp = (snapshot.CompletedAtUtc ?? utcNowProvider()).ToString("yyyyMMdd-HHmmssfff");
                var filePath = Path.Combine(reportDirectory, $"{snapshot.Mode}-{snapshot.TransportName}-{timestamp}-{snapshot.RunId}.diagnosis.txt");
                File.WriteAllText(filePath, TransportMetricsDiagnosisFormatter.BuildChineseDiagnosis(snapshot), new UTF8Encoding(false));
                return filePath;
            }
            catch (Exception exception)
            {
                consoleWriter.WriteLine($"[TransportMetrics] Failed to write diagnosis: {exception.Message}");
                return null;
            }
        }

        private void WriteConsoleSummary(TransportMetricsSnapshot snapshot)
        {
            consoleWriter.WriteLine("[TransportMetrics] English Summary");
            consoleWriter.WriteLine($"[TransportMetrics] Run: {snapshot.RunId}");
            consoleWriter.WriteLine($"[TransportMetrics] Transport: {snapshot.TransportName}");
            consoleWriter.WriteLine($"[TransportMetrics] Mode: {snapshot.Mode}");
            consoleWriter.WriteLine($"[TransportMetrics] StartedAtUtc: {snapshot.StartedAtUtc}");
            consoleWriter.WriteLine($"[TransportMetrics] CompletedAtUtc: {snapshot.CompletedAtUtc}");
            consoleWriter.WriteLine($"[TransportMetrics] DurationMs: {snapshot.DurationMs}");
            consoleWriter.WriteLine($"[TransportMetrics] JsonReport: {snapshot.ReportPath ?? "none"}");
            consoleWriter.WriteLine($"[TransportMetrics] SummaryReport: {snapshot.SummaryPath ?? "none"}");
            consoleWriter.WriteLine($"[TransportMetrics] {snapshot.ReadableSummary.Headline}");
            consoleWriter.WriteLine($"[TransportMetrics] {snapshot.ReadableSummary.SessionSummary}");
            consoleWriter.WriteLine($"[TransportMetrics] {snapshot.ReadableSummary.TrafficSummary}");
            consoleWriter.WriteLine($"[TransportMetrics] {snapshot.ReadableSummary.ErrorSummary}");
            consoleWriter.WriteLine($"[TransportMetrics] {snapshot.ReadableSummary.LifecycleSummary}");
            consoleWriter.WriteLine($"[TransportMetrics] {snapshot.ReadableSummary.HealthSummary}");
            consoleWriter.WriteLine("[TransportMetrics] Top Peers:");

            foreach (var line in snapshot.ReadableSummary.TopPeerHighlights.Take(maxPeerSummariesInConsole))
            {
                consoleWriter.WriteLine($"[TransportMetrics] Peer: {line}");
            }

            if (snapshot.ReadableSummary.TopPeerHighlights.Count == 0)
            {
                consoleWriter.WriteLine("[TransportMetrics] Peer: none");
            }

            consoleWriter.WriteLine("[TransportMetrics] Chinese Summary");
            consoleWriter.WriteLine($"[TransportMetrics] 运行ID: {snapshot.RunId}");
            consoleWriter.WriteLine($"[TransportMetrics] 传输实现: {snapshot.TransportName}");
            consoleWriter.WriteLine($"[TransportMetrics] 运行模式: {TranslateMode(snapshot.Mode)}");
            consoleWriter.WriteLine($"[TransportMetrics] 开始时间(UTC): {snapshot.StartedAtUtc}");
            consoleWriter.WriteLine($"[TransportMetrics] 结束时间(UTC): {snapshot.CompletedAtUtc}");
            consoleWriter.WriteLine($"[TransportMetrics] 总耗时(毫秒): {snapshot.DurationMs}");
            consoleWriter.WriteLine($"[TransportMetrics] Json报告: {snapshot.ReportPath ?? "无"}");
            consoleWriter.WriteLine($"[TransportMetrics] 摘要报告: {snapshot.SummaryPath ?? "无"}");
            consoleWriter.WriteLine($"[TransportMetrics] {BuildChineseHeadline(snapshot)}");
            consoleWriter.WriteLine($"[TransportMetrics] {BuildChineseSessionSummary(snapshot)}");
            consoleWriter.WriteLine($"[TransportMetrics] {BuildChineseTrafficSummary(snapshot)}");
            consoleWriter.WriteLine($"[TransportMetrics] {BuildChineseErrorSummary(snapshot)}");
            consoleWriter.WriteLine($"[TransportMetrics] {BuildChineseLifecycleSummary(snapshot)}");
            consoleWriter.WriteLine($"[TransportMetrics] {BuildChineseHealthSummary(snapshot)}");
            consoleWriter.WriteLine("[TransportMetrics] 重点对端:");

            foreach (var peer in snapshot.PeerSummaries
                         .OrderByDescending(item => item.PayloadBytesSent + item.PayloadBytesReceived)
                         .ThenBy(item => item.RemoteEndPoint, StringComparer.Ordinal)
                         .Take(maxPeerSummariesInConsole))
            {
                consoleWriter.WriteLine($"[TransportMetrics] 对端: {peer.RemoteEndPoint}: 业务消息={peer.PayloadMessagesSent + peer.PayloadMessagesReceived} 条, 数据报={peer.DatagramsSent + peer.DatagramsReceived} 个, 错误={peer.ErrorCountsByStage.Values.Sum()}");
            }

            if (snapshot.PeerSummaries.Count == 0)
            {
                consoleWriter.WriteLine("[TransportMetrics] 对端: 无");
            }
        }

        private string BuildReadableSummaryText(TransportMetricsSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Transport Metrics Summary");
            builder.AppendLine();
            builder.AppendLine("English Summary");
            builder.AppendLine();
            builder.AppendLine($"Run: {snapshot.RunId}");
            builder.AppendLine($"Transport: {snapshot.TransportName}");
            builder.AppendLine($"Mode: {snapshot.Mode}");
            builder.AppendLine($"StartedAtUtc: {snapshot.StartedAtUtc}");
            builder.AppendLine($"CompletedAtUtc: {snapshot.CompletedAtUtc}");
            builder.AppendLine($"DurationMs: {snapshot.DurationMs}");
            builder.AppendLine();
            builder.AppendLine(snapshot.ReadableSummary.Headline);
            builder.AppendLine(snapshot.ReadableSummary.SessionSummary);
            builder.AppendLine(snapshot.ReadableSummary.TrafficSummary);
            builder.AppendLine(snapshot.ReadableSummary.ErrorSummary);
            builder.AppendLine(snapshot.ReadableSummary.LifecycleSummary);
            builder.AppendLine(snapshot.ReadableSummary.HealthSummary);
            builder.AppendLine();
            builder.AppendLine("Top Peers:");

            foreach (var line in snapshot.ReadableSummary.TopPeerHighlights.Take(maxPeerSummariesInTextReport))
            {
                builder.AppendLine($"- {line}");
            }

            if (snapshot.ReadableSummary.TopPeerHighlights.Count == 0)
            {
                builder.AppendLine("- none");
            }

            builder.AppendLine();
            builder.AppendLine("Paths:");
            builder.AppendLine($"- JsonReport: {snapshot.ReportPath ?? "disabled"}");
            builder.AppendLine($"- SummaryReport: {snapshot.SummaryPath ?? "pending"}");
            builder.AppendLine();
            builder.AppendLine("Chinese Summary");
            builder.AppendLine();
            builder.AppendLine($"运行ID: {snapshot.RunId}");
            builder.AppendLine($"传输实现: {snapshot.TransportName}");
            builder.AppendLine($"运行模式: {TranslateMode(snapshot.Mode)}");
            builder.AppendLine($"开始时间(UTC): {snapshot.StartedAtUtc}");
            builder.AppendLine($"结束时间(UTC): {snapshot.CompletedAtUtc}");
            builder.AppendLine($"总耗时(毫秒): {snapshot.DurationMs}");
            builder.AppendLine();
            builder.AppendLine(BuildChineseHeadline(snapshot));
            builder.AppendLine(BuildChineseSessionSummary(snapshot));
            builder.AppendLine(BuildChineseTrafficSummary(snapshot));
            builder.AppendLine(BuildChineseErrorSummary(snapshot));
            builder.AppendLine(BuildChineseLifecycleSummary(snapshot));
            builder.AppendLine(BuildChineseHealthSummary(snapshot));
            builder.AppendLine();
            builder.AppendLine("重点对端:");

            foreach (var peer in snapshot.PeerSummaries
                         .OrderByDescending(item => item.PayloadBytesSent + item.PayloadBytesReceived)
                         .ThenBy(item => item.RemoteEndPoint, StringComparer.Ordinal)
                         .Take(maxPeerSummariesInTextReport))
            {
                builder.AppendLine($"- {peer.RemoteEndPoint}: 业务消息={peer.PayloadMessagesSent + peer.PayloadMessagesReceived} 条, 数据报={peer.DatagramsSent + peer.DatagramsReceived} 个, 错误={peer.ErrorCountsByStage.Values.Sum()}");
            }

            if (snapshot.PeerSummaries.Count == 0)
            {
                builder.AppendLine("- 无");
            }

            builder.AppendLine();
            builder.AppendLine("文件路径:");
            builder.AppendLine($"- Json报告: {snapshot.ReportPath ?? "已禁用"}");
            builder.AppendLine($"- 摘要报告: {snapshot.SummaryPath ?? "待写入"}");
            return builder.ToString();
        }

        private static string TranslateMode(string mode)
        {
            return string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase)
                ? "server / 服务端"
                : string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase)
                    ? "client / 客户端"
                    : mode ?? "unknown / 未知";
        }

        private static string BuildChineseHeadline(TransportMetricsSnapshot snapshot)
        {
            return $"{snapshot.TransportName} {TranslateMode(snapshot.Mode)} 运行已完成，总耗时 {snapshot.DurationMs} 毫秒。";
        }

        private static string BuildChineseSessionSummary(TransportMetricsSnapshot snapshot)
        {
            return $"会话统计：当前活跃={snapshot.ActiveSessions}，峰值={snapshot.PeakActiveSessions}，建立={snapshot.SessionsCreated}，关闭={snapshot.SessionsClosed}，对端数={snapshot.PeerSummaries.Count}。";
        }

        private static string BuildChineseTrafficSummary(TransportMetricsSnapshot snapshot)
        {
            return $"流量统计：业务消息发送/接收={snapshot.PayloadMessagesSent}/{snapshot.PayloadMessagesReceived} 条（{snapshot.PayloadBytesSent}/{snapshot.PayloadBytesReceived} B），数据报发送/接收={snapshot.DatagramsSent}/{snapshot.DatagramsReceived} 个（{snapshot.DatagramBytesSent}/{snapshot.DatagramBytesReceived} B）。";
        }

        private static string BuildChineseErrorSummary(TransportMetricsSnapshot snapshot)
        {
            var totalErrors = snapshot.SendErrors + snapshot.ReceiveErrors + snapshot.OtherErrors;
            if (totalErrors == 0)
            {
                return "错误统计：未记录到传输错误。";
            }

            var busiestErrorStage = snapshot.ErrorCountsByStage.Count == 0
                ? "无"
                : snapshot.ErrorCountsByStage.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).First().Key;
            return $"错误统计：总数={totalErrors}，发送={snapshot.SendErrors}，接收={snapshot.ReceiveErrors}，其他={snapshot.OtherErrors}，最高频阶段={busiestErrorStage}。";
        }

        private static string BuildChineseLifecycleSummary(TransportMetricsSnapshot snapshot)
        {
            if (snapshot.ApplicationSessionsTracked == 0)
            {
                return "生命周期摘要：未捕获到共享会话状态快照。";
            }

            var states = snapshot.ApplicationSessionStateCounts.Count == 0
                ? "无"
                : string.Join("，", snapshot.ApplicationSessionStateCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
            var heartbeatReady = snapshot.ApplicationSessionSummaries.Count(session => session.CanSendHeartbeat);
            var reconnectPending = snapshot.ApplicationSessionSummaries.Count(session => session.NextReconnectAtUtc.HasValue);
            return $"生命周期摘要：已跟踪={snapshot.ApplicationSessionsTracked}，可发送心跳={heartbeatReady}，等待重连={reconnectPending}，状态分布={states}。";
        }

        private static string BuildChineseHealthSummary(TransportMetricsSnapshot snapshot)
        {
            if (snapshot.SessionsWithDiagnostics == 0)
            {
                return "健康摘要：未捕获到 KCP 会话诊断数据。";
            }

            var states = snapshot.SessionStateCounts.Count == 0
                ? "无"
                : string.Join("，", snapshot.SessionStateCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
            return $"健康摘要：平均 RTT={snapshot.AverageSmoothedRttMs:F1} 毫秒，峰值 RTT={snapshot.PeakSmoothedRttMs} 毫秒，WaitSnd={snapshot.TotalWaitSendCount}，在途重传段={snapshot.TotalRetransmittedSegmentsInFlight}，累计观察到的重传次数={snapshot.TotalObservedRetransmissionSends}，状态分布={states}。";
        }

        private static string BuildApplicationSessionKey(string scope, string remoteEndPoint)
        {
            var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "default" : scope;
            var normalizedRemote = string.IsNullOrWhiteSpace(remoteEndPoint) ? "default" : remoteEndPoint;
            return normalizedScope + "|" + normalizedRemote;
        }

        private static TransportApplicationSessionSnapshot Clone(TransportApplicationSessionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new TransportApplicationSessionSnapshot
            {
                Scope = snapshot.Scope,
                RemoteEndPoint = snapshot.RemoteEndPoint,
                ConnectionState = snapshot.ConnectionState,
                CanSendHeartbeat = snapshot.CanSendHeartbeat,
                LastRoundTripTimeMs = snapshot.LastRoundTripTimeMs,
                LastFailureReason = snapshot.LastFailureReason,
                LastLivenessUtc = snapshot.LastLivenessUtc,
                LastHeartbeatSentUtc = snapshot.LastHeartbeatSentUtc,
                NextReconnectAtUtc = snapshot.NextReconnectAtUtc,
                CurrentServerTick = snapshot.CurrentServerTick,
                ObservedAtUtc = snapshot.ObservedAtUtc
            };
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
            public string SessionLifecycleState { get; set; } = "active";
            public TransportSessionDiagnosticsSnapshot SessionDiagnostics { get; private set; } = new();
            public int PeakSmoothedRttMs { get; private set; }
            public int PeakRetransmissionTimeoutMs { get; private set; }
            public int PeakWaitSendCount { get; private set; }
            public int PeakRetransmittedSegmentsInFlight { get; private set; }
            public long ObservedRetransmissionSends { get; private set; }
            public long ObservedLossSignals { get; private set; }
            public Dictionary<string, long> ErrorCountsByStage { get; } = new(StringComparer.Ordinal);

            public void RecordDiagnostics(TransportSessionDiagnosticsSnapshot diagnostics)
            {
                SessionLifecycleState = string.IsNullOrWhiteSpace(diagnostics.LifecycleState)
                    ? SessionLifecycleState
                    : diagnostics.LifecycleState;
                SessionDiagnostics = diagnostics;
                PeakSmoothedRttMs = Math.Max(PeakSmoothedRttMs, diagnostics.SmoothedRttMs);
                PeakRetransmissionTimeoutMs = Math.Max(PeakRetransmissionTimeoutMs, diagnostics.RetransmissionTimeoutMs);
                PeakWaitSendCount = Math.Max(PeakWaitSendCount, diagnostics.WaitSendCount);
                PeakRetransmittedSegmentsInFlight = Math.Max(PeakRetransmittedSegmentsInFlight, diagnostics.RetransmittedSegmentsInFlight);
                ObservedRetransmissionSends = Math.Max(ObservedRetransmissionSends, diagnostics.ObservedRetransmissionSends);
                ObservedLossSignals = Math.Max(ObservedLossSignals, diagnostics.ObservedLossSignals);
            }

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
                    SessionLifecycleState = SessionLifecycleState,
                    SessionDiagnostics = SessionDiagnostics,
                    PeakSmoothedRttMs = PeakSmoothedRttMs,
                    PeakRetransmissionTimeoutMs = PeakRetransmissionTimeoutMs,
                    PeakWaitSendCount = PeakWaitSendCount,
                    PeakRetransmittedSegmentsInFlight = PeakRetransmittedSegmentsInFlight,
                    ObservedRetransmissionSends = ObservedRetransmissionSends,
                    ObservedLossSignals = ObservedLossSignals,
                    ErrorCountsByStage = ErrorCountsByStage.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                };
            }
        }
    }
}
