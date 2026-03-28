using System;
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
    public class SharedNetworkFoundationTests
    {
        private static readonly IPEndPoint Sender = new(IPAddress.Loopback, 9000);

        [Test]
        public void SharedNetworkRuntime_UsesInjectedDispatcherForDeferredClientStyleDispatch()
        {
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new MainThreadNetworkDispatcher());
            var handled = false;

            runtime.MessageManager.RegisterHandler(MessageType.Heartbeat, (payload, sender) =>
            {
                handled = true;
            });

            transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender);

            Assert.That(handled, Is.False);
            Assert.That(runtime.MessageManager.PendingMessageCount, Is.EqualTo(1));

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handled, Is.True);
            Assert.That(runtime.MessageManager.PendingMessageCount, Is.EqualTo(0));
        }

        [Test]
        public void ServerNetworkHost_UsesImmediateDispatcherByDefault()
        {
            var transport = new FakeTransport();
            var host = new ServerNetworkHost(transport);
            var handled = false;

            host.MessageManager.RegisterHandler(MessageType.Heartbeat, (payload, sender) =>
            {
                handled = true;
            });

            transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender);

            Assert.That(handled, Is.True);
            Assert.That(host.MessageManager.PendingMessageCount, Is.EqualTo(0));
        }

        [Test]
        public void SharedHosts_PreserveEnvelopeProtocolContract()
        {
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher());
            var message = new LoginRequest
            {
                PlayerId = "shared-player",
                Speed = 7
            };

            runtime.MessageManager.SendMessage(message, MessageType.LoginRequest, Sender);

            var envelope = Envelope.Parser.ParseFrom(transport.LastSendToData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.LoginRequest));
            Assert.That(LoginRequest.Parser.ParseFrom(envelope.Payload).PlayerId, Is.EqualTo("shared-player"));
            Assert.That(LoginRequest.Parser.ParseFrom(envelope.Payload).Speed, Is.EqualTo(7));
            Assert.That(transport.LastSendTarget, Is.EqualTo(Sender));
        }

        [Test]
        public void SharedNetworkRuntime_StartStop_WithDistinctSyncTransport_ControlsBothLanes()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(
                reliableTransport,
                new ImmediateNetworkMessageDispatcher(),
                syncTransport: syncTransport);

            runtime.StartAsync().GetAwaiter().GetResult();

            Assert.That(reliableTransport.StartCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.StartCallCount, Is.EqualTo(1));

            runtime.Stop();

            Assert.That(reliableTransport.StopCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.StopCallCount, Is.EqualTo(1));
        }

        [Test]
        public void SharedNetworkRuntime_RoutesMoveInputThroughConfiguredSyncLane()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(
                reliableTransport,
                new ImmediateNetworkMessageDispatcher(),
                syncTransport: syncTransport);
            var message = new MoveInput
            {
                PlayerId = "shared-player",
                Tick = 33,
                MoveX = 1f,
                MoveY = -1f
            };

            runtime.MessageManager.SendMessage(message, MessageType.MoveInput);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(0));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.LastSentData, Is.Not.Null);

            var envelope = Envelope.Parser.ParseFrom(syncTransport.LastSentData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.MoveInput));
            Assert.That(MoveInput.Parser.ParseFrom(envelope.Payload).Tick, Is.EqualTo(33));
        }

        [Test]
        public void ServerNetworkHost_StartsDistinctSyncTransport_AndTracksInboundActivityFromSyncLane()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var host = new ServerNetworkHost(reliableTransport, syncTransport: syncTransport);

            host.StartAsync().GetAwaiter().GetResult();
            syncTransport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender);

            Assert.That(reliableTransport.StartCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.StartCallCount, Is.EqualTo(1));
            Assert.That(host.ManagedSessions.Count, Is.EqualTo(1));
            Assert.That(host.TryGetSession(Sender, out var session), Is.True);
            Assert.That(session.SessionManager.State, Is.EqualTo(ConnectionState.TransportConnected));
        }

        private static byte[] BuildEnvelope(MessageType type, IMessage payload)
        {
            return new Envelope
            {
                Type = (int)type,
                Payload = payload.ToByteString()
            }.ToByteArray();
        }

        private sealed class FakeTransport : ITransport
        {
            public byte[] LastSentData { get; private set; }

            public byte[] LastSendToData { get; private set; }

            public IPEndPoint LastSendTarget { get; private set; }

            public int SendCallCount { get; private set; }

            public int StartCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public event Action<byte[], IPEndPoint> OnReceive;

            public Task StartAsync()
            {
                StartCallCount++;
                return Task.CompletedTask;
            }

            public void Stop()
            {
                StopCallCount++;
            }

            public void Send(byte[] data)
            {
                SendCallCount++;
                LastSentData = Copy(data);
            }

            public void SendTo(byte[] data, IPEndPoint target)
            {
                LastSendToData = Copy(data);
                LastSendTarget = target;
            }

            public void SendToAll(byte[] data)
            {
            }

            public void EmitReceive(byte[] data, IPEndPoint sender)
            {
                OnReceive?.Invoke(Copy(data), sender);
            }

            private static byte[] Copy(byte[] data)
            {
                if (data == null)
                {
                    return null;
                }

                var copy = new byte[data.Length];
                Array.Copy(data, copy, data.Length);
                return copy;
            }
        }
    }
}
