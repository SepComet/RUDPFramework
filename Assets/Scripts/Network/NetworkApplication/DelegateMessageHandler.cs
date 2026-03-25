using System;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Network.Defines;

namespace Network.NetworkApplication
{
    public class DelegateMessageHandler : IMessageHandler
    {
        private readonly Func<byte[], IPEndPoint, Task> handler;

        public DelegateMessageHandler(Func<byte[], IPEndPoint, Task> handler)
        {
            this.handler = handler;
        }

        public DelegateMessageHandler(Action<byte[], IPEndPoint> handler)
        {
            this.handler = (msg, sender) =>
            {
                handler(msg, sender);
                return Task.CompletedTask;
            };
        }

        public Task HandleAsync(byte[] message, IPEndPoint sender)
        {
            return handler(message, sender);
        }
    }
}