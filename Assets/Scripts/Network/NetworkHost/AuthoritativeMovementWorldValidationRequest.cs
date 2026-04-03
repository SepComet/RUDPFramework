using System;
using System.Net;

namespace Network.NetworkHost
{
    public sealed class AuthoritativeMovementWorldValidationRequest
    {
        public AuthoritativeMovementWorldValidationRequest(
            IPEndPoint remoteEndPoint,
            string playerId,
            float currentPositionX,
            float currentPositionY,
            float currentPositionZ,
            float candidatePositionX,
            float candidatePositionY,
            float candidatePositionZ,
            float velocityX,
            float velocityY,
            float velocityZ)
        {
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            CurrentPositionX = currentPositionX;
            CurrentPositionY = currentPositionY;
            CurrentPositionZ = currentPositionZ;
            CandidatePositionX = candidatePositionX;
            CandidatePositionY = candidatePositionY;
            CandidatePositionZ = candidatePositionZ;
            VelocityX = velocityX;
            VelocityY = velocityY;
            VelocityZ = velocityZ;
        }

        public IPEndPoint RemoteEndPoint { get; }

        public string PlayerId { get; }

        public float CurrentPositionX { get; }

        public float CurrentPositionY { get; }

        public float CurrentPositionZ { get; }

        public float CandidatePositionX { get; }

        public float CandidatePositionY { get; }

        public float CandidatePositionZ { get; }

        public float VelocityX { get; }

        public float VelocityY { get; }

        public float VelocityZ { get; }
    }
}
