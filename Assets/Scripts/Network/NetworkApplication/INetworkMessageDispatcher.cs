using System;
using System.Threading.Tasks;

namespace Network.NetworkApplication
{
    public interface INetworkMessageDispatcher
    {
        int PendingCount { get; }

        void Enqueue(Func<Task> workItem);

        Task<int> DrainAsync(int maxItems = int.MaxValue);
    }
}
