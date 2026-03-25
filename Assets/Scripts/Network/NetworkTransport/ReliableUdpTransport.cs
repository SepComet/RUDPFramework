using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Network.NetworkTransport
{
    public class ReliableUdpTransport : ITransport
    {
        private readonly UdpClient _client;
        private readonly IPEndPoint _defaultRemoteEndPoint;
        private readonly bool _isServer;

        private readonly List<ClientSession> _sessions = new();

        private readonly Timer _retransmitTimer;
        private readonly Timer _cleanupTimer;

        //TODO: volatile 关键字
        private volatile bool _isRunning;

        // 配置参数
        private const int RetransmitTimeoutMs = 1000;
        private const int SessionTimeoutMs = 30000;
        private const int MaxRetransmitAttempts = 5;

        public event Action<byte[], IPEndPoint> OnReceive;

        private Task _receiveTask;

        // 构造函数——服务端模式
        public ReliableUdpTransport(int listenPort)
        {
            _client = new UdpClient(listenPort);
            _isServer = true;
            _retransmitTimer = new Timer(CheckRetransmit, null, 100, 100);
            _cleanupTimer = new Timer(CleanupSessions, null, 5000, 5000);
            Console.WriteLine($"[Transport] 服务端模式，监听端口: {listenPort}");
        }

        // 构造函数——客户端模式
        public ReliableUdpTransport(string serverIP, int serverPort)
        {
            _client = new UdpClient(0);
            _defaultRemoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            _isServer = false;
            _retransmitTimer = new Timer(CheckRetransmit, null, 100, 100);
            _cleanupTimer = new Timer(CleanupSessions, null, 5000, 5000);
            Console.WriteLine($"[Transport] 客户端模式，目标: {_defaultRemoteEndPoint}");
        }

        public async Task StartAsync()
        {
            _sessions.Clear();

            _isRunning = true;
            Console.WriteLine("[Transport] 传输层启动");

            // 开始接收数据
            _receiveTask = ReceiveLoop();
            await Task.Delay(100); // 给接收循环一点启动时间
        }

        public void Tick()
        {
            foreach (var session in _sessions)
            {
                session.Tick(DateTime.UtcNow.Millisecond);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _retransmitTimer.Dispose();
            _cleanupTimer.Dispose();
            _client.Close();
            _sessions.Clear();
            Console.WriteLine("[Transport] 传输层停止");
        }

        public async void SendTo(Packet packet, IPEndPoint target)
        {
            if (!_isRunning)
            {
                return;
            }

            var bytes = packet.ToBytes();
            await _client.SendAsync(bytes, bytes.Length, target);
            
            Console.WriteLine($"[Transport] 发送数据包到 {target}");
        }

        public void SendToAll(byte[] data)
        {
            foreach (var session in _sessions)
            {
                session.SendPacket(data);
            }
        }

        private async Task ReceiveLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var result = await _client.ReceiveAsync();
                    var packet = Packet.FromBytes(result.Buffer);

                    if (packet.Type == PacketType.Data)
                    {
                        HandleDataPacket(packet, result.RemoteEndPoint);
                    }
                    else if (packet.Type == PacketType.Ack)
                    {
                        HandleAckPacket(packet, result.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return; // 正常关闭
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Transport] 接收错误：{e.Message}");
                }
            }
        }

        private void HandleDataPacket(Packet packet, IPEndPoint senderEndPoint)
        {
            var session = GetOrCreateSession(senderEndPoint);

            Console.WriteLine(
                $"[Transport] 收到数据包从{senderEndPoint} SeqNum={packet.SequenceNumber}, DataLen={packet.Data.Length}");

            // 发送ACK
            var ackPacket = Packet.CreateAckPacket(packet.SequenceNumber);
            SendPacketTo(ackPacket, senderEndPoint);
            Console.WriteLine($"[Transport] 发送ACK 到 {senderEndPoint} SeqNum={packet.SequenceNumber}");

            // 检查是否应该交付
            if (session.TryProcessReceiveSequence(packet.SequenceNumber, out bool shouldDeliver))
            {
                if (shouldDeliver)
                {
                    OnReceive?.Invoke(packet.Data, senderEndPoint);
                    Console.WriteLine($"[Transport] 交付数据包从 {senderEndPoint} SeqNum={packet.SequenceNumber}");
                }
                else
                {
                    Console.WriteLine($"[Transport] 重复包从 {senderEndPoint} SeqNum={packet.SequenceNumber}，忽略");
                }
            }
            else
            {
                // 乱序到达，暂存（简化处理：直接丢弃，依赖重传）
                Console.WriteLine($"[Transport] 乱序包从 {senderEndPoint} SeqNum={packet.SequenceNumber}，丢弃");
            }
        }

        private void HandleAckPacket(Packet packet, IPEndPoint senderEndPoint)
        {
            var session = GetOrCreateSession(senderEndPoint);
            Console.WriteLine($"[Transport] 收到ACK从 {senderEndPoint} SeqNum={packet.SequenceNumber}");

            if (session.PendingAcks.TryRemove(packet.SequenceNumber, out _))
            {
                Console.WriteLine($"[Transport] 确认包到 {senderEndPoint} SeqNum={packet.SequenceNumber}");
            }
        }

        private ClientSession GetOrCreateSession(IPEndPoint endPoint)
        {
            string key = endPoint.ToString();
            return _sessions.GetOrAdd(key, _ =>
            {
                var session = new ClientSession(endPoint);
                Console.WriteLine($"创建新会话：{endPoint}");
                return session;
            });
        }

        private void CheckRetransmit(object state)
        {
            if (!_isRunning)
            {
                return;
            }

            var now = DateTime.Now;
            var toRetransmit = new List<(IPEndPoint target, uint seqNum, Packet packet)>();

            foreach (var sessionKvp in _sessions)
            {
                var session = sessionKvp.Value;
                foreach (var ackKvp in session.PendingAcks)
                {
                    var timeSinceLastSend = now - ackKvp.Value.sendTime;
                    if (timeSinceLastSend.TotalMilliseconds > RetransmitTimeoutMs)
                    {
                        toRetransmit.Add((session.EndPoint, ackKvp.Key, ackKvp.Value.packet));
                    }
                }
            }


            foreach (var (target, seqNum, packet) in toRetransmit)
            {
                var session = GetOrCreateSession(target);
                if (session.PendingAcks.ContainsKey(seqNum))
                {
                    // 更新发送时间
                    session.PendingAcks[seqNum] = (packet, now);
                    SendPacketTo(packet, target);
                    Console.WriteLine($"[Transport] 重传包到 {target} SeqNum={seqNum}");
                }
            }
        }

        private void CleanupSessions(object state)
        {
            if (!_isRunning)
            {
                return;
            }

            var now = DateTime.Now;
            var toRemove = new List<string>();

            foreach (var sessionKvp in _sessions)
            {
                var session = sessionKvp.Value;
                var timeSinceLastActivity = now - session.LastActivity;

                if (timeSinceLastActivity.TotalMilliseconds > SessionTimeoutMs)
                {
                    toRemove.Add(sessionKvp.Key);
                }
            }

            foreach (string key in toRemove)
            {
                if (_sessions.TryRemove(key, out var session))
                {
                    Console.WriteLine($"[Transport] 清理超时会话：{session.EndPoint}");
                }
            }

            if (_isServer)
            {
                PrintSessionInfo();
            }
        }

        private async void SendPacketTo(Packet packet, IPEndPoint endPoint)
        {
            try
            {
                var data = packet.ToBytes();
                await _client.SendAsync(data, data.Length, endPoint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Transport] 发送错误：{e.Message}");
            }
        }

        private void PrintSessionInfo()
        {
            Console.WriteLine($"当前活跃会话数：{_sessions.Count}");
            foreach (var sessionKvp in _sessions)
            {
                var session = sessionKvp.Value;
                Console.WriteLine(
                    $"  会话：{session.EndPoint}，发送SeqNum：{session.SendSequenceNumber}，期望接收：{session.GetExpectedAck()}，待确认: {session.PendingAcks.Count}");
            }
        }
    }
}