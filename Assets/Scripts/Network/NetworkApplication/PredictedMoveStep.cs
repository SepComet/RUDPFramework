using System;
using Network.Defines;

namespace Network.NetworkApplication
{
    public readonly struct PredictedMoveStep
    {
        public PredictedMoveStep(MoveInput input, float simulatedDurationSeconds)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            SimulatedDurationSeconds = simulatedDurationSeconds < 0f ? 0f : simulatedDurationSeconds;
        }

        public MoveInput Input { get; }

        public float SimulatedDurationSeconds { get; }
    }
}
