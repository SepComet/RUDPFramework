using System;
using System.Threading.Tasks;

namespace Network.NetworkApplication
{
    public sealed class ImmediateNetworkMessageDispatcher : INetworkMessageDispatcher
    {
        public int PendingCount => 0;

        public void Enqueue(Func<Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItem().GetAwaiter().GetResult();
        }

        public Task<int> DrainAsync(int maxItems = int.MaxValue)
        {
            if (maxItems <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItems));
            }

            return Task.FromResult(0);
        }
    }
}
