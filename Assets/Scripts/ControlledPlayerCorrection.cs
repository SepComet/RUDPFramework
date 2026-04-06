using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public readonly struct ControlledPlayerCorrectionSettings
{
    public ControlledPlayerCorrectionSettings(
        float authoritativeCadenceSeconds,
        float moveSpeed,
        float turnSpeedDegreesPerSecond,
        float snapDistanceMultiplier = 3f,
        float snapAngleMultiplier = 15f)
    {
        AuthoritativeCadenceSeconds = Mathf.Max(0f, authoritativeCadenceSeconds);
        MoveSpeed = Mathf.Max(0f, moveSpeed);
        TurnSpeedDegreesPerSecond = Mathf.Max(0f, turnSpeedDegreesPerSecond);
        SnapDistanceMultiplier = Mathf.Max(1f, snapDistanceMultiplier);
        SnapAngleMultiplier = Mathf.Max(1f, snapAngleMultiplier);
    }

    public float AuthoritativeCadenceSeconds { get; }

    public float MoveSpeed { get; }

    public float TurnSpeedDegreesPerSecond { get; }

    public float SnapDistanceMultiplier { get; }

    public float SnapAngleMultiplier { get; }

    public float MaxBoundedPositionCorrection => MoveSpeed * AuthoritativeCadenceSeconds;

    public float MaxBoundedRotationCorrectionDegrees => TurnSpeedDegreesPerSecond * AuthoritativeCadenceSeconds;

    public float SnapPositionThreshold => MaxBoundedPositionCorrection * SnapDistanceMultiplier;

    public float SnapRotationThresholdDegrees => MaxBoundedRotationCorrectionDegrees * SnapAngleMultiplier;

    public int MaxCorrectionSteps => Mathf.Max(1, Mathf.CeilToInt(SnapDistanceMultiplier));
}

public readonly struct ControlledPlayerVisualCorrectionState
{
    public static ControlledPlayerVisualCorrectionState None => default;

    public ControlledPlayerVisualCorrectionState(Vector3 targetPosition, Quaternion targetRotation, int remainingStepBudget)
    {
        TargetPosition = targetPosition;
        TargetRotation = targetRotation;
        RemainingStepBudget = Mathf.Max(0, remainingStepBudget);
    }

    public Vector3 TargetPosition { get; }

    public Quaternion TargetRotation { get; }

    public int RemainingStepBudget { get; }

    public bool IsActive => RemainingStepBudget > 0;
}

public readonly struct ControlledPlayerCorrectionResult
{
    public ControlledPlayerCorrectionResult(
        Vector3 position,
        Quaternion rotation,
        bool usedHardSnap,
        ControlledPlayerVisualCorrectionState nextState)
    {
        Position = position;
        Rotation = rotation;
        UsedHardSnap = usedHardSnap;
        NextState = nextState;
    }

    public Vector3 Position { get; }

    public Quaternion Rotation { get; }

    public bool UsedHardSnap { get; }

    public ControlledPlayerVisualCorrectionState NextState { get; }
}

public static class ControlledPlayerCorrection
{
    public static ControlledPlayerCorrectionResult Resolve(
        Vector3 currentPosition,
        Quaternion currentRotation,
        Vector3 targetPosition,
        Quaternion targetRotation,
        ControlledPlayerCorrectionSettings settings)
    {
        return Resolve(
            currentPosition,
            currentRotation,
            targetPosition,
            targetRotation,
            settings,
            ControlledPlayerVisualCorrectionState.None);
    }

    public static ControlledPlayerCorrectionResult Resolve(
        Vector3 currentPosition,
        Quaternion currentRotation,
        Vector3 targetPosition,
        Quaternion targetRotation,
        ControlledPlayerCorrectionSettings settings,
        ControlledPlayerVisualCorrectionState activeCorrection)
    {
        var positionError = Vector3.Distance(currentPosition, targetPosition);
        var rotationError = Quaternion.Angle(currentRotation, targetRotation);
        var boundedPositionCorrection = settings.MaxBoundedPositionCorrection;
        var boundedRotationCorrection = settings.MaxBoundedRotationCorrectionDegrees;

        if (positionError <= Mathf.Epsilon && rotationError <= Mathf.Epsilon)
        {
            return new ControlledPlayerCorrectionResult(targetPosition, targetRotation, false, ControlledPlayerVisualCorrectionState.None);
        }

        if (boundedPositionCorrection <= Mathf.Epsilon && boundedRotationCorrection <= Mathf.Epsilon)
        {
            return new ControlledPlayerCorrectionResult(targetPosition, targetRotation, true, ControlledPlayerVisualCorrectionState.None);
        }

        if (positionError > settings.SnapPositionThreshold || rotationError > settings.SnapRotationThresholdDegrees)
        {
            return new ControlledPlayerCorrectionResult(targetPosition, targetRotation, true, ControlledPlayerVisualCorrectionState.None);
        }

        var remainingStepBudget = activeCorrection.IsActive
            ? activeCorrection.RemainingStepBudget
            : settings.MaxCorrectionSteps;
        if (remainingStepBudget <= 0)
        {
            return new ControlledPlayerCorrectionResult(targetPosition, targetRotation, true, ControlledPlayerVisualCorrectionState.None);
        }

        var correctedPosition = Vector3.MoveTowards(currentPosition, targetPosition, boundedPositionCorrection);
        var correctedRotation = Quaternion.RotateTowards(currentRotation, targetRotation, boundedRotationCorrection);
        var nextPositionError = Vector3.Distance(correctedPosition, targetPosition);
        var nextRotationError = Quaternion.Angle(correctedRotation, targetRotation);
        if (nextPositionError <= Mathf.Epsilon && nextRotationError <= Mathf.Epsilon)
        {
            return new ControlledPlayerCorrectionResult(targetPosition, targetRotation, false, ControlledPlayerVisualCorrectionState.None);
        }

        remainingStepBudget--;
        if (remainingStepBudget <= 0)
        {
            return new ControlledPlayerCorrectionResult(targetPosition, targetRotation, true, ControlledPlayerVisualCorrectionState.None);
        }

        return new ControlledPlayerCorrectionResult(
            correctedPosition,
            correctedRotation,
            false,
            new ControlledPlayerVisualCorrectionState(targetPosition, targetRotation, remainingStepBudget));
    }
}
