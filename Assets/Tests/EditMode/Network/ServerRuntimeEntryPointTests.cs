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
    public class ServerRuntimeEntryPointTests
    {
        private static readonly IPEndPoint Peer = new(IPAddress.Loopback, 9100);

        [Test]
        public void StartServerRuntimeAsync_ReliableOnly_StartsAndExposesServerHost()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                TransportFactory = port => CreateTransport(createdTransports, port)
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            Assert.That(createdTransports.Keys, Is.EquivalentTo(new[] { 9000 }));
            Assert.That(runtime.IsRunning, Is.True);
            Assert.That(runtime.Host.Transport, Is.SameAs(createdTransports[9000]));
            Assert.That(runtime.Host.SyncTransport, Is.Null);
            Assert.That(createdTransports[9000].StartCallCount, Is.EqualTo(1));
        }

        [Test]
        public void StartServerRuntimeAsync_DualTransport_StartsBothConfiguredLanes()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                TransportFactory = port => CreateTransport(createdTransports, port)
            };

            using var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();

            Assert.That(createdTransports.Keys, Is.EquivalentTo(new[] { 9000, 9001 }));
            Assert.That(runtime.Host.Transport, Is.SameAs(createdTransports[9000]));
            Assert.That(runtime.Host.SyncTransport, Is.SameAs(createdTransports[9001]));
            Assert.That(createdTransports[9000].StartCallCount, Is.EqualTo(1));
            Assert.That(createdTransports[9001].StartCallCount, Is.EqualTo(1));
        }

        [Test]
        public void StartServerRuntimeAsync_SyncTransportStartFails_RollsBackStartedResources()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                SyncPort = 9001,
                TransportFactory = port =>
                {
                    var transport = CreateTransport(createdTransports, port);
                    if (port == 9001)
                    {
                        transport.StartException = new InvalidOperationException("sync failed");
                    }

                    return transport;
                }
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult());

            Assert.That(exception.Message, Is.EqualTo("sync failed"));
            Assert.That(createdTransports[9000].StartCallCount, Is.EqualTo(1));
            Assert.That(createdTransports[9000].StopCallCount, Is.EqualTo(1));
            Assert.That(createdTransports[9001].StartCallCount, Is.EqualTo(1));
            Assert.That(createdTransports[9001].StopCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ServerRuntimeHandle_DrainsMessages_ExposesManagedSessions_AndStopsIdempotently()
        {
            var createdTransports = new Dictionary<int, FakeTransport>();
            var configuration = new ServerRuntimeConfiguration(9000)
            {
                Dispatcher = new MainThreadNetworkDispatcher(),
                TransportFactory = port => CreateTransport(createdTransports, port)
            };
            var runtime = ServerRuntimeEntryPoint.StartAsync(configuration).GetAwaiter().GetResult();
            var handled = false;

            runtime.Host.MessageManager.RegisterHandler(MessageType.Heartbeat, (payload, sender) =>
            {
                handled = true;
            });

            createdTransports[9000].EmitReceive(CreateEnvelope(MessageType.Heartbeat, new Heartbeat()), Peer);

            Assert.That(runtime.ManagedSessions.Count, Is.EqualTo(1));
            Assert.That(runtime.TryGetSession(Peer, out var session), Is.True);
            Assert.That(session.SessionManager.State, Is.EqualTo(ConnectionState.TransportConnected));
            Assert.That(runtime.Host.MessageManager.PendingMessageCount, Is.EqualTo(1));

            runtime.DrainPendingMessagesAsync().GetAwaiter().GetResult();
            runtime.UpdateLifecycle();

            Assert.That(handled, Is.True);
            Assert.That(runtime.Host.MessageManager.PendingMessageCount, Is.EqualTo(0));

            runtime.Stop();
            runtime.Stop();

            Assert.That(runtime.IsRunning, Is.False);
            Assert.That(runtime.ManagedSessions.Count, Is.EqualTo(0));
            Assert.That(createdTransports[9000].StopCallCount, Is.EqualTo(1));
        }

        private static FakeTransport CreateTransport(IDictionary<int, FakeTransport> createdTransports, int port)
        {
            var transport = new FakeTransport();
            createdTransports.Add(port, transport);
            return transport;
        }

        private static byte[] CreateEnvelope(MessageType type, IMessage payload)
        {
            return new Envelope
            {
                Type = (int)type,
                Payload = payload.ToByteString()
            }.ToByteArray();
        }

        private sealed class FakeTransport : ITransport
        {
            public Exception StartException { get; set; }

            public int StartCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public event Action<byte[], IPEndPoint> OnReceive;

            public Task StartAsync()
            {
                StartCallCount++;
                if (StartException != null)
                {
                    throw StartException;
                }

                return Task.CompletedTask;
            }

            public void Stop()
            {
                StopCallCount++;
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
