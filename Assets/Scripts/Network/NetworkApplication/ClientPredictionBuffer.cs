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

        public long? LastAcknowledgedMoveTick { get; private set; }

        /// <summary>
        /// Time of the last received authoritative state, used to compute
        /// actual elapsed wall-clock time for accumulation synchronization.
        /// </summary>
        private float _lastAuthoritativeStateTime = float.NegativeInfinity;

        /// <summary>
        /// Returns the wall-clock time of the last authoritative state arrival.
        /// Valid only after TryApplyAuthoritativeState has been called at least once.
        /// </summary>
        public float LastAuthoritativeStateTime => _lastAuthoritativeStateTime;

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

        /// <summary>
        /// Accumulate pending input duration using the actual elapsed wall-clock time
        /// since the last authoritative state, not the fixed simulation cadence.
        /// This synchronizes accumulation with the server's 20Hz authoritative cadence.
        /// </summary>
        public void AccumulateWithElapsedTime(float elapsedSinceLastState)
        {
            if (pendingInputs.Count == 0 || elapsedSinceLastState <= 0f || !float.IsFinite(elapsedSinceLastState))
            {
                return;
            }

            var latest = pendingInputs[^1];
            pendingInputs[^1] = new PredictedMoveStep(latest.Input, latest.SimulatedDurationSeconds + elapsedSinceLastState);
        }

        public bool TryApplyAuthoritativeState(PlayerState state, float currentTime, out IReadOnlyList<PredictedMoveStep> replayInputs)
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
            LastAcknowledgedMoveTick = state.AcknowledgedMoveTick;
            pendingInputs.RemoveAll(input => input.Input.Tick <= state.AcknowledgedMoveTick);
            replayInputs = pendingInputs.ToArray();

            // Reset the elapsed-time tracker so the next accumulation period
            // starts from this authoritative state's arrival time.
            _lastAuthoritativeStateTime = currentTime;
            return true;
        }
    }
}
