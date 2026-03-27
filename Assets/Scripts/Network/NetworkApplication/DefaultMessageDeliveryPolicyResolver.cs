using System.Collections.Generic;
using Network.Defines;

namespace Network.NetworkApplication
{
    public sealed class DefaultMessageDeliveryPolicyResolver : IMessageDeliveryPolicyResolver
    {
        private static readonly IReadOnlyDictionary<MessageType, DeliveryPolicy> DefaultPolicies =
            new Dictionary<MessageType, DeliveryPolicy>
            {
                { MessageType.PlayerInput, DeliveryPolicy.HighFrequencySync },
                { MessageType.PlayerState, DeliveryPolicy.HighFrequencySync }
            };

        public DeliveryPolicy Resolve(MessageType messageType)
        {
            return DefaultPolicies.TryGetValue(messageType, out var policy)
                ? policy
                : DeliveryPolicy.ReliableOrdered;
        }
    }
}
