using Network.Defines;

namespace Network.NetworkApplication
{
    public interface IMessageDeliveryPolicyResolver
    {
        DeliveryPolicy Resolve(MessageType messageType);
    }
}
