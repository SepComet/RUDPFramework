using System;
using System.IO;

namespace Network.NetworkTransport
{
    public sealed class TransportMetricsOptions
    {
        public static TransportMetricsOptions Default { get; } = new();

        public string ReportDirectory { get; set; }

        public TextWriter ConsoleWriter { get; set; }

        public bool WriteJsonReport { get; set; } = true;

        public bool WriteTextSummaryReport { get; set; } = true;

        public bool WriteDiagnosisReport { get; set; } = true;

        public bool EmitConsoleSummary { get; set; } = true;

        public int MaxPeerSummariesInTextReport { get; set; } = 5;

        public int MaxPeerSummariesInConsole { get; set; } = 3;
    }
}
