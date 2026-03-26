using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network.NetworkTransport
{
    public class ReliableUdpTransport : ITransport
    {
        private readonly UdpClient _client;
        private readonly IPEndPoint _defaultRemoteEndPoint;
        private readonly bool _isServer;

        // Stage one keeps this class name for compatibility while collapsing it to plain UDP.
        private readonly ConcurrentDictionary<string, IPEndPoint> _knownRemoteEndPoints = new();

        private volatile bool _isRunning;

        public event Action<byte[], IPEndPoint> OnReceive;

        private Task _receiveTask = Task.CompletedTask;

        public ReliableUdpTransport(int listenPort)
        {
            _client = new UdpClient(listenPort);
            _isServer = true;
            Console.WriteLine($"[Transport] 服务端模式，监听端口: {listenPort}");
        }

        public ReliableUdpTransport(string serverIP, int serverPort)
        {
            _client = new UdpClient(0);
            _defaultRemoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            _isServer = false;
            Console.WriteLine($"[Transport] 客户端模式，目标: {_defaultRemoteEndPoint}");
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                return;
            }

            _knownRemoteEndPoints.Clear();
            _isRunning = true;
            Console.WriteLine("[Transport] 传输层启动");
            _receiveTask = ReceiveLoop();
            await Task.Yield();
        }

        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _client.Close();
            _knownRemoteEndPoints.Clear();
            Console.WriteLine("[Transport] 传输层停止");
        }

        public void Send(byte[] data)
        {
            if (_defaultRemoteEndPoint == null)
            {
                throw new InvalidOperationException("Default remote endpoint is not configured.");
            }

            SendTo(data, _defaultRemoteEndPoint);
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            EnsureRunning();
            RememberRemote(target);
            _client.Send(data, data.Length, target);
            Console.WriteLine($"[Transport] 发送数据到 {target}");
        }

        public void SendToAll(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            EnsureRunning();

            if (!_isServer)
            {
                throw new InvalidOperationException("SendToAll is only supported in server mode.");
            }

            foreach (var remoteEndPoint in _knownRemoteEndPoints.Values)
            {
                _client.Send(data, data.Length, remoteEndPoint);
                Console.WriteLine($"[Transport] 广播数据到 {remoteEndPoint}");
            }
        }

        private async Task ReceiveLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var result = await _client.ReceiveAsync();
                    RememberRemote(result.RemoteEndPoint);
                    OnReceive?.Invoke(result.Buffer, result.RemoteEndPoint);
                }
                catch (ObjectDisposedException) when (!_isRunning)
                {
                    return;
                }
                catch (SocketException) when (!_isRunning)
                {
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Transport] 接收错误：{e.Message}");
                }
            }
        }

        private void EnsureRunning()
        {
            if (!_isRunning)
            {
                throw new InvalidOperationException("Transport has not been started.");
            }
        }

        private void RememberRemote(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                return;
            }

            _knownRemoteEndPoints[remoteEndPoint.ToString()] = remoteEndPoint;
        }
    }
}
