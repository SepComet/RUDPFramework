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

        private readonly Dictionary<MessageType, Func<byte[], IPEndPoint, Task>> handlers =
            new();

        public MessageManager(ITransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.transport.OnReceive += OnTransportReceiveAsync;
        }

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

        private async void OnTransportReceiveAsync(byte[] data, IPEndPoint sender)
        {
            try
            {
                var envelope = Envelope.Parser.ParseFrom(data);
                var type = (MessageType)envelope.Type;
                Console.WriteLine($"[MessageManager] 收到消息：{type} 来自 {sender}");

                if (handlers.TryGetValue(type, out var handler))
                {
                    await handler(envelope.Payload.ToByteArray(), sender);
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
    }
}
