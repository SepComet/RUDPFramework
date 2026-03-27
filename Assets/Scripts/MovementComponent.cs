using System.Collections.Generic;
using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

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
    private PlayerInput _cachedInput;

    public void Init(bool isControlled, Player master, int speed = 0, long serverTick = 0)
    {
        this._master = master;
        this._isControlled = isControlled;
        this._speed = speed;
        this._startTickOffset = serverTick;
        _rigid.interpolation = RigidbodyInterpolation.Interpolate;
        _rigid.isKinematic = !isControlled;
        _rigid.velocity = Vector3.zero;
        if (serverTick != 0 && _isControlled) MainUI.Instance.OnStartTickOffsetChanged(serverTick);
    }

    private void Update()
    {
        if (_isControlled)
        {
            _cachedInput = CaptureInput();

            if (Time.time - _lastSendTime > _sendInterval)
            {
                if (_cachedInput != null)
                {
                    NetworkManager.Instance.SendPlayerInput(_cachedInput);
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

            Simulate(_cachedInput);
            if (_cachedInput != null)
            {
                _predictionBuffer.Record(_cachedInput);
            }
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

    private PlayerInput CaptureInput()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        if (input == Vector3.zero) return null;
        return new PlayerInput()
        {
            PlayerId = _master.PlayerId,
            Input = ProtoExtensions.ToProtoVector3(input),
            Tick = Tick
        };
    }

    private void Simulate(PlayerInput input)
    {
        Vector3 dir = input == null ? Vector3.zero : input.Input.ToVector3();
        _rigid.velocity = _speed * dir;
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

    private void ReplayPendingInputs(IReadOnlyList<PlayerInput> replayInputs)
    {
        foreach (var replayInput in replayInputs)
        {
            _rigid.position += _speed * replayInput.Input.ToVector3() * _sendInterval;
        }

        if (_isControlled)
        {
            MainUI.Instance.OnClientPosChanged(_rigid.position);
        }
    }
}
