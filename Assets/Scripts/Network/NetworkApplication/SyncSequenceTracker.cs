using System;
using System.Collections.Generic;
using System.Net;
using Network.Defines;

namespace Network.NetworkApplication
{
    public sealed class SyncSequenceTracker
    {
        private readonly object gate = new();
        private readonly Dictionary<string, long> latestSequenceByStream = new();

        public bool ShouldAccept(MessageType messageType, byte[] payload, IPEndPoint sender)
        {
            if (!TryResolveSequence(messageType, payload, sender, out var streamKey, out var sequence))
            {
                return true;
            }

            lock (gate)
            {
                if (latestSequenceByStream.TryGetValue(streamKey, out var latestSequence) &&
                    sequence < latestSequence)
                {
                    return false;
                }

                latestSequenceByStream[streamKey] = sequence;
                return true;
            }
        }

        private static bool TryResolveSequence(
            MessageType messageType,
            byte[] payload,
            IPEndPoint sender,
            out string streamKey,
            out long sequence)
        {
            switch (messageType)
            {
                case MessageType.PlayerInput:
                {
                    var input = PlayerInput.Parser.ParseFrom(payload);
                    streamKey = $"input:{Normalize(sender)}:{input.PlayerId}";
                    sequence = input.Tick;
                    return true;
                }

                case MessageType.PlayerState:
                {
                    var state = PlayerState.Parser.ParseFrom(payload);
                    streamKey = $"state:{state.PlayerId}";
                    sequence = state.Tick;
                    return true;
                }

                default:
                    streamKey = null;
                    sequence = 0;
                    return false;
            }
        }

        private static string Normalize(IPEndPoint sender)
        {
            return sender == null ? "unknown" : sender.ToString();
        }
    }
}
