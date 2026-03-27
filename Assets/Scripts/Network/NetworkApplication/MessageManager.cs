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
        private readonly ITransport transport;
        private readonly INetworkMessageDispatcher dispatcher;

        private readonly Dictionary<MessageType, Func<byte[], IPEndPoint, Task>> handlers =
            new();

        public MessageManager(ITransport transport, INetworkMessageDispatcher dispatcher)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.transport.OnReceive += OnTransportReceive;
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

            if (target != null)
            {
                transport.SendTo(envelope.ToByteArray(), target);
            }
            else
            {
                transport.Send(envelope.ToByteArray());
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
            transport.SendToAll(envelope.ToByteArray());
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

                if (handlers.TryGetValue(type, out var handler))
                {
                    var payload = envelope.Payload.ToByteArray();
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
    }
}
