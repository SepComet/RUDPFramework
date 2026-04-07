using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public static class ClientGameplayInputFlow
{
    public static bool HasPlanarInput(Vector3 input)
    {
        return new Vector2(input.x, input.z).sqrMagnitude > 0f;
    }

    public static bool TryCreateMoveInput(string playerId, long tick, Vector3 input, bool stopMessagePending,
        out MoveInput message)
    {
        if (!HasPlanarInput(input) && !stopMessagePending)
        {
            message = null;
            return false;
        }

        message = new MoveInput
        {
            PlayerId = playerId,
            Tick = tick,
            TurnInput = -input.x,
            ThrottleInput = input.z
        };
        return true;
    }

    public static bool TryCreateShootInput(
        string playerId,
        long tick,
        bool fireTriggered,
        Vector3 aimDirection,
        out ShootInput message,
        string targetId = "")
    {
        if (!fireTriggered)
        {
            message = null;
            return false;
        }

        message = CreateShootInput(playerId, tick, aimDirection, targetId);
        return true;
    }

    public static ShootInput CreateShootInput(string playerId, long tick, Vector3 aimDirection, string targetId = "")
    {
        var planarDirection = new Vector3(aimDirection.x, 0f, aimDirection.z);
        if (planarDirection.sqrMagnitude <= 0f)
        {
            planarDirection = Vector3.forward;
        }
        else
        {
            planarDirection.Normalize();
        }

        return new ShootInput
        {
            PlayerId = playerId,
            Tick = tick,
            DirX = planarDirection.x,
            DirY = planarDirection.z,
            TargetId = targetId ?? string.Empty
        };
    }

    public static void SendShootInput(
        MessageManager messageManager,
        string playerId,
        long tick,
        Vector3 aimDirection,
        string targetId = "")
    {
        if (messageManager == null)
        {
            throw new System.ArgumentNullException(nameof(messageManager));
        }

        SendShootInput(messageManager, CreateShootInput(playerId, tick, aimDirection, targetId));
    }

    public static void SendShootInput(MessageManager messageManager, ShootInput message)
    {
        if (messageManager == null)
        {
            throw new System.ArgumentNullException(nameof(messageManager));
        }

        if (message == null)
        {
            throw new System.ArgumentNullException(nameof(message));
        }

        messageManager.SendMessage(message, MessageType.ShootInput);
    }
}
