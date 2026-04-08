using System.Collections.Generic;
using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class MovementResolverComponent : MonoBehaviour
{
    private const float ServerSimulationStepSeconds = 0.05f;
    [SerializeField] private float SnapThreshold = 0.5f;
    private const float TurnSpeedDegreesPerSecond = 180f;

    [SerializeField] private int _speed = 2;
    [SerializeField] private MovementComponent _movement;
    [SerializeField] private InputComponent _inputComponent;
    [SerializeField] private bool _applyServerCorrection = true;

    private Player _master;
    private bool _isControlled;
    private Vector3 _serverPosition;
    private ClientAuthoritativePlayerStateSnapshot _lastAuthoritativeState;

    private Vector3 _authoritativePosition;
    private Quaternion _authoritativeRotation;
    private Vector3 _predictedPosition;
    private Quaternion _predictedRotation;

    public long Tick { get; private set; }
    private long _startTickOffset;
    private long _currentTickOffset;
    private float _simulationAccumulator;
    private readonly ClientPredictionBuffer _predictionBuffer = new ClientPredictionBuffer();
    private readonly RemotePlayerSnapshotInterpolator _remoteSnapshotInterpolator = new();

    private void Awake()
    {
        _movement ??= GetComponent<MovementComponent>();
        _inputComponent ??= GetComponent<InputComponent>();
    }

    public void Init(bool isControlled, Player master, ClientMovementBootstrap bootstrap)
    {
        Init(isControlled, master, bootstrap.AuthoritativeMoveSpeed, bootstrap.ServerTick);
    }

    public void Init(bool isControlled, Player master, int speed = 0, long serverTick = 0)
    {
        _movement ??= GetComponent<MovementComponent>();
        _inputComponent ??= GetComponent<InputComponent>();

        _master = master;
        _isControlled = isControlled;
        _speed = speed;
        _startTickOffset = serverTick;

        if (_movement != null)
        {
            _movement.Init(isControlled, _speed, TurnSpeedDegreesPerSecond);
            _authoritativePosition = _movement.CurrentPosition;
            _authoritativeRotation = _movement.CurrentRotation;
            _predictedPosition = _movement.CurrentPosition;
            _predictedRotation = _movement.CurrentRotation;
        }

        if (_inputComponent != null && master != null)
        {
            _inputComponent.InjectPlayerId(master.PlayerId);
        }

        if (serverTick != 0 && _isControlled && MainUI.Instance != null)
        {
            MainUI.Instance.OnStartTickOffsetChanged(serverTick);
        }
    }

    private void Update()
    {
        if (_isControlled && _inputComponent != null && MainUI.Instance != null)
        {
            MainUI.Instance.OnClientTickChanged(_inputComponent.CurrentTick);
        }
    }

    private void Start()
    {
        if (_inputComponent != null)
        {
            _inputComponent.OnMoveInputCreated += HandleMoveInputCreated;
        }
    }

    private void OnDestroy()
    {
        if (_inputComponent != null)
        {
            _inputComponent.OnMoveInputCreated -= HandleMoveInputCreated;
        }
    }

    private void HandleMoveInputCreated(MoveInput moveInput)
    {
        _predictionBuffer.Record(moveInput);
    }

    public void SetApplyServerCorrection(bool apply)
    {
        _applyServerCorrection = apply;
    }

    private void FixedUpdate()
    {
        if (_movement == null)
        {
            return;
        }

        if (_isControlled)
        {
            _simulationAccumulator += Time.fixedDeltaTime;
            while (_simulationAccumulator >= ServerSimulationStepSeconds)
            {
                if (!_predictionBuffer.TryGetNextUnsimulatedInput(out var nextInput))
                {
                    _simulationAccumulator = 0f;
                    break;
                }

                Simulate(nextInput.Input);
                _predictionBuffer.MarkInputSimulated(nextInput.Input.Tick, ServerSimulationStepSeconds);
                _simulationAccumulator -= ServerSimulationStepSeconds;
            }

            return;
        }

        var sample = _remoteSnapshotInterpolator.Sample(Time.time);
        if (sample.HasValue)
        {
            _movement.SnapToPose(sample.Position, sample.Rotation);
        }
    }

    private void Simulate(MoveInput moveInput)
    {
        var simulationTurnInput = ToSimulationTurnInput(moveInput.TurnInput);
        TankMovementKinematics.ApplyStep(
            _speed,
            simulationTurnInput,
            moveInput.ThrottleInput,
            ServerSimulationStepSeconds,
            ref _predictedPosition, ref _predictedRotation);
        _movement.SetExpectedTurnInput(simulationTurnInput);
        _movement.SetTargetPose(_predictedPosition, _predictedRotation);

        if (MainUI.Instance != null)
        {
            MainUI.Instance.OnClientPosChanged(_movement.CurrentPosition);
        }
    }

    public void OnAuthoritativeState(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        if (_isControlled)
        {
            if (!_applyServerCorrection)
            {
                return;
            }

            _lastAuthoritativeState = snapshot;
            Reconcile(snapshot);
            return;
        }

        _lastAuthoritativeState = snapshot;
        _remoteSnapshotInterpolator.TryAddSnapshot(snapshot, Time.time);
    }

    private void Reconcile(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        _serverPosition = snapshot.Position;
        if (!_predictionBuffer.TryApplyAuthoritativeState(snapshot.SourceState, Time.time, out var replayInputs))
        {
            return;
        }

        _authoritativePosition = snapshot.Position;
        _authoritativeRotation = snapshot.RotationQuaternion;
        _predictedPosition = _authoritativePosition;
        _predictedRotation = _authoritativeRotation;

        ReplayPendingInputs(replayInputs);

        var error = Vector3.Distance(_movement.CurrentPosition, _predictedPosition);
        var shouldSnap = error > SnapThreshold;
        Debug.Log(
            $"[Reconcile] tick={snapshot.SourceState.Tick} ack={snapshot.AcknowledgedMoveTick} " +
            $"error={error:F3} threshold={SnapThreshold:F3} snap={shouldSnap} " +
            $"current=({_movement.CurrentPosition.x:F3},{_movement.CurrentPosition.y:F3},{_movement.CurrentPosition.z:F3}) " +
            $"predicted=({_predictedPosition.x:F3},{_predictedPosition.y:F3},{_predictedPosition.z:F3}) " +
            $"authoritative=({_authoritativePosition.x:F3},{_authoritativePosition.y:F3},{_authoritativePosition.z:F3})");
        if (shouldSnap)
        {
            _movement.SnapToPose(_predictedPosition, _predictedRotation);
        }
        else
        {
            _movement.BlendToPoseFromCurrent(_predictedPosition, _predictedRotation);
        }

        _simulationAccumulator = 0f;

        if (MainUI.Instance != null)
        {
            MainUI.Instance.OnServerPosChanged(_serverPosition);
            MainUI.Instance.OnCorrectionMagnitudeChanged?.Invoke(
                _predictedPosition,
                snapshot.Position,
                error,
                Quaternion.Angle(_predictedRotation, snapshot.RotationQuaternion));
            MainUI.Instance.OnAcknowledgedMoveTickChanged?.Invoke(_predictionBuffer.LastAcknowledgedMoveTick ?? 0);
        }
    }

    public void SetServerTick(long serverTick)
    {
        _currentTickOffset = serverTick - Tick - _startTickOffset;
        if (_isControlled && MainUI.Instance != null)
        {
            MainUI.Instance.OnServerTickChanged(serverTick);
        }
    }

    private void ReplayPendingInputs(IReadOnlyList<PredictedMoveStep> replayInputs)
    {
        var lastSimulationTurnInput = 0f;
        foreach (var replayInput in replayInputs)
        {
            var remaining = replayInput.SimulatedDurationSeconds;
            while (remaining > 0f)
            {
                var step = Mathf.Min(remaining, ServerSimulationStepSeconds);
                var beforeYaw = _predictedRotation.eulerAngles.y;
                var simulationTurnInput = ToSimulationTurnInput(replayInput.Input.TurnInput);
                lastSimulationTurnInput = simulationTurnInput;
                TankMovementKinematics.ApplyStep(
                    _speed,
                    simulationTurnInput,
                    replayInput.Input.ThrottleInput,
                    step,
                    ref _predictedPosition,
                    ref _predictedRotation);
                var afterYaw = _predictedRotation.eulerAngles.y;
                Debug.Log(
                    $"[ReplayStep] authTick={_lastAuthoritativeState?.SourceState?.Tick ?? 0} " +
                    $"inputTick={replayInput.Input.Tick} netTurn={replayInput.Input.TurnInput:F2} simTurn={simulationTurnInput:F2} " +
                    $"throttle={replayInput.Input.ThrottleInput:F2} step={step:F3} " +
                    $"yaw={beforeYaw:F2}->{afterYaw:F2} " +
                    $"predicted=({_predictedPosition.x:F3},{_predictedPosition.y:F3},{_predictedPosition.z:F3})");
                remaining -= step;
            }
        }

        _movement.SetExpectedTurnInput(lastSimulationTurnInput);
    }

    private static float ToSimulationTurnInput(float networkTurnInput)
    {
        return -networkTurnInput;
    }
}
