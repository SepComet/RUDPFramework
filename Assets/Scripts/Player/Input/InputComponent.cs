using System;
using Network.Defines;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;


/// <summary>
/// 输入组件，负责从 IInputSource 获取输入、发送 MoveInput 到服务器、管理 tick
/// Update() 是唯一调用 SendMoveInput / SendShootInput 的地方
/// </summary>
public class InputComponent : MonoBehaviour
{
    [SerializeField] private string _playerId = "player";
    [SerializeField] private float _sendInterval = 0.05f; // 50ms 发送间隔

    private IInputSource _inputSource;
    private Vector3 _currentInput;
    private Vector3 _lastAimDirection = Vector3.forward;
    private float _lastSendTime;
    private bool _stopMessagePending;
    private bool _wasMovingLastFrame;
    private long _tick;

    public event Action<MoveInput> OnMoveInputCreated;
    public event Action<ShootInput> OnShootInputCreated;

    public long CurrentTick => _tick;

    /// <summary>
    /// 设置玩家 ID（由 MovementComponent.Init 调用）
    /// </summary>
    public void InjectPlayerId(string playerId)
    {
        _playerId = playerId;
    }

    private void Awake()
    {
        var camera = Camera.main;
        _inputSource = new UnityInputSource(camera?.transform);
    }

    /// <summary>
    /// 设置自定义输入源（用于替换默认的 UnityInputSource）
    /// </summary>
    public void SetInputSource(IInputSource source)
    {
        _inputSource = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// 获取当前输入源（用于测试）
    /// </summary>
    public IInputSource GetInputSource()
    {
        return _inputSource;
    }

    /// <summary>
    /// 重置 tick（用于测试）
    /// </summary>
    public void ResetTick(long tick = 0)
    {
        _tick = tick;
    }

    private void Update()
    {
        // 从输入源获取输入
        _currentInput = _inputSource?.GetPlanarInput() ?? Vector3.zero;

        // 检测移动状态变化
        bool hasMovement = ClientGameplayInputFlow.HasPlanarInput(_currentInput);
        if (hasMovement)
        {
            _stopMessagePending = false;
        }
        else if (_wasMovingLastFrame)
        {
            _stopMessagePending = true;
        }

        _wasMovingLastFrame = hasMovement;

        // 处理射击输入
        var shootInput = GetShootInput();
        if (shootInput != null && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SendShootInput(shootInput);
            OnShootInputCreated?.Invoke(shootInput);
        }

        // 定期发送移动输入
        if (Time.time - _lastSendTime > _sendInterval)
        {
            SendMoveInput();
        }
    }

    private void SendMoveInput()
    {
        if (!ClientGameplayInputFlow.TryCreateMoveInput(_playerId, _tick, _currentInput,
                _stopMessagePending, out var moveInput))
        {
            return;
        }

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SendMoveInput(moveInput);
        }

        OnMoveInputCreated?.Invoke(moveInput);
        _stopMessagePending = false;
        _lastSendTime = Time.time;
        _tick++;
    }

    private ShootInput GetShootInput()
    {
        var shootTriggered = _inputSource?.ConsumeShootInput() ?? false;
        var aimDirection = _inputSource?.GetAimDirection() ?? Vector3.forward;

        if (!shootTriggered)
        {
            return null;
        }

        var planarForward = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
        if (ClientGameplayInputFlow.HasPlanarInput(planarForward))
        {
            _lastAimDirection = planarForward;
        }

        return ClientGameplayInputFlow.CreateShootInput(_playerId, _tick, _lastAimDirection);
    }
}
