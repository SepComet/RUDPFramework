using System;

namespace Network.NetworkHost
{
    public sealed class ServerAuthoritativeMovementConfiguration
    {
        public float MoveSpeed { get; set; } = 5f;

        public TimeSpan BroadcastInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        public int DefaultHp { get; set; } = 100;

        internal void Validate()
        {
            if (float.IsNaN(MoveSpeed) || float.IsInfinity(MoveSpeed) || MoveSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(MoveSpeed), "Move speed must be finite and non-negative.");
            }

            if (BroadcastInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(BroadcastInterval), "Broadcast interval must be positive.");
            }
        }
    }
}
