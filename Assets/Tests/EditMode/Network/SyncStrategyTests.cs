using System.Collections.Generic;
using System.Net;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkHost;
using Network.NetworkTransport;
using NUnit.Framework;

namespace Tests.EditMode.Network
{
    public class SyncStrategyTests
    {
        [Test]
        public void ClientPredictionBuffer_AuthoritativeState_PrunesAcknowledgedInputs()
        {
            var buffer = new ClientPredictionBuffer();
            buffer.Record(new PlayerInput { PlayerId = "player-1", Tick = 10 });
            buffer.Record(new PlayerInput { PlayerId = "player-1", Tick = 11 });
            buffer.Record(new PlayerInput { PlayerId = "player-1", Tick = 12 });

            var accepted = buffer.TryApplyAuthoritativeState(
                new PlayerState { PlayerId = "player-1", Tick = 11 },
                out var replayInputs);

            Assert.That(accepted, Is.True);
            Assert.That(buffer.LastAuthoritativeTick, Is.EqualTo(11));
            Assert.That(replayInputs.Count, Is.EqualTo(1));
            Assert.That(replayInputs[0].Tick, Is.EqualTo(12));
            Assert.That(buffer.PendingInputs.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClientPredictionBuffer_StaleAuthoritativeState_IsIgnored()
        {
            var buffer = new ClientPredictionBuffer();
            buffer.Record(new PlayerInput { PlayerId = "player-1", Tick = 10 });
            buffer.TryApplyAuthoritativeState(new PlayerState { PlayerId = "player-1", Tick = 10 }, out _);

            var accepted = buffer.TryApplyAuthoritativeState(
                new PlayerState { PlayerId = "player-1", Tick = 9 },
                out var replayInputs);

            Assert.That(accepted, Is.False);
            Assert.That(replayInputs, Is.Empty);
            Assert.That(buffer.LastAuthoritativeTick, Is.EqualTo(10));
        }

        [Test]
        public void ClockSyncState_RejectsOlderSamples()
        {
            var clockSync = new ClockSyncState();

            var acceptedFirst = clockSync.ObserveSample(42);
            var acceptedSecond = clockSync.ObserveSample(41);

            Assert.That(acceptedFirst, Is.True);
            Assert.That(acceptedSecond, Is.False);
            Assert.That(clockSync.CurrentServerTick, Is.EqualTo(42));
        }

        [Test]
        public void SharedNetworkRuntime_AuthoritativeStateUpdatesClockWithoutChangingLifecycle()
        {
            var transport = new FakeTransport();
            var runtime = new SharedNetworkRuntime(transport, new ImmediateNetworkMessageDispatcher());

            runtime.StartAsync().GetAwaiter().GetResult();
            runtime.NotifyLoginStarted();
            runtime.NotifyLoginSucceeded();
            runtime.ObserveAuthoritativeState(88);

            Assert.That(runtime.SessionManager.State, Is.EqualTo(ConnectionState.LoggedIn));
            Assert.That(runtime.ClockSync.CurrentServerTick, Is.EqualTo(88));
        }

        [Test]
        public void ServerNetworkHost_RejectsStaleInputPerPeerWithoutCrossPeerInterference()
        {
            var transport = new FakeTransport();
            var host = new ServerNetworkHost(transport);
            var peerA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
            var peerB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5002);
            var handledTicksByPeer = new Dictionary<string, List<long>>();

            host.MessageManager.RegisterHandler(MessageType.PlayerInput, (payload, sender) =>
            {
                var key = sender.ToString();
                if (!handledTicksByPeer.TryGetValue(key, out var ticks))
                {
                    ticks = new List<long>();
                    handledTicksByPeer.Add(key, ticks);
                }

                ticks.Add(PlayerInput.Parser.ParseFrom(payload).Tick);
            });

            transport.EmitReceive(
                BuildEnvelope(MessageType.PlayerInput, new PlayerInput { PlayerId = "player-a", Tick = 5 }),
                peerA);
            transport.EmitReceive(
                BuildEnvelope(MessageType.PlayerInput, new PlayerInput { PlayerId = "player-a", Tick = 4 }),
                peerA);
            transport.EmitReceive(
                BuildEnvelope(MessageType.PlayerInput, new PlayerInput { PlayerId = "player-b", Tick = 4 }),
                peerB);

            Assert.That(handledTicksByPeer[peerA.ToString()], Is.EqualTo(new long[] { 5 }));
            Assert.That(handledTicksByPeer[peerB.ToString()], Is.EqualTo(new long[] { 4 }));
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
            public event System.Action<byte[], IPEndPoint> OnReceive;

            public System.Threading.Tasks.Task StartAsync()
            {
                return System.Threading.Tasks.Task.CompletedTask;
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
