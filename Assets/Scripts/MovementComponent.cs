using System.Collections.Generic;
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

    public static bool TryCreateMoveInput(string playerId, long tick, Vector3 input, bool stopMessagePending, out MoveInput message)
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

public class MovementComponent : MonoBehaviour
{
    [SerializeField] private float _sendInterval = 0.05f;
    private Player _master;
    private const float TurnSpeedDegreesPerSecond = 180f;
    private const float UnityYawOffsetDegrees = 90f;
    private int _speed = 2;
    [SerializeField] private Rigidbody _rigid;
    private float _lastSendTime = 0;
    private bool _isControlled = false;

    private Vector3 _serverPosition;
    private bool _hasServerState = false;
    private ClientAuthoritativePlayerStateSnapshot _lastAuthoritativeState;

    public long Tick { get; private set; } = 0;
    private long _startTickOffset = 0;
    private long _currentTickOffset = 0;
    private readonly ClientPredictionBuffer _predictionBuffer = new ClientPredictionBuffer();

    private readonly RemotePlayerSnapshotInterpolator _remoteSnapshotInterpolator = new();
    [SerializeField] private float _lerpRate = 0.1f;
    private Vector3 _cachedMoveInput;
    private Vector3 _lastAimDirection = Vector3.forward;
    private bool _wasMovingLastFrame;
    private bool _stopMessagePending;

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
        if (serverTick != 0 && _isControlled && MainUI.Instance != null) MainUI.Instance.OnStartTickOffsetChanged(serverTick);
    }

    private void Update()
    {
        if (_isControlled)
        {
            _cachedMoveInput = CaptureMovement();
            var hasMovement = ClientGameplayInputFlow.HasPlanarInput(_cachedMoveInput);
            if (hasMovement)
            {
                _stopMessagePending = false;
            }
            else if (_wasMovingLastFrame)
            {
                _stopMessagePending = true;
            }

            _wasMovingLastFrame = hasMovement;

            var shootInput = CaptureShootInput();
            if (shootInput != null)
            {
                NetworkManager.Instance.SendShootInput(shootInput);
            }

            if (Time.time - _lastSendTime > _sendInterval)
            {
                if (ClientGameplayInputFlow.TryCreateMoveInput(_master.PlayerId, Tick, _cachedMoveInput, _stopMessagePending, out var moveInput))
                {
                    NetworkManager.Instance.SendMoveInput(moveInput);
                    _predictionBuffer.Record(moveInput);
                    _stopMessagePending = false;
                }

                _lastSendTime = Time.time;
                Tick++;

                MainUI.Instance.OnClientTickChanged(Tick);
            }
        }
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

            Simulate(_cachedMoveInput);
            _predictionBuffer.AccumulateLatest(Time.fixedDeltaTime);
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
        if (!_predictionBuffer.TryApplyAuthoritativeState(snapshot.SourceState, out var replayInputs))
        {
            return;
        }

        _serverPosition = snapshot.Position;
        _rigid.position = _serverPosition;
        _rigid.rotation = snapshot.RotationQuaternion;
        _rigid.velocity = snapshot.Velocity;
        _rigid.angularVelocity = Vector3.zero;
        ReplayPendingInputs(replayInputs);
    }

    private Vector3 CaptureMovement()
    {
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
    }

    private ShootInput CaptureShootInput()
    {
        return ClientGameplayInputFlow.TryCreateShootInput(
            _master.PlayerId,
            Tick,
            Input.GetMouseButtonDown(0),
            ResolveAimDirection(),
            out var shootInput)
            ? shootInput
            : null;
    }

    private Vector3 ResolveAimDirection()
    {
        var planarForward = Vector3.ProjectOnPlane(_rigid.transform.forward, Vector3.up);
        if (ClientGameplayInputFlow.HasPlanarInput(planarForward))
        {
            _lastAimDirection = planarForward;
            return planarForward;
        }

        return ClientGameplayInputFlow.HasPlanarInput(_lastAimDirection) ? _lastAimDirection : ResolveHeadingForward(UnityYawToHeading(_rigid.rotation.eulerAngles.y));
    }

    private void Simulate(Vector3 input)
    {
        ApplyTankMovement(-input.x, input.z, Time.fixedDeltaTime);
        if (_isControlled)
        {
            if (MainUI.Instance != null)
            {
                MainUI.Instance.OnClientPosChanged(_rigid.position);
            }
        }
    }

    public void OnAuthoritativeState(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        if (_isControlled)
        {
            _lastAuthoritativeState = snapshot;
            _hasServerState = true;
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

        if (_currentTickOffset < 0)
        {
            _sendInterval = 0.052f;
        }
        if (_currentTickOffset > 0)
        {
            _sendInterval = 0.048f;
        }
    }

    private void ReplayPendingInputs(IReadOnlyList<PredictedMoveStep> replayInputs)
    {
        foreach (var replayInput in replayInputs)
        {
            ApplyTankMovement(replayInput.Input.TurnInput, replayInput.Input.ThrottleInput, replayInput.SimulatedDurationSeconds);
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
        var heading = NormalizeDegrees(UnityYawToHeading(_rigid.rotation.eulerAngles.y) + (clampedTurnInput * TurnSpeedDegreesPerSecond * deltaTime));
        _rigid.rotation = Quaternion.Euler(0f, HeadingToUnityYaw(heading), 0f);

        var forward = ResolveHeadingForward(heading);
        var velocity = forward * (clampedThrottleInput * _speed);
        _rigid.velocity = velocity;
        _rigid.position += velocity * deltaTime;
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
