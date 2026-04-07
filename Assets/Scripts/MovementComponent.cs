using System.Collections.Generic;
using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class MovementComponent : MonoBehaviour
{
    [SerializeField] private int _speed = 2;
    [SerializeField] private Rigidbody _rigid;
    [SerializeField] private InputComponent _inputComponent;

    private Player _master;
    private const float TurnSpeedDegreesPerSecond = 180f;

    // 测试时设为 false，可接收服务器状态日志但不应用校正
    [SerializeField] private bool _applyServerCorrection = true;
    private const float UnityYawOffsetDegrees = 90f;

    // Server authoritative movement cadence used for replay substepping.
    // This matches ServerAuthoritativeMovementConfiguration.SimulationInterval (50ms).
    private const float kServerSimulationStepSeconds = 0.05f;

    private bool _isControlled = false;

    private Vector3 _serverPosition;
    private bool _hasServerState = false;
    private ClientAuthoritativePlayerStateSnapshot _lastAuthoritativeState;
    private ControlledPlayerVisualCorrectionState _activeVisualCorrection;

    public long Tick { get; private set; } = 0;
    private long _startTickOffset = 0;
    private long _currentTickOffset = 0;
    private float _simulationAccumulator = 0f;
    private readonly ClientPredictionBuffer _predictionBuffer = new ClientPredictionBuffer();

    private readonly RemotePlayerSnapshotInterpolator _remoteSnapshotInterpolator = new();
    [SerializeField] private float _lerpRate = 0.1f;
    private Vector3 _lastAimDirection = Vector3.forward;

    public void Init(bool isControlled, Player master, ClientMovementBootstrap bootstrap)
    {
        Init(isControlled, master, bootstrap.AuthoritativeMoveSpeed, bootstrap.ServerTick);
    }

    public void Init(bool isControlled, Player master, int speed = 0, long serverTick = 0)
    {
        _master = master;
        _isControlled = isControlled;
        _speed = speed;
        _startTickOffset = serverTick;
        _rigid.interpolation = isControlled ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        _rigid.isKinematic = !isControlled;
        _rigid.velocity = Vector3.zero;
        _rigid.angularVelocity = Vector3.zero;

        // 设置 InputComponent 的 playerId
        if (_inputComponent != null)
        {
            _inputComponent.InjectPlayerId(master.PlayerId);
        }

        if (serverTick != 0 && _isControlled && MainUI.Instance != null)
            MainUI.Instance.OnStartTickOffsetChanged(serverTick);
    }

    private void Update()
    {
        if (_isControlled)
        {
            if (_inputComponent != null)
            {
                MainUI.Instance.OnClientTickChanged(_inputComponent.CurrentTick);
            }
        }
    }

    private void Start()
    {
        // 订阅 InputComponent 的事件来记录预测输入
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
        // 记录到预测缓冲区，用于后续的服务器状态校正和回放
        _predictionBuffer.Record(moveInput);
    }

    /// <summary>
    /// 测试用：设置是否应用服务器状态校正（默认 true）
    /// 设为 false 时只打印服务器状态日志，不影响本地位置
    /// </summary>
    public void SetApplyServerCorrection(bool apply)
    {
        _applyServerCorrection = apply;
    }

    private void FixedUpdate()
    {
        if (_isControlled)
        {
            if (_hasServerState)
            {
                if (MainUI.Instance != null)
                {
                    MainUI.Instance.OnServerPosChanged(_serverPosition);
                }

                Reconcile(_lastAuthoritativeState);
                _hasServerState = false;
            }

            // 累积时间，按服务端 50ms 步长进行模拟
            _simulationAccumulator += Time.fixedDeltaTime;
            while (_simulationAccumulator >= kServerSimulationStepSeconds)
            {
                // 使用最近发送的 MoveInput（来自 predictionBuffer）而非实时输入，
                // 确保客户端与服务端的输入时序一致
                Simulate(GetLatestPredictedInput());
                _simulationAccumulator -= kServerSimulationStepSeconds;
            }

            // 注意：模拟时间现在在 Simulate() 内部通过 AccumulateLatest 累加
        }
        else
        {
            var sample = _remoteSnapshotInterpolator.Sample(Time.time);
            if (sample.HasValue)
            {
                _rigid.MovePosition(sample.Position);
                _rigid.MoveRotation(sample.Rotation);
                _rigid.velocity = sample.Velocity;
            }
        }
    }

    private void Reconcile(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        _serverPosition = snapshot.Position;
        if (!_predictionBuffer.TryApplyAuthoritativeState(snapshot.SourceState, Time.time, out var replayInputs))
        {
            return;
        }

        var predictedPosition = _rigid.position;
        var predictedRotation = _rigid.rotation;
        var correction = ControlledPlayerCorrection.Resolve(
            predictedPosition,
            predictedRotation,
            snapshot.Position,
            snapshot.RotationQuaternion,
            new ControlledPlayerCorrectionSettings(kServerSimulationStepSeconds, _speed, TurnSpeedDegreesPerSecond,
                snapDistanceMultiplier: 5f),
            _activeVisualCorrection);

        _activeVisualCorrection = correction.NextState;
        _rigid.position = correction.Position;
        _rigid.rotation = correction.Rotation;
        _rigid.velocity = correction.UsedHardSnap ? snapshot.Velocity : Vector3.zero;
        _rigid.angularVelocity = Vector3.zero;

        // 位置已被校正到服务器位置，不需要再 ReplayPendingInputs
        // 因为 pendingInputs 中的 SimulatedDurationSeconds 是累积的模拟时间，
        // 如果用来 replay 会导致多余的移动。清空 pendingInputs 让客户端从校正位置重新开始
        if (replayInputs.Count > 0)
        {
            _predictionBuffer.ClearPendingInputs();
        }

        // 清零 accumulator 防止 FixedUpdate 中再次 Simulate 导致重复移动
        _simulationAccumulator = 0f;

        if (MainUI.Instance != null)
        {
            MainUI.Instance.OnCorrectionMagnitudeChanged?.Invoke(
                predictedPosition,
                snapshot.Position,
                correction.PositionError,
                correction.RotationErrorDegrees);
            MainUI.Instance.OnAcknowledgedMoveTickChanged?.Invoke(_predictionBuffer.LastAcknowledgedMoveTick ?? 0);
        }
    }

    private Vector3 ResolveAimDirection()
    {
        var planarForward = Vector3.ProjectOnPlane(_rigid.transform.forward, Vector3.up);
        if (ClientGameplayInputFlow.HasPlanarInput(planarForward))
        {
            _lastAimDirection = planarForward;
            return planarForward;
        }

        return ClientGameplayInputFlow.HasPlanarInput(_lastAimDirection)
            ? _lastAimDirection
            : ResolveHeadingForward(UnityYawToHeading(_rigid.rotation.eulerAngles.y));
    }

    private void Simulate(Vector3 input)
    {
        ApplyTankMovement(-input.x, input.z, kServerSimulationStepSeconds);

        // 每次 Simulate 后累加模拟时间（用于 Reconcile 时的重放）
        _predictionBuffer.AccumulateLatest(kServerSimulationStepSeconds);

        if (_isControlled)
        {
            if (MainUI.Instance != null)
            {
                MainUI.Instance.OnClientPosChanged(_rigid.position);
            }

            // 打印客户端当前状态，用于与服务端状态对比
            Debug.Log($"[ClientState] Tick={Tick} " +
                      $"Pos=({_rigid.position.x:F3}, {_rigid.position.y:F3}, {_rigid.position.z:F3}) " +
                      $"Rot={_rigid.rotation.eulerAngles.y:F2}");
        }
    }

    public void OnAuthoritativeState(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        if (_isControlled)
        {
            _lastAuthoritativeState = snapshot;

            // 打印服务端状态，用于与客户端计算结果对比
            Debug.Log($"[ServerState] Tick={snapshot.SourceState.Tick} " +
                      $"Pos=({snapshot.SourceState.Position.X:F3}, {snapshot.SourceState.Position.Y:F3}, {snapshot.SourceState.Position.Z:F3}) " +
                      $"Rot={snapshot.SourceState.Rotation:F2} " +
                      $"Vel=({snapshot.SourceState.Velocity.X:F3}, {snapshot.SourceState.Velocity.Y:F3}, {snapshot.SourceState.Velocity.Z:F3}) " +
                      $"AckTick={snapshot.AcknowledgedMoveTick}");

            // 清理已确认的旧输入，确保客户端使用正确的（已确认的）输入
            var pendingBefore = _predictionBuffer.PendingInputs.Count;
            _predictionBuffer.PruneAcknowledgedInputs(snapshot.AcknowledgedMoveTick);
            var pendingAfter = _predictionBuffer.PendingInputs.Count;
            Debug.Log(
                $"[Prune] AckTick={snapshot.AcknowledgedMoveTick} removed {pendingBefore - pendingAfter}/{pendingBefore} inputs, remaining={pendingAfter}");

            // 收到服务器状态后，必须清空 pendingInputs
            // 因为 pendingInputs 中的 SimulatedDurationSeconds 是累积的模拟时间，
            // 如果不清理，客户端会继续用这些输入移动（测试模式下位置不被服务器校正）
            _predictionBuffer.ClearPendingInputs();
            _simulationAccumulator = 0f;

            // 只有开启校正时才设置 _hasServerState，否则只打印日志不应用
            if (_applyServerCorrection)
            {
                _hasServerState = true;
            }
        }
        else
        {
            _lastAuthoritativeState = snapshot;
            _remoteSnapshotInterpolator.TryAddSnapshot(snapshot, Time.time);
        }
    }

    public void SetServerTick(long serverTick)
    {
        _currentTickOffset = serverTick - Tick - _startTickOffset;
        if (_isControlled)
        {
            if (MainUI.Instance != null)
            {
                MainUI.Instance.OnServerTickChanged(serverTick);
            }
        }
    }

    /// <summary>
    /// 获取最近发送的 MoveInput，用于与服务器输入时序对齐。
    /// 如果没有记录的输入，返回零向量（停止状态）。
    /// </summary>
    private Vector3 GetLatestPredictedInput()
    {
        var pending = _predictionBuffer.PendingInputs;
        if (pending.Count == 0)
        {
            Debug.Log("[MoveInput] No pending inputs, using zero (stop)");
            return Vector3.zero;
        }

        var latest = pending[^1];
        Debug.Log(
            $"[MoveInput] Using tick={latest.Input.Tick} TurnInput={latest.Input.TurnInput} ThrottleInput={latest.Input.ThrottleInput} ({pending.Count} pending)");
        // MoveInput 的 TurnInput/ThrottleInput 转回 Unity 的 x/z 格式
        // 注意 TurnInput 在 MoveInput 里是正数=右，正数=-input.x=左（需要取反）
        // ThrottleInput 在 MoveInput 里正数=前进，正数=input.z=前
        return new Vector3(-latest.Input.TurnInput, 0f, latest.Input.ThrottleInput);
    }

    private void ReplayPendingInputs(IReadOnlyList<PredictedMoveStep> replayInputs)
    {
        foreach (var replayInput in replayInputs)
        {
            var remaining = replayInput.SimulatedDurationSeconds;
            while (remaining > 0f)
            {
                // Use the server's fixed cadence (50ms) as the substep size to ensure
                // replay trajectory matches live FixedUpdate prediction exactly.
                var step = Mathf.Min(remaining, kServerSimulationStepSeconds);
                ApplyTankMovement(
                    replayInput.Input.TurnInput,
                    replayInput.Input.ThrottleInput,
                    step);
                remaining -= step;
            }
        }

        if (_isControlled)
        {
            if (MainUI.Instance != null)
            {
                MainUI.Instance.OnClientPosChanged(_rigid.position);
            }
        }
    }

    private void ApplyTankMovement(float turnInput, float throttleInput, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            _rigid.velocity = Vector3.zero;
            return;
        }

        var clampedTurnInput = Mathf.Clamp(turnInput, -1f, 1f);
        var clampedThrottleInput = Mathf.Clamp(throttleInput, -1f, 1f);
        var heading = NormalizeDegrees(UnityYawToHeading(_rigid.rotation.eulerAngles.y) +
                                       (clampedTurnInput * TurnSpeedDegreesPerSecond * deltaTime));
        _rigid.rotation = Quaternion.Euler(0f, HeadingToUnityYaw(heading), 0f);

        var forward = ResolveHeadingForward(heading);
        var velocity = forward * (clampedThrottleInput * _speed);
        _rigid.velocity = velocity;
        _rigid.position += velocity * deltaTime;

        // 调试日志：打印每步计算细节
        Debug.Log($"[MoveStep] _speed={_speed} deltaTime={deltaTime:F4} throttle={clampedThrottleInput} " +
                  $"heading={heading:F2} velocity=({velocity.x:F3}, {velocity.y:F3}, {velocity.z:F3}) " +
                  $"pos=({_rigid.position.x:F3}, {_rigid.position.y:F3}, {_rigid.position.z:F3})");
    }

    private static Vector3 ResolveHeadingForward(float headingDegrees)
    {
        var rotationRadians = headingDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rotationRadians), 0f, Mathf.Sin(rotationRadians));
    }

    private static float HeadingToUnityYaw(float headingDegrees)
    {
        return NormalizeDegrees(UnityYawOffsetDegrees - headingDegrees);
    }

    private static float UnityYawToHeading(float unityYawDegrees)
    {
        return NormalizeDegrees(UnityYawOffsetDegrees - unityYawDegrees);
    }

    private static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }
}
