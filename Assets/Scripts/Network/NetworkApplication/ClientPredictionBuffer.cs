using System;
using System.Collections.Generic;
using Network.Defines;

namespace Network.NetworkApplication
{
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

        /// <summary>
        /// 清空所有 pending inputs。 
        /// 用于 Reconcile 后清理已重放的输入，避免它们的时间被继续累积。 
        /// </summary>
        public void ClearPendingInputs()
        {
            pendingInputs.Clear();
        }

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

        public bool TryGetNextUnsimulatedInput(out PredictedMoveStep predictedMoveStep)
        {
            for (var i = 0; i < pendingInputs.Count; i++)
            {
                if (pendingInputs[i].SimulatedDurationSeconds <= 0f)
                {
                    predictedMoveStep = pendingInputs[i];
                    return true;
                }
            }

            predictedMoveStep = default;
            return false;
        }

        public void MarkInputSimulated(long tick, float simulatedDurationSeconds)
        {
            if (simulatedDurationSeconds <= 0f)
            {
                return;
            }

            for (var i = 0; i < pendingInputs.Count; i++)
            {
                if (pendingInputs[i].Input.Tick != tick)
                {
                    continue;
                }

                pendingInputs[i] = new PredictedMoveStep(pendingInputs[i].Input, simulatedDurationSeconds);
                return;
            }
        }

        public bool TryApplyAuthoritativeState(PlayerState state, float currentTime,
            out IReadOnlyList<PredictedMoveStep> replayInputs)
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
            replayInputs = pendingInputs.FindAll(input => input.SimulatedDurationSeconds > 0f);

            // Reset the elapsed-time tracker so the next accumulation period
            // starts from this authoritative state's arrival time.
            _lastAuthoritativeStateTime = currentTime;
            return true;
        }

        /// <summary>
        /// 只清除已确认的旧输入，不触发 replay，不更新 LastAuthoritativeTick。
        /// 用于在校正被禁用时，保持 predictionBuffer 的输入与服务端同步。
        /// </summary>
        public void PruneAcknowledgedInputs(long acknowledgedMoveTick)
        {
            if (acknowledgedMoveTick <= 0)
            {
                return;
            }

            pendingInputs.RemoveAll(input => input.Input.Tick <= acknowledgedMoveTick);
            LastAcknowledgedMoveTick = acknowledgedMoveTick;
        }
    }
}
