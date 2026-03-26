using System;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkTransport;
using NUnit.Framework;

namespace Tests.EditMode.Network
{
    public class MessageManagerTests
    {
        private static readonly IPEndPoint Sender = new(IPAddress.Loopback, 8080);

        [Test]
        public void SendMessage_WithoutTarget_UsesDefaultSend()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport);
            var message = new Heartbeat();

            manager.SendMessage(message, MessageType.Heartbeat);

            Assert.That(transport.SendCallCount, Is.EqualTo(1));
            Assert.That(transport.SendToCallCount, Is.EqualTo(0));
            Assert.That(transport.LastSentData, Is.Not.Null);

            var envelope = Envelope.Parser.ParseFrom(transport.LastSentData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.Heartbeat));
            Assert.That(envelope.Payload.ToByteArray(), Is.EqualTo(message.ToByteArray()));
        }

        [Test]
        public void SendMessage_WithTarget_UsesExplicitSend()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport);
            var message = new LoginRequest
            {
                PlayerId = "player-1",
                Speed = 5
            };

            manager.SendMessage(message, MessageType.LoginRequest, Sender);

            Assert.That(transport.SendCallCount, Is.EqualTo(0));
            Assert.That(transport.SendToCallCount, Is.EqualTo(1));
            Assert.That(transport.LastSendTarget, Is.EqualTo(Sender));

            var envelope = Envelope.Parser.ParseFrom(transport.LastSendToData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.LoginRequest));
            Assert.That(envelope.Payload.ToByteArray(), Is.EqualTo(message.ToByteArray()));
        }

        [Test]
        public void BroadcastMessage_UsesBroadcastSend()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport);
            var message = new Heartbeat();

            manager.BroadcastMessage(message, MessageType.Heartbeat);

            Assert.That(transport.SendToAllCallCount, Is.EqualTo(1));
            Assert.That(transport.LastBroadcastData, Is.Not.Null);

            var envelope = Envelope.Parser.ParseFrom(transport.LastBroadcastData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.Heartbeat));
            Assert.That(envelope.Payload.ToByteArray(), Is.EqualTo(message.ToByteArray()));
        }

        [Test]
        public void Receive_ValidEnvelope_DispatchesRegisteredHandler()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport);
            var handled = false;
            IPEndPoint receivedSender = null;
            byte[] receivedPayload = null;
            var message = new Heartbeat();

            manager.RegisterHandler(MessageType.Heartbeat, (payload, sender) =>
            {
                handled = true;
                receivedSender = sender;
                receivedPayload = payload;
            });

            transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, message), Sender);

            Assert.That(handled, Is.True);
            Assert.That(receivedSender, Is.EqualTo(Sender));
            Assert.That(receivedPayload, Is.EqualTo(message.ToByteArray()));
        }

        [Test]
        public void Receive_UnregisteredMessage_DoesNotThrow()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport);

            Assert.DoesNotThrow(() =>
                transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender));
        }

        [Test]
        public void Receive_InvalidBytes_DoesNotBreakFollowingDispatch()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport);
            var handledCount = 0;

            manager.RegisterHandler(MessageType.Heartbeat, (payload, sender) =>
            {
                handledCount++;
            });

            Assert.DoesNotThrow(() => transport.EmitReceive(new byte[] { 0x01, 0x02, 0x03 }, Sender));
            transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender);

            Assert.That(handledCount, Is.EqualTo(1));
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

            public byte[] LastBroadcastData { get; private set; }

            public int SendCallCount { get; private set; }

            public int SendToCallCount { get; private set; }

            public int SendToAllCallCount { get; private set; }

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
                SendCallCount++;
                LastSentData = Copy(data);
            }

            public void SendTo(byte[] data, IPEndPoint target)
            {
                SendToCallCount++;
                LastSendToData = Copy(data);
                LastSendTarget = target;
            }

            public void SendToAll(byte[] data)
            {
                SendToAllCallCount++;
                LastBroadcastData = Copy(data);
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
