using System.Collections.Generic;
using System.Linq;
using Network.Defines;

namespace Network.NetworkApplication
{
    /// <summary>
    /// Resolves the delivery policy for each <see cref="MessageType"/>.
    /// Policies are intentionally explicit here so that adding a new <see cref="MessageType"/>
    /// requires an intentional decision rather than silently falling through to a default.
    /// </summary>
    public sealed class DefaultMessageDeliveryPolicyResolver : IMessageDeliveryPolicyResolver
    {
        private static readonly IReadOnlyDictionary<MessageType, DeliveryPolicy> Policies =
            new Dictionary<MessageType, DeliveryPolicy>
            {
                // High-frequency sync lane: latest-wins, stale-drop.
                // These messages are sent every frame and a stale value is never useful.
                { MessageType.MoveInput, DeliveryPolicy.HighFrequencySync },
                { MessageType.PlayerState, DeliveryPolicy.HighFrequencySync },

                // Reliable ordered lane: guaranteed delivery, ordered.
                // ShootInput carries player intent; losing or reordering it changes gameplay outcomes.
                { MessageType.ShootInput, DeliveryPolicy.ReliableOrdered },

                // CombatEvent is a server-authoritative result; clients must receive it reliably
                // and in order to maintain consistent HP/death state.
                { MessageType.CombatEvent, DeliveryPolicy.ReliableOrdered },

                // PlayerJoin carries spawn data that the client needs to instantiate a player.
                // Reliable ordered ensures the client receives it before gameplay begins.
                { MessageType.PlayerJoin, DeliveryPolicy.ReliableOrdered },

                // Login/logout are session-control messages that must not be lost or reordered.
                { MessageType.LoginRequest, DeliveryPolicy.ReliableOrdered },
                { MessageType.LoginResponse, DeliveryPolicy.ReliableOrdered },
                { MessageType.LogoutRequest, DeliveryPolicy.ReliableOrdered },

                // Heartbeat carries server tick used for clock sync; a missing sample is
                // simply a lost sample — no value in stale delivery.
                { MessageType.Heartbeat, DeliveryPolicy.ReliableOrdered },
                { MessageType.HeartbeatResponse, DeliveryPolicy.ReliableOrdered },
            };

        public DeliveryPolicy Resolve(MessageType messageType)
        {
            if (Policies.TryGetValue(messageType, out var policy))
            {
                return policy;
            }

            // A new MessageType was added without an explicit policy decision.
            // Fail fast so the omission is noticed rather than silently defaulting.
            throw new System.ArgumentException(
                $"MessageType '{messageType}' has no assigned {nameof(DeliveryPolicy)}. " +
                "Add an explicit entry in the policy dictionary.",
                nameof(messageType));
        }
    }
}
