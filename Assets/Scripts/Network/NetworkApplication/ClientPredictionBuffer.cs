using System;
using System.Collections.Generic;
using System.Linq;
using Network.Defines;

namespace Network.NetworkApplication
{
    public sealed class ClientPredictionBuffer
    {
        private readonly List<PlayerInput> pendingInputs = new();

        public long? LastAuthoritativeTick { get; private set; }

        public IReadOnlyList<PlayerInput> PendingInputs => pendingInputs;

        public void Record(PlayerInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (pendingInputs.Count > 0 && pendingInputs[^1].Tick >= input.Tick)
            {
                return;
            }

            pendingInputs.Add(input);
        }

        public bool TryApplyAuthoritativeState(PlayerState state, out IReadOnlyList<PlayerInput> replayInputs)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (LastAuthoritativeTick.HasValue && state.Tick <= LastAuthoritativeTick.Value)
            {
                replayInputs = Array.Empty<PlayerInput>();
                return false;
            }

            LastAuthoritativeTick = state.Tick;
            pendingInputs.RemoveAll(input => input.Tick <= state.Tick);
            replayInputs = pendingInputs.ToArray();
            return true;
        }
    }
}
