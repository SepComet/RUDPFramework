using System.Collections.Generic;
using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class MovementResolverComponent : MonoBehaviour
{
    private const float ServerSimulationStepSeconds = 0.05f;
    private const float SnapThreshold = 0.5f;

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
            _movement.Init(isControlled);
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
                var pendingCount = _predictionBuffer.PendingInputs.Count;
                if (pendingCount == 0)
                {
                    _simulationAccumulator = 0f;
                    break;
                }

                Simulate(GetLatestPredictedInput());
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

    private void Simulate(Vector3 input)
    {
        TankMovementKinematics.ApplyStep(_speed, input.x, input.z, ServerSimulationStepSeconds,
            ref _predictedPosition, ref _predictedRotation);
        _movement.SetTargetPose(_predictedPosition, _predictedRotation);
        _predictionBuffer.AccumulateLatest(ServerSimulationStepSeconds);

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
        if (error > SnapThreshold)
        {
            _movement.SnapToPose(_predictedPosition, _predictedRotation);
        }
        else
        {
            _movement.SetTargetPose(_predictedPosition, _predictedRotation);
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

    private Vector3 GetLatestPredictedInput()
    {
        var pending = _predictionBuffer.PendingInputs;
        if (pending.Count == 0)
        {
            return Vector3.zero;
        }

        var latest = pending[^1];
        return new Vector3(-latest.Input.TurnInput, 0f, latest.Input.ThrottleInput);
    }

    private void ReplayPendingInputs(IReadOnlyList<PredictedMoveStep> replayInputs)
    {
        foreach (var replayInput in replayInputs)
        {
            var remaining = replayInput.SimulatedDurationSeconds;
            while (remaining > 0f)
            {
                var step = Mathf.Min(remaining, ServerSimulationStepSeconds);
                TankMovementKinematics.ApplyStep(
                    _speed,
                    replayInput.Input.TurnInput,
                    replayInput.Input.ThrottleInput,
                    step,
                    ref _predictedPosition,
                    ref _predictedRotation);
                remaining -= step;
            }
        }
    }
}
