using System;
using System.Collections.Generic;
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
            private readonly Dictionary<uint, uint> _observedSegmentXmitBySequence = new();
            private long _observedRetransmissionSends;
            private long _observedLossSignals;

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
                _owner.RecordSessionDiagnostics(this, "active");
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
                            _owner.RecordTransportError("kcp-send", RemoteEndPoint, $"KCP send failed with error code {result}.");
                            throw new InvalidOperationException($"KCP send failed with error code {result}.");
                        }
                    }

                    LastActivityUtc = DateTime.UtcNow;
                    UpdateNoLock(GetCurrentTimeMilliseconds());
                    _owner.RecordSessionDiagnostics(this, "active");
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
                            _owner.RecordTransportError("kcp-input", RemoteEndPoint, $"KCP input failed with error code {result}.");
                            Console.WriteLine($"[KcpTransport] KCP input failed for {RemoteEndPoint}: {result}");
                            return;
                        }
                    }

                    LastActivityUtc = DateTime.UtcNow;
                    UpdateNoLock(GetCurrentTimeMilliseconds());
                    _owner.RecordSessionDiagnostics(this, "active");
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
                            _owner.RecordTransportError("kcp-recv", RemoteEndPoint, $"KCP recv failed with error code {result}.");
                            payload = null;
                            return false;
                        }

                        if (result != payload.Length)
                        {
                            Array.Resize(ref payload, result);
                        }
                    }

                    LastActivityUtc = DateTime.UtcNow;
                    _owner.RecordSessionDiagnostics(this, "active");
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
                    _owner.RecordSessionDiagnostics(this, "active");
                }
            }

            public TransportSessionDiagnosticsSnapshot CaptureDiagnostics(string lifecycleState)
            {
                lock (_gate)
                {
                    var now = DateTimeOffset.UtcNow;
                    if (_disposed || _kcp == null)
                    {
                        return new TransportSessionDiagnosticsSnapshot
                        {
                            LifecycleState = lifecycleState,
                            ObservedAtUtc = now,
                            IdleMs = Math.Max(0L, (long)(now - LastActivityUtc).TotalMilliseconds)
                        };
                    }

                    var retransmittedSegmentsInFlight = 0;
                    var observedRetransmissionDelta = 0L;
                    var head = &_kcp->snd_buf;

                    for (var node = head->next; node != head; node = node->next)
                    {
                        var segment = (IKCPSEG*)node;
                        var currentXmit = Math.Max(0u, segment->xmit);
                        _observedSegmentXmitBySequence.TryGetValue(segment->sn, out var observedXmit);
                        var baselineXmit = observedXmit == 0 ? 1u : observedXmit;
                        if (currentXmit > baselineXmit)
                        {
                            observedRetransmissionDelta += currentXmit - baselineXmit;
                        }

                        if (currentXmit > observedXmit)
                        {
                            _observedSegmentXmitBySequence[segment->sn] = currentXmit;
                        }

                        if (currentXmit > 1)
                        {
                            retransmittedSegmentsInFlight++;
                        }
                    }

                    if (observedRetransmissionDelta > 0)
                    {
                        _observedRetransmissionSends += observedRetransmissionDelta;
                        _observedLossSignals += observedRetransmissionDelta;
                    }

                    return new TransportSessionDiagnosticsSnapshot
                    {
                        LifecycleState = lifecycleState,
                        ObservedAtUtc = now,
                        IdleMs = Math.Max(0L, (long)(now - LastActivityUtc).TotalMilliseconds),
                        KcpStateCode = unchecked((int)_kcp->state),
                        SmoothedRttMs = Math.Max(0, _kcp->rx_srtt),
                        RttVarianceMs = Math.Max(0, _kcp->rx_rttval),
                        RetransmissionTimeoutMs = Math.Max(0, _kcp->rx_rto),
                        LocalSendWindow = checked((int)_kcp->snd_wnd),
                        LocalReceiveWindow = checked((int)_kcp->rcv_wnd),
                        RemoteWindow = checked((int)_kcp->rmt_wnd),
                        CongestionWindow = checked((int)_kcp->cwnd),
                        WaitSendCount = Math.Max(0, KCP.ikcp_waitsnd(_kcp)),
                        SendQueueCount = checked((int)_kcp->nsnd_que),
                        SendBufferCount = checked((int)_kcp->nsnd_buf),
                        ReceiveQueueCount = checked((int)_kcp->nrcv_que),
                        ReceiveBufferCount = checked((int)_kcp->nrcv_buf),
                        DeadLinkThreshold = checked((int)_kcp->dead_link),
                        SegmentTransmitCount = _kcp->xmit,
                        RetransmittedSegmentsInFlight = retransmittedSegmentsInFlight,
                        ObservedRetransmissionSends = _observedRetransmissionSends,
                        ObservedLossSignals = _observedLossSignals
                    };
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
