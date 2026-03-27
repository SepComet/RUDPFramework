using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkHost;
using Network.NetworkTransport;
using NUnit.Framework;

namespace Tests.EditMode.Network
{
    public class SessionLifecycleTests
    {
        [Test]
        public void SharedNetworkRuntime_StartAsync_TransitionsToTransportConnectedButNotLoggedIn()
        {
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher());

            runtime.StartAsync().GetAwaiter().GetResult();

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.TransportConnected));
            Assert.That(runtime.SessionManager.CanSendHeartbeat, Is.False);
        }

        [Test]
        public void LoginFailure_IsDistinctFromTransportConnectedState()
        {
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher());

            runtime.StartAsync().GetAwaiter().GetResult();
            runtime.NotifyLoginStarted();
            runtime.NotifyLoginFailed("bad credentials");

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.LoginFailed));
            Assert.That(runtime.SessionManager.LastFailureReason, Is.EqualTo("bad credentials"));
        }

        [Test]
        public void HeartbeatTimeout_SchedulesAndStartsReconnect()
        {
            var clock = new MutableClock(new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero));
            var policy = new SessionReconnectPolicy(
                heartbeatInterval: TimeSpan.FromSeconds(2),
                heartbeatTimeout: TimeSpan.FromSeconds(5),
                reconnectDelay: TimeSpan.FromSeconds(3),
                autoReconnect: true);
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher(), policy, clock.UtcNow);
            var events = new List<SessionEventKind>();

            runtime.LifecycleChanged += lifecycleEvent => events.Add(lifecycleEvent.Kind);
            runtime.StartAsync().GetAwaiter().GetResult();
            runtime.NotifyLoginStarted();
            runtime.NotifyLoginSucceeded();

            clock.Advance(TimeSpan.FromSeconds(6));
            runtime.UpdateLifecycle();

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.ReconnectPending));
            Assert.That(events, Does.Contain(SessionEventKind.TimedOut));
            Assert.That(events, Does.Contain(SessionEventKind.ReconnectScheduled));

            clock.Advance(TimeSpan.FromSeconds(3));
            runtime.UpdateLifecycle();

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.Reconnecting));
            Assert.That(events, Does.Contain(SessionEventKind.ReconnectStarted));
        }

        [Test]
        public void HeartbeatResponse_UpdatesRttAndServerTick_WithoutChangingLoggedInState()
        {
            var clock = new MutableClock(new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero));
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher(), utcNowProvider: clock.UtcNow);

            runtime.StartAsync().GetAwaiter().GetResult();
            runtime.NotifyLoginStarted();
            runtime.NotifyLoginSucceeded();
            runtime.NotifyHeartbeatSent();

            clock.Advance(TimeSpan.FromMilliseconds(120));
            runtime.NotifyHeartbeatReceived(321);

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.LoggedIn));
            Assert.That(runtime.SessionManager.LastRoundTripTime, Is.EqualTo(TimeSpan.FromMilliseconds(120)));
            Assert.That(runtime.SessionManager.LastServerTick, Is.EqualTo(321));
        }

        [Test]
        public void ServerNetworkHost_TracksMultipleSessionsIndependently()
        {
            var clock = new MutableClock(new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero));
            var policy = new SessionReconnectPolicy(
                heartbeatInterval: TimeSpan.FromSeconds(2),
                heartbeatTimeout: TimeSpan.FromSeconds(5),
                reconnectDelay: TimeSpan.FromSeconds(3),
                autoReconnect: true);
            var transport = new FakeTransport();
            var host = new ServerNetworkHost(transport, reconnectPolicy: policy, utcNowProvider: clock.UtcNow);
            var peerA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
            var peerB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5002);

            host.StartAsync().GetAwaiter().GetResult();
            transport.EmitReceive(CreateEnvelope(MessageType.Heartbeat), peerA);
            transport.EmitReceive(CreateEnvelope(MessageType.Heartbeat), peerB);
            host.NotifyLoginStarted(peerA);
            host.NotifyLoginSucceeded(peerA);
            host.NotifyLoginStarted(peerB);
            host.NotifyLoginSucceeded(peerB);

            clock.Advance(TimeSpan.FromSeconds(6));
            host.NotifyHeartbeatReceived(peerB, 99);
            host.UpdateLifecycle();

            Assert.That(host.ManagedSessions.Count, Is.EqualTo(2));
            Assert.That(host.TryGetSession(peerA, out var sessionA), Is.True);
            Assert.That(host.TryGetSession(peerB, out var sessionB), Is.True);
            Assert.That(sessionA.SessionManager.State, Is.EqualTo(ConnectionState.ReconnectPending));
            Assert.That(sessionB.SessionManager.State, Is.EqualTo(ConnectionState.LoggedIn));
            Assert.That(sessionB.SessionManager.LastServerTick, Is.EqualTo(99));
        }

        [Test]
        public void ServerNetworkHost_RemoveSession_DoesNotDisturbOtherPeers()
        {
            var transport = new FakeTransport();
            var host = new ServerNetworkHost(transport);
            var peerA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
            var peerB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5002);

            host.StartAsync().GetAwaiter().GetResult();
            transport.EmitReceive(CreateEnvelope(MessageType.Heartbeat), peerA);
            transport.EmitReceive(CreateEnvelope(MessageType.Heartbeat), peerB);

            var removed = host.RemoveSession(peerA, "peer closed");

            Assert.That(removed, Is.True);
            Assert.That(host.ManagedSessions.Count, Is.EqualTo(1));
            Assert.That(host.TryGetSession(peerA, out _), Is.False);
            Assert.That(host.TryGetSession(peerB, out var sessionB), Is.True);
            Assert.That(sessionB.SessionManager.State, Is.EqualTo(ConnectionState.TransportConnected));
        }

        [Test]
        public void ServerNetworkHost_LifecycleEventsIncludeRemotePeerIdentity()
        {
            var transport = new FakeTransport();
            var host = new ServerNetworkHost(transport);
            var peer = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
            MultiSessionLifecycleEvent receivedEvent = null;

            host.LifecycleChanged += lifecycleEvent => receivedEvent = lifecycleEvent;

            host.StartAsync().GetAwaiter().GetResult();
            transport.EmitReceive(CreateEnvelope(MessageType.Heartbeat), peer);

            Assert.That(receivedEvent, Is.Not.Null);
            Assert.That(receivedEvent.RemoteEndPoint, Is.EqualTo(peer));
            Assert.That(receivedEvent.LifecycleEvent.CurrentState, Is.EqualTo(ConnectionState.TransportConnected));
        }

        private static byte[] CreateEnvelope(MessageType type)
        {
            return new Envelope
            {
                Type = (int)type
            }.ToByteArray();
        }

        private sealed class MutableClock
        {
            public MutableClock(DateTimeOffset now)
            {
                Now = now;
            }

            public DateTimeOffset Now { get; private set; }

            public DateTimeOffset UtcNow()
            {
                return Now;
            }

            public void Advance(TimeSpan delta)
            {
                Now = Now.Add(delta);
            }
        }

        private sealed class FakeTransport : ITransport
        {
            public event Action<byte[], IPEndPoint> OnReceive;

            public Task StartAsync()
            {
                return Task.CompletedTask;
            }

            public void Stop()
            {
            }

            public void Send(byte[] data)
            {
            }

            public void SendTo(byte[] data, IPEndPoint target)
            {
            }

            public void SendToAll(byte[] data)
            {
            }

            public void EmitReceive(byte[] data, IPEndPoint sender)
            {
                OnReceive?.Invoke(data, sender);
            }
        }
    }
}
