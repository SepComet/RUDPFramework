using System;
using System.Net;
using System.Runtime.InteropServices;
using kcp;

namespace Network.NetworkTransport
{
    public partial class KcpTransport
    {
        private sealed unsafe class KcpSession : IDisposable
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