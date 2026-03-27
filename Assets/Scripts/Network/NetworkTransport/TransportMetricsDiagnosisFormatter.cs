using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Network.NetworkTransport
{
    public static class TransportMetricsDiagnosisFormatter
    {
        public static string BuildChineseDiagnosis(TransportMetricsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Transport Metrics Diagnosis / 传输诊断结论");
            builder.AppendLine();
            builder.AppendLine("结论摘要");
            builder.AppendLine($"- 传输实现：{snapshot.TransportName}");
            builder.AppendLine($"- 运行模式：{TranslateMode(snapshot.Mode)}");
            builder.AppendLine($"- 运行时长：{snapshot.DurationMs} 毫秒");
            builder.AppendLine($"- 总体判断：{BuildOverallAssessment(snapshot)}");
            builder.AppendLine();
            builder.AppendLine("关键观察");

            foreach (var finding in BuildFindings(snapshot))
            {
                builder.AppendLine($"- {finding}");
            }

            builder.AppendLine();
            builder.AppendLine("会话状态");

            foreach (var line in BuildSessionLines(snapshot))
            {
                builder.AppendLine($"- {line}");
            }

            builder.AppendLine();
            builder.AppendLine("热点对端");

            foreach (var peerLine in BuildPeerLines(snapshot))
            {
                builder.AppendLine($"- {peerLine}");
            }

            return builder.ToString();
        }

        private static IEnumerable<string> BuildFindings(TransportMetricsSnapshot snapshot)
        {
            yield return $"业务消息发送/接收={snapshot.PayloadMessagesSent}/{snapshot.PayloadMessagesReceived}，数据报发送/接收={snapshot.DatagramsSent}/{snapshot.DatagramsReceived}。";

            if (snapshot.ApplicationSessionsTracked > 0)
            {
                yield return $"共享会话已跟踪 {snapshot.ApplicationSessionsTracked} 个，状态分布：{FormatCounts(snapshot.ApplicationSessionStateCounts)}。";
            }

            if (snapshot.SessionsWithDiagnostics == 0)
            {
                yield return "没有采集到 KCP 会话诊断数据，当前无法判断 RTT、队列堆积和重传情况。";
                yield break;
            }

            yield return $"平均 RTT={snapshot.AverageSmoothedRttMs:F1} 毫秒，峰值 RTT={snapshot.PeakSmoothedRttMs} 毫秒。";
            yield return $"WaitSnd 总量={snapshot.TotalWaitSendCount}，发送队列={snapshot.TotalSendQueueCount}，发送缓冲={snapshot.TotalSendBufferCount}。";
            yield return $"在途重传段={snapshot.TotalRetransmittedSegmentsInFlight}，累计观察到的重传次数={snapshot.TotalObservedRetransmissionSends}。";

            if (snapshot.TotalWaitSendCount > 0 || snapshot.TotalSendQueueCount > 0 || snapshot.TotalSendBufferCount > 0)
            {
                yield return "存在发送侧堆积迹象，建议优先排查发送频率、消息体积和远端处理能力。";
            }

            if (snapshot.TotalObservedRetransmissionSends > 0 || snapshot.TotalRetransmittedSegmentsInFlight > 0)
            {
                yield return "存在重传迹象，建议结合网络抖动、丢包和窗口大小继续排查。";
            }

            if (snapshot.AverageSmoothedRttMs >= 150 || snapshot.PeakSmoothedRttMs >= 250)
            {
                yield return "延迟已偏高，若游戏表现出现卡顿或回滚，优先关注 RTT 抖动。";
            }

            if (snapshot.SendErrors + snapshot.ReceiveErrors + snapshot.OtherErrors > 0)
            {
                yield return $"记录到错误 {snapshot.SendErrors + snapshot.ReceiveErrors + snapshot.OtherErrors} 次，最高频阶段={FindBusiestStage(snapshot)}。";
            }
        }

        private static IEnumerable<string> BuildSessionLines(TransportMetricsSnapshot snapshot)
        {
            if (snapshot.ApplicationSessionSummaries.Count == 0)
            {
                yield return "没有共享会话快照。";
                yield break;
            }

            foreach (var session in snapshot.ApplicationSessionSummaries
                         .OrderBy(item => item.Scope, StringComparer.Ordinal)
                         .ThenBy(item => item.RemoteEndPoint, StringComparer.Ordinal))
            {
                var target = string.IsNullOrWhiteSpace(session.RemoteEndPoint) ? "default" : session.RemoteEndPoint;
                var details = new List<string>
                {
                    $"scope={session.Scope}",
                    $"target={target}",
                    $"state={session.ConnectionState}"
                };

                if (session.CanSendHeartbeat)
                {
                    details.Add("heartbeat=ready");
                }

                if (session.LastRoundTripTimeMs.HasValue)
                {
                    details.Add($"rtt={session.LastRoundTripTimeMs.Value}ms");
                }

                if (session.NextReconnectAtUtc.HasValue)
                {
                    details.Add($"reconnectAt={session.NextReconnectAtUtc.Value:O}");
                }

                if (!string.IsNullOrWhiteSpace(session.LastFailureReason))
                {
                    details.Add($"reason={session.LastFailureReason}");
                }

                if (session.CurrentServerTick.HasValue)
                {
                    details.Add($"serverTick={session.CurrentServerTick.Value}");
                }

                yield return string.Join(", ", details);
            }
        }

        private static IEnumerable<string> BuildPeerLines(TransportMetricsSnapshot snapshot)
        {
            var peers = snapshot.PeerSummaries
                .OrderByDescending(peer => peer.PayloadBytesSent + peer.PayloadBytesReceived)
                .ThenBy(peer => peer.RemoteEndPoint, StringComparer.Ordinal)
                .Take(5)
                .ToList();

            if (peers.Count == 0)
            {
                yield return "没有热点对端数据。";
                yield break;
            }

            foreach (var peer in peers)
            {
                yield return
                    $"{peer.RemoteEndPoint}: payload={peer.PayloadMessagesSent + peer.PayloadMessagesReceived} 条, datagram={peer.DatagramsSent + peer.DatagramsReceived} 个, " +
                    $"waitSnd={peer.SessionDiagnostics.WaitSendCount}, retrans={peer.ObservedRetransmissionSends}, state={peer.SessionLifecycleState}";
            }
        }

        private static string BuildOverallAssessment(TransportMetricsSnapshot snapshot)
        {
            if (snapshot.SessionsWithDiagnostics == 0 && snapshot.ApplicationSessionsTracked == 0)
            {
                return "观测数据不足";
            }

            var hasErrors = snapshot.SendErrors + snapshot.ReceiveErrors + snapshot.OtherErrors > 0;
            var hasRetransmissions = snapshot.TotalObservedRetransmissionSends > 0 || snapshot.TotalRetransmittedSegmentsInFlight > 0;
            var hasBacklog = snapshot.TotalWaitSendCount > 0 || snapshot.TotalSendQueueCount > 0 || snapshot.TotalSendBufferCount > 0;
            var hasReconnect = snapshot.ApplicationSessionStateCounts.TryGetValue("ReconnectPending", out var pending) && pending > 0;
            var hasTimeout = snapshot.ApplicationSessionStateCounts.TryGetValue("TimedOut", out var timedOut) && timedOut > 0;
            var highLatency = snapshot.AverageSmoothedRttMs >= 150 || snapshot.PeakSmoothedRttMs >= 250;

            if (hasTimeout || hasReconnect)
            {
                return "已出现会话不稳定迹象";
            }

            if (hasErrors && (hasRetransmissions || hasBacklog || highLatency))
            {
                return "网络质量存在明显风险";
            }

            if (hasRetransmissions || hasBacklog || highLatency)
            {
                return "网络链路存在轻中度异常";
            }

            return "整体稳定";
        }

        private static string FindBusiestStage(TransportMetricsSnapshot snapshot)
        {
            return snapshot.ErrorCountsByStage.Count == 0
                ? "无"
                : snapshot.ErrorCountsByStage
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .First()
                    .Key;
        }

        private static string FormatCounts(IReadOnlyDictionary<string, long> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                return "无";
            }

            return string.Join("，", counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
        }

        private static string TranslateMode(string mode)
        {
            return string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase)
                ? "server / 服务端"
                : string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase)
                    ? "client / 客户端"
                    : mode ?? "unknown / 未知";
        }
    }
}
