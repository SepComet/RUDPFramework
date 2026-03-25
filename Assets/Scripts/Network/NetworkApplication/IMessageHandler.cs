using System.Net;
using System.Threading.Tasks;

namespace Network.NetworkApplication
{
    public interface IMessageHandler
    {
        Task HandleAsync(byte[] message, IPEndPoint sender);
    }
}