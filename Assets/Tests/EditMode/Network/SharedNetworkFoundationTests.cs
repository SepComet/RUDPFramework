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
                TurnInput = 1f,
                ThrottleInput = -1f
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

        [Test]
        public void NetworkIntegrationFactory_CreateClientRuntime_WithSyncPort_UsesDistinctTransportsPerLane()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var runtime = NetworkIntegrationFactory.CreateClientRuntime(
                "127.0.0.1",
                8080,
                new ImmediateNetworkMessageDispatcher(),
                syncPort: 8081,
                transportFactory: (serverIp, port) =>
                {
                    var transport = new FakeTransport();
                    createdTransports.Add(port, transport);
                    return transport;
                });
            var moveInput = new MoveInput
            {
                PlayerId = "shared-player",
                Tick = 77,
                ThrottleInput = 1f
            };

            runtime.MessageManager.SendMessage(moveInput, MessageType.MoveInput);
            runtime.MessageManager.SendMessage(new Heartbeat(), MessageType.Heartbeat);

            Assert.That(createdTransports.Keys, Is.EquivalentTo(new[] { 8080, 8081 }));
            Assert.That(runtime.Transport, Is.SameAs(createdTransports[8080]));
            Assert.That(runtime.SyncTransport, Is.SameAs(createdTransports[8081]));
            Assert.That(createdTransports[8080].SendCallCount, Is.EqualTo(1));
            Assert.That(createdTransports[8081].SendCallCount, Is.EqualTo(1));
        }

        [Test]
        public void NetworkIntegrationFactory_CreateServerHost_WithSyncPort_UsesDistinctTransportsPerLane()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var host = NetworkIntegrationFactory.CreateServerHost(
                9000,
                syncPort: 9001,
                transportFactory: port =>
                {
                    var transport = new FakeTransport();
                    createdTransports.Add(port, transport);
                    return transport;
                });
            var moveInput = new MoveInput
            {
                PlayerId = "server-player",
                Tick = 88,
                ThrottleInput = 1f
            };

            host.MessageManager.SendMessage(moveInput, MessageType.MoveInput);
            host.MessageManager.SendMessage(new Heartbeat(), MessageType.Heartbeat);

            Assert.That(createdTransports.Keys, Is.EquivalentTo(new[] { 9000, 9001 }));
            Assert.That(host.Transport, Is.SameAs(createdTransports[9000]));
            Assert.That(host.SyncTransport, Is.SameAs(createdTransports[9001]));
            Assert.That(createdTransports[9000].SendCallCount, Is.EqualTo(1));
            Assert.That(createdTransports[9001].SendCallCount, Is.EqualTo(1));
        }

        [Test]
        public void NetworkIntegrationFactory_CreateServerHost_WithoutSyncPort_PreservesSingleTransportFallback()
        {
            var reliableTransport = new FakeTransport();
            var host = NetworkIntegrationFactory.CreateServerHost(
                9000,
                transportFactory: _ => reliableTransport);
            var moveInput = new MoveInput
            {
                PlayerId = "fallback-player",
                Tick = 99,
                TurnInput = -1f
            };

            host.MessageManager.SendMessage(moveInput, MessageType.MoveInput);

            Assert.That(host.Transport, Is.SameAs(reliableTransport));
            Assert.That(host.SyncTransport, Is.Null);
            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(1));
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
