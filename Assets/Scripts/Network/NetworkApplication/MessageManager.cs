using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;
using Network.NetworkTransport;

namespace Network.NetworkApplication
{
    public class MessageManager
    {
        private readonly INetworkMessageLane reliableLane;
        private readonly INetworkMessageLane syncLane;
        private readonly INetworkMessageDispatcher dispatcher;
        private readonly IMessageDeliveryPolicyResolver deliveryPolicyResolver;
        private readonly SyncSequenceTracker syncSequenceTracker;

        private readonly Dictionary<MessageType, Func<byte[], IPEndPoint, Task>> handlers =
            new();

        public MessageManager(ITransport transport, INetworkMessageDispatcher dispatcher)
            : this(
                CreateLane(transport),
                dispatcher,
                new DefaultMessageDeliveryPolicyResolver(),
                null,
                new SyncSequenceTracker())
        {
        }

        public MessageManager(
            ITransport reliableTransport,
            INetworkMessageDispatcher dispatcher,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver,
            ITransport syncTransport = null,
            SyncSequenceTracker syncSequenceTracker = null)
            : this(
                CreateLane(reliableTransport),
                dispatcher,
                deliveryPolicyResolver,
                CreateLaneIfDistinct(reliableTransport, syncTransport),
                syncSequenceTracker)
        {
        }

        public MessageManager(
            INetworkMessageLane reliableLane,
            INetworkMessageDispatcher dispatcher,
            IMessageDeliveryPolicyResolver deliveryPolicyResolver = null,
            INetworkMessageLane syncLane = null,
            SyncSequenceTracker syncSequenceTracker = null)
        {
            this.reliableLane = reliableLane ?? throw new ArgumentNullException(nameof(reliableLane));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.deliveryPolicyResolver = deliveryPolicyResolver ?? new DefaultMessageDeliveryPolicyResolver();
            this.syncLane = syncLane;
            this.syncSequenceTracker = syncSequenceTracker ?? new SyncSequenceTracker();

            this.reliableLane.Received += OnTransportReceive;
            if (this.syncLane != null && !ReferenceEquals(this.syncLane, this.reliableLane))
            {
                this.syncLane.Received += OnTransportReceive;
            }
        }

        public INetworkMessageDispatcher Dispatcher => dispatcher;

        public int PendingMessageCount => dispatcher.PendingCount;

        public void RegisterHandler(MessageType type, IMessageHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            handlers[type] = (payload, sender) => handler.HandleAsync(payload, sender);

            Console.WriteLine($"[MessageManager] 注册处理器：{type}");
        }

        public void RegisterHandler(MessageType type, Func<byte[], IPEndPoint, Task> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            RegisterHandler(type, new DelegateMessageHandler(handler));
        }

        public void RegisterHandler(MessageType type, Action<byte[], IPEndPoint> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            RegisterHandler(type, new DelegateMessageHandler(handler));
        }

        public void SendMessage<T>(T message, MessageType type, IPEndPoint target = null) where T : IMessage
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var envelope = new Envelope()
            {
                Type = (int)type,
                Payload = message.ToByteString()
            };
            var lane = ResolveLane(type);

            if (target != null)
            {
                lane.SendTo(envelope.ToByteArray(), target);
            }
            else
            {
                lane.Send(envelope.ToByteArray());
            }

            Console.WriteLine($"[MessageManager] 发送消息：{type} -> {target?.ToString() ?? "default"}");
        }

        public void BroadcastMessage<T>(T message, MessageType type) where T : IMessage
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Console.WriteLine($"[MessageManager] 广播消息：{type}");
            var envelope = new Envelope()
            {
                Type = (int)type,
                Payload = message.ToByteString()
            };
            ResolveLane(type).SendToAll(envelope.ToByteArray());
        }

        public Task<int> DrainPendingMessagesAsync(int maxMessages = int.MaxValue)
        {
            return dispatcher.DrainAsync(maxMessages);
        }

        private void OnTransportReceive(byte[] data, IPEndPoint sender)
        {
            try
            {
                var envelope = Envelope.Parser.ParseFrom(data);
                var type = (MessageType)envelope.Type;
                Console.WriteLine($"[MessageManager] 收到消息：{type} 来自 {sender}");
                var payload = envelope.Payload.ToByteArray();

                if (!syncSequenceTracker.ShouldAccept(type, payload, sender))
                {
                    Console.WriteLine($"[MessageManager] 丢弃过期同步消息：{type} 来自 {sender}");
                    return;
                }

                if (handlers.TryGetValue(type, out var handler))
                {
                    dispatcher.Enqueue(() => DispatchAsync(handler, payload, sender, type));
                }
                else
                {
                    Console.WriteLine($"[MessageManager] 警告：未注册的消息类型 {type}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MessageManager] 消息处理错误：{ex.Message}");
            }
        }

        private static async Task DispatchAsync(
            Func<byte[], IPEndPoint, Task> handler,
            byte[] payload,
            IPEndPoint sender,
            MessageType type)
        {
            try
            {
                await handler(payload, sender);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MessageManager] Handler 执行错误：{type} -> {ex.Message}");
            }
        }

        private INetworkMessageLane ResolveLane(MessageType type)
        {
            var policy = deliveryPolicyResolver.Resolve(type);
            return policy == DeliveryPolicy.HighFrequencySync && syncLane != null
                ? syncLane
                : reliableLane;
        }

        private static INetworkMessageLane CreateLane(ITransport transport)
        {
            return new TransportMessageLane(transport ?? throw new ArgumentNullException(nameof(transport)));
        }

        private static INetworkMessageLane CreateLaneIfDistinct(ITransport reliableTransport, ITransport syncTransport)
        {
            if (syncTransport == null)
            {
                return null;
            }

            return ReferenceEquals(reliableTransport, syncTransport)
                ? null
                : CreateLane(syncTransport);
        }
    }
}
