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
            var parsed = MoveInput.Parser.ParseFrom(envelope.Payload);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.MoveInput));
            Assert.That(parsed.PlayerId, Is.EqualTo("player-1"));
            Assert.That(parsed.Tick, Is.EqualTo(12));
            Assert.That(parsed.MoveX, Is.EqualTo(1));
            Assert.That(parsed.MoveY, Is.EqualTo(-1));
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
            var parsed = ShootInput.Parser.ParseFrom(envelope.Payload);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.ShootInput));
            Assert.That(parsed.PlayerId, Is.EqualTo("player-1"));
            Assert.That(parsed.Tick, Is.EqualTo(15));
            Assert.That(parsed.DirX, Is.EqualTo(0.5f));
            Assert.That(parsed.DirY, Is.EqualTo(1f));
            Assert.That(parsed.TargetId, Is.EqualTo("enemy-1"));
        }

        [Test]
        public void SendMessage_PlayerState_UsesSyncLanePolicyAndPreservesAuthoritativeFields()
        {
            var reliableTransport = new FakeTransport();
            var syncTransport = new FakeTransport();
            var manager = new MessageManager(
                reliableTransport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport);
            var message = new PlayerState
            {
                PlayerId = "player-1",
                Tick = 21,
                Position = new global::Network.Defines.Vector3 { X = 3f, Y = 0f, Z = -2f },
                Velocity = new global::Network.Defines.Vector3 { X = 1f, Y = 0f, Z = 0.5f },
                Rotation = 90f,
                Hp = 87
            };

            manager.SendMessage(message, MessageType.PlayerState);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(0));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(1));

            var envelope = Envelope.Parser.ParseFrom(syncTransport.LastSentData);
            var parsed = PlayerState.Parser.ParseFrom(envelope.Payload);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.PlayerState));
            Assert.That(parsed.PlayerId, Is.EqualTo("player-1"));
            Assert.That(parsed.Tick, Is.EqualTo(21));
            Assert.That(parsed.Position.X, Is.EqualTo(3f));
            Assert.That(parsed.Position.Z, Is.EqualTo(-2f));
            Assert.That(parsed.Velocity.X, Is.EqualTo(1f));
            Assert.That(parsed.Velocity.Z, Is.EqualTo(0.5f));
            Assert.That(parsed.Rotation, Is.EqualTo(90f));
            Assert.That(parsed.Hp, Is.EqualTo(87));
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
                Damage = 7,
                HitPosition = new global::Network.Defines.Vector3 { X = 8f, Y = 0f, Z = 4f }
            };

            manager.SendMessage(message, MessageType.CombatEvent);

            Assert.That(reliableTransport.SendCallCount, Is.EqualTo(1));
            Assert.That(syncTransport.SendCallCount, Is.EqualTo(0));

            var envelope = Envelope.Parser.ParseFrom(reliableTransport.LastSentData);
            var parsed = CombatEvent.Parser.ParseFrom(envelope.Payload);
            Assert.That(envelope.Type, Is.EqualTo((int)MessageType.CombatEvent));
            Assert.That(parsed.Tick, Is.EqualTo(20));
            Assert.That(parsed.EventType, Is.EqualTo(CombatEventType.DamageApplied));
            Assert.That(parsed.AttackerId, Is.EqualTo("player-1"));
            Assert.That(parsed.TargetId, Is.EqualTo("enemy-1"));
            Assert.That(parsed.Damage, Is.EqualTo(7));
            Assert.That(parsed.HitPosition.X, Is.EqualTo(8f));
            Assert.That(parsed.HitPosition.Z, Is.EqualTo(4f));
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
        public void Receive_InvalidEnvelope_DoesNotThrow()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());

            Assert.DoesNotThrow(() => transport.EmitReceive(new byte[] { 1, 2, 3 }, Sender));
            Assert.That(manager.PendingMessageCount, Is.EqualTo(0));
        }

        [Test]
        public void Receive_UnknownMessageType_IsIgnored()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(transport, new MainThreadNetworkDispatcher());
            var handled = false;

            manager.RegisterHandler(MessageType.Heartbeat, (payload, sender) => handled = true);

            transport.EmitReceive(BuildEnvelope(unchecked((MessageType)999), new Heartbeat()), Sender);

            Assert.That(handled, Is.False);
            Assert.That(manager.PendingMessageCount, Is.EqualTo(0));
        }

        [Test]
        public void Receive_StaleMoveInput_IsDropped()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(
                transport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport: null,
                syncSequenceTracker: new SyncSequenceTracker());
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

            Assert.That(handledTicks, Is.EqualTo(new[] { 8L }));
        }

        [Test]
        public void Receive_StalePlayerState_IsDropped()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(
                transport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport: null,
                syncSequenceTracker: new SyncSequenceTracker());
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

            Assert.That(handledTicks, Is.EqualTo(new[] { 8L }));
        }

        [Test]
        public void Receive_ShootInput_IsNotDroppedBySequenceTracker()
        {
            var transport = new FakeTransport();
            var manager = new MessageManager(
                transport,
                new MainThreadNetworkDispatcher(),
                new DefaultMessageDeliveryPolicyResolver(),
                syncTransport: null,
                syncSequenceTracker: new SyncSequenceTracker());
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

            Assert.That(handledTicks, Is.EqualTo(new[] { 8L, 6L }));
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

            public byte[] LastBroadcastData { get; private set; }

            public IPEndPoint LastSendTarget { get; private set; }

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
