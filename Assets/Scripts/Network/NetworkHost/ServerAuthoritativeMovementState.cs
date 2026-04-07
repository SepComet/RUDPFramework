using System;
using System.Net;

namespace Network.NetworkHost
{
    public sealed class ServerAuthoritativeMovementState
    {
        public ServerAuthoritativeMovementState(IPEndPoint remoteEndPoint, string playerId, int hp, float speed = 5f)
        {
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            Hp = hp;
            IsDead = hp <= 0;
            Speed = speed;
        }

        public IPEndPoint RemoteEndPoint { get; }

        public string PlayerId { get; }

        public long LastAcceptedMoveTick { get; internal set; }

        public long LastAcceptedShootTick { get; internal set; }

        public long LastBroadcastTick { get; internal set; }

        public float PositionX { get; internal set; }

        public float PositionY { get; internal set; }

        public float PositionZ { get; internal set; }

        public float VelocityX { get; internal set; }

        public float VelocityY { get; internal set; }

        public float VelocityZ { get; internal set; }

        public float Rotation { get; internal set; }

        public int Hp { get; internal set; }

        public bool IsDead { get; internal set; }

        public float InputX { get; internal set; }

        public float InputY { get; internal set; }

        public float Speed { get; internal set; }
    }
}
