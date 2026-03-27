using System;
using System.IO;
using System.Linq;

namespace Network.NetworkTransport
{
    public static class TransportMetricsReportLocator
    {
        public static string GetDefaultReportDirectory()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Logs", "transport-metrics");
        }

        public static string TryGetLatestDiagnosisPath(string reportDirectory = null)
        {
            var directory = string.IsNullOrWhiteSpace(reportDirectory)
                ? GetDefaultReportDirectory()
                : reportDirectory;

            if (!Directory.Exists(directory))
            {
                return null;
            }

            return new DirectoryInfo(directory)
                .GetFiles("*.diagnosis.txt", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }

        public static string ReadLatestDiagnosisText(string reportDirectory = null)
        {
            var path = TryGetLatestDiagnosisPath(reportDirectory);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return File.ReadAllText(path);
        }
    }
}
