using System;

namespace Network.NetworkApplication
{
    public sealed class ClockSyncState
    {
        private readonly Func<DateTimeOffset> utcNowProvider;

        public ClockSyncState(Func<DateTimeOffset> utcNowProvider = null)
        {
            this.utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
        }

        public long? CurrentServerTick { get; private set; }

        public DateTimeOffset? LastSampleReceivedAtUtc { get; private set; }

        public bool ObserveSample(long? serverTick)
        {
            if (!serverTick.HasValue)
            {
                return false;
            }

            if (CurrentServerTick.HasValue && serverTick.Value < CurrentServerTick.Value)
            {
                return false;
            }

            CurrentServerTick = serverTick.Value;
            LastSampleReceivedAtUtc = utcNowProvider();
            return true;
        }
    }
}
