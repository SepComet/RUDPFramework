using UnityEngine;

public static class TankMovementKinematics
{
    private const float TurnSpeedDegreesPerSecond = 180f;
    private const float UnityYawOffsetDegrees = 90f;

    public static void ApplyStep(int speed, float turnInput, float throttleInput, float deltaTime,
        ref Vector3 position, ref Quaternion rotation)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        var clampedTurnInput = Mathf.Clamp(turnInput, -1f, 1f);
        var clampedThrottleInput = Mathf.Clamp(throttleInput, -1f, 1f);
        var heading = NormalizeDegrees(UnityYawToHeading(rotation.eulerAngles.y) +
                                       (clampedTurnInput * TurnSpeedDegreesPerSecond * deltaTime));
        rotation = Quaternion.Euler(0f, HeadingToUnityYaw(heading), 0f);

        var forward = ResolveHeadingForward(heading);
        var velocity = forward * (clampedThrottleInput * speed);
        position += velocity * deltaTime;
    }

    public static Vector3 ResolveHeadingForward(float headingDegrees)
    {
        var rotationRadians = headingDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rotationRadians), 0f, Mathf.Sin(rotationRadians));
    }

    public static float HeadingToUnityYaw(float headingDegrees)
    {
        return NormalizeDegrees(UnityYawOffsetDegrees - headingDegrees);
    }

    public static float UnityYawToHeading(float unityYawDegrees)
    {
        return NormalizeDegrees(UnityYawOffsetDegrees - unityYawDegrees);
    }

    public static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }
}
