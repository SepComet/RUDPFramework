using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Network.NetworkApplication
{
    public sealed class MainThreadNetworkDispatcher : INetworkMessageDispatcher
    {
        private readonly ConcurrentQueue<Func<Task>> pendingWork = new();

        public int PendingCount => pendingWork.Count;

        public void Enqueue(Func<Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            pendingWork.Enqueue(workItem);
        }

        public async Task<int> DrainAsync(int maxItems = int.MaxValue)
        {
            if (maxItems <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItems));
            }

            var processed = 0;

            while (processed < maxItems && pendingWork.TryDequeue(out var workItem))
            {
                await workItem();
                processed++;
            }

            return processed;
        }
    }
}
