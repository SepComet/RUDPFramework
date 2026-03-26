using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using kcp;

namespace Network.NetworkTransport
{
    public class KcpTransport : ITransport
    {
        private const uint DefaultConv = 1;
        private const int DefaultNoDelay = 1;
        private const int DefaultInterval = 10;
        private const int DefaultResend = 2;
        private const int DefaultNc = 1;
        private const int DefaultSendWindow = 128;
        private const int DefaultReceiveWindow = 128;
        private const int DefaultMtu = 1200;
        private static readonly TimeSpan UpdateLoopDelay = TimeSpan.FromMilliseconds(DefaultInterval);

        private readonly UdpClient _client;
        private readonly bool _isServer;
        private readonly IPEndPoint _defaultRemoteEndPoint;
        private readonly uint _defaultConv;
        private readonly ConcurrentDictionary<string, KcpSession> _sessions = new();
        private readonly object _socketSendLock = new();

        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask = Task.CompletedTask;
        private Task _updateTask = Task.CompletedTask;
        private volatile bool _isRunning;

        public event Action<byte[], IPEndPoint> OnReceive;

        internal int ActiveSessionCount => _sessions.Count;

        public KcpTransport(int listenPort, uint conv = DefaultConv)
        {
            _client = new UdpClient(listenPort);
            _isServer = true;
            _defaultConv = conv;

            Console.WriteLine($"[KcpTransport] 服务端模式，监听端口: {listenPort}");
        }

        public KcpTransport(string serverIp, int serverPort, uint conv = DefaultConv)
        {
            if (string.IsNullOrWhiteSpace(serverIp))
            {
                throw new ArgumentException("Server IP is required.", nameof(serverIp));
            }

            _client = new UdpClient(0);
            _defaultRemoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            _defaultConv = conv;

            Console.WriteLine($"[KcpTransport] 客户端模式，目标: {_defaultRemoteEndPoint}, conv={conv}");
        }

        public Task StartAsync()
        {
            if (_isRunning)
            {
                return Task.CompletedTask;
            }

            _sessions.Clear();
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            if (!_isServer)
            {
                GetOrCreateSession(_defaultRemoteEndPoint, _defaultConv);
            }

            _receiveTask = ReceiveLoopAsync(_cancellationTokenSource.Token);
            _updateTask = UpdateLoopAsync(_cancellationTokenSource.Token);

            Console.WriteLine("[KcpTransport] 传输层启动");
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _client.Close();
            WaitForBackgroundTasks();
            DisposeAllSessions();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            Console.WriteLine("[KcpTransport] 传输层停止");
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

            if (!_isServer && !target.Equals(_defaultRemoteEndPoint))
            {
                throw new InvalidOperationException("Client mode only supports the configured default remote endpoint.");
            }

            var session = GetOrCreateSession(target, _defaultConv);
            session.Send(data);
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

            foreach (var session in _sessions.Values)
            {
                session.Send(data);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _client.ReceiveAsync();
                    var session = GetOrCreateSession(result.RemoteEndPoint, ResolveConv(result.Buffer));
                    session.Input(result.Buffer);
                    DrainReceivedMessages(session);
                }
                catch (ObjectDisposedException) when (!_isRunning || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (SocketException) when (!_isRunning || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[KcpTransport] 接收错误：{exception.Message}");
                }
            }
        }

        private async Task UpdateLoopAsync(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                var current = GetCurrentTimeMilliseconds();

                foreach (var session in _sessions.Values)
                {
                    session.UpdateIfDue(current);
                    DrainReceivedMessages(session);
                }

                try
                {
                    await Task.Delay(UpdateLoopDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void DrainReceivedMessages(KcpSession session)
        {
            while (session.TryReceive(out var payload))
            {
                OnReceive?.Invoke(payload, session.RemoteEndPoint);
            }
        }

        private KcpSession GetOrCreateSession(IPEndPoint remoteEndPoint, uint conv)
        {
            var normalizedEndPoint = NormalizeEndPoint(remoteEndPoint);
            var key = normalizedEndPoint.ToString();

            return _sessions.GetOrAdd(key, _ => new KcpSession(this, normalizedEndPoint, conv));
        }

        private IPEndPoint NormalizeEndPoint(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (_isServer)
            {
                return new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
            }

            if (_defaultRemoteEndPoint == null)
            {
                throw new InvalidOperationException("Default remote endpoint is not configured.");
            }

            return new IPEndPoint(_defaultRemoteEndPoint.Address, _defaultRemoteEndPoint.Port);
        }

        private unsafe uint ResolveConv(byte[] datagram)
        {
            if (datagram == null || datagram.Length < sizeof(uint))
            {
                return _defaultConv;
            }

            fixed (byte* buffer = datagram)
            {
                return KCP.ikcp_getconv(buffer);
            }
        }

        private static uint GetCurrentTimeMilliseconds()
        {
            return unchecked((uint)Environment.TickCount);
        }

        private void EnsureRunning()
        {
            if (!_isRunning)
            {
                throw new InvalidOperationException("Transport has not been started.");
            }
        }

        private void WaitForBackgroundTasks()
        {
            try
            {
                Task.WaitAll(new[] { _receiveTask, _updateTask }, TimeSpan.FromSeconds(1));
            }
            catch (AggregateException exception)
            {
                foreach (var innerException in exception.InnerExceptions)
                {
                    Console.WriteLine($"[KcpTransport] 停止等待错误：{innerException.Message}");
                }
            }
        }

        private void DisposeAllSessions()
        {
            foreach (var pair in _sessions)
            {
                pair.Value.Dispose();
            }

            _sessions.Clear();
        }

        private unsafe int SendDatagram(byte* buffer, int length, IPEndPoint remoteEndPoint)
        {
            if (!_isRunning)
            {
                return -1;
            }

            var datagram = new byte[length];
            Marshal.Copy((IntPtr)buffer, datagram, 0, length);

            try
            {
                lock (_socketSendLock)
                {
                    return _client.Send(datagram, datagram.Length, remoteEndPoint);
                }
            }
            catch (ObjectDisposedException) when (!_isRunning)
            {
                return -1;
            }
            catch (SocketException exception)
            {
                Console.WriteLine($"[KcpTransport] 发送错误：{exception.Message}");
                return -1;
            }
        }

        private unsafe sealed class KcpSession : IDisposable
        {
            private readonly KcpTransport _owner;
            private readonly object _gate = new();
            private readonly GCHandle _handle;

            private IKCPCB* _kcp;
            private bool _disposed;
            private uint _nextUpdateAt;

            public KcpSession(KcpTransport owner, IPEndPoint remoteEndPoint, uint conv)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
                Conv = conv;
                LastActivityUtc = DateTime.UtcNow;

                _handle = GCHandle.Alloc(this);
                _kcp = KCP.ikcp_create(conv, (void*)GCHandle.ToIntPtr(_handle));
                KCP.ikcp_setoutput(_kcp, &OutputCallback);
                KCP.ikcp_nodelay(_kcp, DefaultNoDelay, DefaultInterval, DefaultResend, DefaultNc);
                KCP.ikcp_wndsize(_kcp, DefaultSendWindow, DefaultReceiveWindow);
                KCP.ikcp_setmtu(_kcp, DefaultMtu);

                _nextUpdateAt = GetCurrentTimeMilliseconds();
            }

            public uint Conv { get; }

            public IPEndPoint RemoteEndPoint { get; }

            public DateTime LastActivityUtc { get; private set; }

            public void Send(byte[] payload)
            {
                if (payload == null)
                {
                    throw new ArgumentNullException(nameof(payload));
                }

                lock (_gate)
                {
                    ThrowIfDisposed();

                    if (payload.Length == 0)
                    {
                        return;
                    }

                    fixed (byte* buffer = payload)
                    {
                        var result = KCP.ikcp_send(_kcp, buffer, payload.Length);
                        if (result < 0)
                        {
                            throw new InvalidOperationException($"KCP send failed with error code {result}.");
                        }
                    }

                    LastActivityUtc = DateTime.UtcNow;
                    UpdateNoLock(GetCurrentTimeMilliseconds());
                }
            }

            public void Input(byte[] datagram)
            {
                if (datagram == null)
                {
                    throw new ArgumentNullException(nameof(datagram));
                }

                if (datagram.Length == 0)
                {
                    return;
                }

                lock (_gate)
                {
                    ThrowIfDisposed();

                    fixed (byte* buffer = datagram)
                    {
                        var result = KCP.ikcp_input(_kcp, buffer, datagram.Length);
                        if (result < 0)
                        {
                            Console.WriteLine($"[KcpTransport] KCP input failed for {RemoteEndPoint}: {result}");
                            return;
                        }
                    }

                    LastActivityUtc = DateTime.UtcNow;
                    UpdateNoLock(GetCurrentTimeMilliseconds());
                }
            }

            public bool TryReceive(out byte[] payload)
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        payload = null;
                        return false;
                    }

                    var size = KCP.ikcp_peeksize(_kcp);
                    if (size <= 0)
                    {
                        payload = null;
                        return false;
                    }

                    payload = new byte[size];

                    fixed (byte* buffer = payload)
                    {
                        var result = KCP.ikcp_recv(_kcp, buffer, payload.Length);
                        if (result < 0)
                        {
                            payload = null;
                            return false;
                        }

                        if (result != payload.Length)
                        {
                            Array.Resize(ref payload, result);
                        }
                    }

                    LastActivityUtc = DateTime.UtcNow;
                    return true;
                }
            }

            public void UpdateIfDue(uint current)
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (KCP._itimediff(current, _nextUpdateAt) < 0)
                    {
                        return;
                    }

                    UpdateNoLock(current);
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;

                    if (_kcp != null)
                    {
                        KCP.ikcp_release(_kcp);
                        _kcp = null;
                    }

                    if (_handle.IsAllocated)
                    {
                        _handle.Free();
                    }
                }
            }

            private void UpdateNoLock(uint current)
            {
                KCP.ikcp_update(_kcp, current);
                _nextUpdateAt = KCP.ikcp_check(_kcp, current);
            }

            private void ThrowIfDisposed()
            {
                if (_disposed || _kcp == null)
                {
                    throw new ObjectDisposedException(nameof(KcpSession));
                }
            }

            private int SendRaw(byte* buffer, int length)
            {
                return _owner.SendDatagram(buffer, length, RemoteEndPoint);
            }

            private static int OutputCallback(byte* buffer, int length, IKCPCB* kcp, void* user)
            {
                if (user == null)
                {
                    return -1;
                }

                var handle = GCHandle.FromIntPtr((IntPtr)user);
                if (handle.Target is not KcpSession session)
                {
                    return -1;
                }

                return session.SendRaw(buffer, length);
            }
        }
    }
}
