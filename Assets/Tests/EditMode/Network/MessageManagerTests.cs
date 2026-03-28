using System;
using System.Collections.Generic;
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
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
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
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
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
        public void SendMessage_MoveInput_UsesSyncLanePolicy()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var manager = new MessageManager(
                reliableTransport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport);
            var message = new MoveInput
            {
                PlayerId = "player-1",
                Tick = 12,
                MoveX = 1,
                MoveY = -1
            };

            manager.SendMessage(message, MessageType.MoveInput);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(0));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(1));

            var envelope = Envelope.Parser.ParseFrom(syncTransport.LastSentData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.MoveInput));
            Assert.That(MoveInput.Parser.ParseFrom(envelope.Payload).Tick, Is.EqualTo(12));
        }

        [Test]
        public void SendMessage_ShootInput_UsesReliableLanePolicy()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var manager = new MessageManager(
                reliableTransport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport);
            var message = new ShootInput
            {
                PlayerId = "player-1",
                Tick = 15,
                DirX = 0.5f,
                DirY = 1f,
                TargetId = "enemy-1"
            };

            manager.SendMessage(message, MessageType.ShootInput);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(0));

            var envelope = Envelope.Parser.ParseFrom(reliableTransport.LastSentData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.ShootInput));
            Assert.That(ShootInput.Parser.ParseFrom(envelope.Payload).TargetId, Is.EqualTo("enemy-1"));
        }

        [Test]
        public void SendMessage_CombatEvent_UsesReliableLanePolicy()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var manager = new MessageManager(
                reliableTransport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport);
            var message = new CombatEvent
            {
                Tick = 20,
                EventType = CombatEventType.DamageApplied,
                AttackerId = "player-1",
                TargetId = "enemy-1",
                Damage = 7
            };

            manager.SendMessage(message, MessageType.CombatEvent);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(0));

            var envelope = Envelope.Parser.ParseFrom(reliableTransport.LastSentData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.CombatEvent));
            Assert.That(CombatEvent.Parser.ParseFrom(envelope.Payload).Damage, Is.EqualTo(7));
        }

        [Test]
        public void SendMessage_Heartbeat_UsesReliableLanePolicy()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var manager = new MessageManager(
                reliableTransport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport);
            var message = new Heartbeat();

            manager.SendMessage(message, MessageType.Heartbeat);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(0));

            var envelope = Envelope.Parser.ParseFrom(reliableTransport.LastSentData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.Heartbeat));
            Assert.That(envelope.Payload.ToByteArray(), Is.EqualTo(message.ToByteArray()));
        }

        [Test]
        public void BroadcastMessage_UsesBroadcastSend()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var message = new Heartbeat();

            manager.BroadcastMessage(message, MessageType.Heartbeat);

            Assert.That(transport.SendToAllCallCount, Is.EqualTo(1));
            Assert.That(transport.LastBroadcastData, Is.Not.Null);

            var envelope = Envelope.Parser.ParseFrom(transport.LastBroadcastData);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.Heartbeat));
            Assert.That(envelope.Payload.ToByteArray(), Is.EqualTo(message.ToByteArray()));
        }

        [Test]
        public void Receive_ValidEnvelope_IsDeferredUntilDrain()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
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

            Assert.That(handled, Is.False);
            Assert.That(manager.PendingMessageCount, Is.EqualTo(1));

            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handled, Is.True);
            Assert.That(receivedSender, Is.EqualTo(Sender));
            Assert.That(receivedPayload, Is.EqualTo(message.ToByteArray()));
            Assert.That(manager.PendingMessageCount, Is.EqualTo(0));
        }

        [Test]
        public void Receive_UnregisteredMessage_DoesNotThrow()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());

            Assert.DoesNotThrow(() =>
                transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender));
            Assert.That(manager.PendingMessageCount, Is.EqualTo(0));
        }

        [Test]
        public void Receive_InvalidBytes_DoesNotBreakFollowingDispatch()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var handledCount = 0;

            manager.RegisterHandler(MessageType.Heartbeat, (payload, sender) =>
            {
                handledCount++;
            });

            Assert.DoesNotThrow(() => transport.EmitReceive(new byte[] { 0x01, 0x02, 0x03 }, Sender));
            transport.EmitReceive(BuildEnvelope(MessageType.Heartbeat, new Heartbeat()), Sender);

            Assert.That(handledCount, Is.EqualTo(0));
            Assert.That(manager.PendingMessageCount, Is.EqualTo(1));

            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handledCount, Is.EqualTo(1));
        }

        [Test]
        public void Receive_MultipleMessages_PreserveEnqueueOrder()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var handledSpeeds = new List<int>();

            manager.RegisterHandler(MessageType.LoginRequest, (payload, sender) =>
            {
                handledSpeeds.Add(LoginRequest.Parser.ParseFrom(payload).Speed);
            });

            transport.EmitReceive(BuildEnvelope(MessageType.LoginRequest, new LoginRequest { PlayerId = "a", Speed = 1 }), Sender);
            transport.EmitReceive(BuildEnvelope(MessageType.LoginRequest, new LoginRequest { PlayerId = "b", Speed = 2 }), Sender);

            Assert.That(handledSpeeds, Is.Empty);
            Assert.That(manager.PendingMessageCount, Is.EqualTo(2));

            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handledSpeeds, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Receive_StaleMoveInput_IsDropped()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var handledTicks = new List<long>();

            manager.RegisterHandler(MessageType.MoveInput, (payload, sender) =>
            {
                handledTicks.Add(MoveInput.Parser.ParseFrom(payload).Tick);
            });

            transport.EmitReceive(
                BuildEnvelope(MessageType.MoveInput, new MoveInput { PlayerId = "player-1", Tick = 8, MoveX = 1 }),
                Sender);
            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            transport.EmitReceive(
                BuildEnvelope(MessageType.MoveInput, new MoveInput { PlayerId = "player-1", Tick = 6, MoveX = -1 }),
                Sender);
            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handledTicks, Is.EqualTo(new long[] { 8 }));
        }

        [Test]
        public void Receive_StalePlayerState_IsDropped()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var handledTicks = new List<long>();

            manager.RegisterHandler(MessageType.PlayerState, (payload, sender) =>
            {
                handledTicks.Add(PlayerState.Parser.ParseFrom(payload).Tick);
            });

            transport.EmitReceive(
                BuildEnvelope(MessageType.PlayerState, new PlayerState { PlayerId = "player-1", Tick = 8 }),
                Sender);
            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            transport.EmitReceive(
                BuildEnvelope(MessageType.PlayerState, new PlayerState { PlayerId = "player-1", Tick = 6 }),
                Sender);
            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handledTicks, Is.EqualTo(new long[] { 8 }));
        }

        [Test]
        public void Receive_ShootInput_IsNotDroppedBySequenceTracker()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var handledTicks = new List<long>();

            manager.RegisterHandler(MessageType.ShootInput, (payload, sender) =>
            {
                handledTicks.Add(ShootInput.Parser.ParseFrom(payload).Tick);
            });

            transport.EmitReceive(
                BuildEnvelope(MessageType.ShootInput, new ShootInput { PlayerId = "player-1", Tick = 8, DirX = 1f }),
                Sender);
            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            transport.EmitReceive(
                BuildEnvelope(MessageType.ShootInput, new ShootInput { PlayerId = "player-1", Tick = 6, DirY = 1f }),
                Sender);
            manager.DrainPendingMessagesAsync().GetAwaiter().GetResult();

            Assert.That(handledTicks, Is.EqualTo(new long[] { 8, 6 }));
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