using System;
using System.Collections.Generic;
using System.Linq;
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

    public sealed class ClientPredictionBuffer
    {
        private readonly List<PredictedMoveStep> pendingInputs = new();

        public long? LastAuthoritativeTick { get; private set; }

        public IReadOnlyList<PredictedMoveStep> PendingInputs => pendingInputs;

        public void Record(MoveInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (pendingInputs.Count > 0 && pendingInputs[^1].Input.Tick >= input.Tick)
            {
                return;
            }

            pendingInputs.Add(new PredictedMoveStep(input, 0f));
        }

        public void AccumulateLatest(float simulatedDurationSeconds)
        {
            if (pendingInputs.Count == 0 || simulatedDurationSeconds <= 0f)
            {
                return;
            }

            var latest = pendingInputs[^1];
            pendingInputs[^1] = new PredictedMoveStep(latest.Input, latest.SimulatedDurationSeconds + simulatedDurationSeconds);
        }

        public bool TryApplyAuthoritativeState(PlayerState state, out IReadOnlyList<PredictedMoveStep> replayInputs)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (LastAuthoritativeTick.HasValue && state.Tick <= LastAuthoritativeTick.Value)
            {
                replayInputs = Array.Empty<PredictedMoveStep>();
                return false;
            }

            LastAuthoritativeTick = state.Tick;
            pendingInputs.RemoveAll(input => input.Input.Tick <= state.Tick);
            replayInputs = pendingInputs.ToArray();
            return true;
        }
    }
}