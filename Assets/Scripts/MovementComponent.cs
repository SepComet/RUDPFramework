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
            MoveX = input.x,
            MoveY = input.z
        };
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
}

public class MovementComponent : MonoBehaviour
{
    [SerializeField] private float _sendInterval = 0.05f;
    private Player _master;
    private int _speed = 2;
    [SerializeField] private Rigidbody _rigid;
    private float _lastSendTime = 0;
    private bool _isControlled = false;

    private Vector3 _serverPosition;
    private bool _hasServerState = false;
    private PlayerState _lastServerState;

    public long Tick { get; private set; } = 0;
    private long _startTickOffset = 0;
    private long _currentTickOffset = 0;
    private readonly ClientPredictionBuffer _predictionBuffer = new ClientPredictionBuffer();

    private Vector3 _serverPos;
    private Vector3 _currentPos;
    private float _lerpTime;
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
        _rigid.interpolation = RigidbodyInterpolation.Interpolate;
        _rigid.isKinematic = !isControlled;
        _rigid.velocity = Vector3.zero;
        if (serverTick != 0 && _isControlled) MainUI.Instance.OnStartTickOffsetChanged(serverTick);
    }

    private void Update()
    {
        if (_isControlled)
        {
            _cachedMoveInput = CaptureMovement();
            var hasMovement = ClientGameplayInputFlow.HasPlanarInput(_cachedMoveInput);
            if (hasMovement)
            {
                _lastAimDirection = _cachedMoveInput;
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
                MainUI.Instance.OnServerPosChanged(_serverPosition);
                Reconcile(_lastServerState);
                _hasServerState = false;
            }

            Simulate(_cachedMoveInput);
        }
        else
        {
            _lerpTime += Time.fixedDeltaTime / 0.05f;
            _rigid.MovePosition(Vector3.Lerp(_currentPos, _serverPos, _lerpTime));
        }
    }

    private void Reconcile(PlayerState state)
    {
        if (!_predictionBuffer.TryApplyAuthoritativeState(state, out var replayInputs))
        {
            return;
        }

        _serverPosition = state.Position.ToVector3();
        _rigid.position = Vector3.Lerp(_rigid.position, _serverPosition, _lerpRate);
        _rigid.velocity = Vector3.zero;
        ReplayPendingInputs(replayInputs);
    }

    private Vector3 CaptureMovement()
    {
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
    }

    private ShootInput CaptureShootInput()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return null;
        }

        return ClientGameplayInputFlow.CreateShootInput(_master.PlayerId, Tick, ResolveAimDirection());
    }

    private Vector3 ResolveAimDirection()
    {
        if (ClientGameplayInputFlow.HasPlanarInput(_lastAimDirection))
        {
            return _lastAimDirection;
        }

        var forward = _master != null ? _master.transform.forward : transform.forward;
        var planarForward = new Vector3(forward.x, 0f, forward.z);
        return ClientGameplayInputFlow.HasPlanarInput(planarForward) ? planarForward : Vector3.forward;
    }

    private void Simulate(Vector3 input)
    {
        _rigid.velocity = _speed * input;
        if (_isControlled)
        {
            MainUI.Instance.OnClientPosChanged(_rigid.position);
        }
    }

    public void OnServerState(PlayerState state)
    {
        if (_isControlled)
        {
            if (_predictionBuffer.LastAuthoritativeTick.HasValue &&
                state.Tick <= _predictionBuffer.LastAuthoritativeTick.Value)
            {
                return;
            }

            _lastServerState = state;
            _hasServerState = true;
        }
        else
        {
            if (_lastServerState != null && state.Tick < _lastServerState.Tick)
            {
                return;
            }

            _lastServerState = state;
            _serverPos = state.Position.ToVector3();
            _currentPos = _rigid.position;
            _lerpTime = 0f;
        }
    }

    public void SetServerTick(long serverTick)
    {
        _currentTickOffset = serverTick - Tick - _startTickOffset;
        if (_isControlled)
        {
            MainUI.Instance.OnServerTickChanged(serverTick);
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

    private void ReplayPendingInputs(IReadOnlyList<MoveInput> replayInputs)
    {
        foreach (var replayInput in replayInputs)
        {
            _rigid.position += _speed * new Vector3(replayInput.MoveX, 0f, replayInput.MoveY) * _sendInterval;
        }

        if (_isControlled)
        {
            MainUI.Instance.OnClientPosChanged(_rigid.position);
        }
    }
}
