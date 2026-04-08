using UnityEngine;

public class MovementComponent : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigid;
    [SerializeField] private float _followMoveSpeed = 2f;
    [SerializeField] private float _followTurnSpeedDegreesPerSecond = 180f;
    [SerializeField] private float _correctionDecayMoveSpeed = 4f;
    [SerializeField] private float _correctionDecayTurnSpeedDegreesPerSecond = 360f;
    private const float RemoteInterpolationAlpha = 0.15f;
    private const float UnexpectedTurnLogCooldownSeconds = 0.25f;

    private bool _isControlled;
    private Vector3 _currentPosition;
    private Quaternion _currentRotation;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private Vector3 _correctionPositionOffset;
    private Quaternion _correctionRotationOffset = Quaternion.identity;
    private float _expectedTurnInput;
    private float _lastUnexpectedTurnLogTime = float.NegativeInfinity;

    private void Awake()
    {
        _rigid ??= GetComponent<Rigidbody>();
    }

    public void Init(bool isControlled, float followMoveSpeed = 2f, float followTurnSpeedDegreesPerSecond = 180f)
    {
        _rigid ??= GetComponent<Rigidbody>();
        _isControlled = isControlled;
        _followMoveSpeed = Mathf.Max(0f, followMoveSpeed);
        _followTurnSpeedDegreesPerSecond = Mathf.Max(0f, followTurnSpeedDegreesPerSecond);
        _correctionDecayMoveSpeed = Mathf.Max(_followMoveSpeed * 2f, _followMoveSpeed);
        _correctionDecayTurnSpeedDegreesPerSecond =
            Mathf.Max(_followTurnSpeedDegreesPerSecond * 2f, _followTurnSpeedDegreesPerSecond);
        _rigid.interpolation = isControlled ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        _rigid.isKinematic = !isControlled;
        _rigid.velocity = Vector3.zero;
        _rigid.angularVelocity = Vector3.zero;

        _currentPosition = _rigid.position;
        _currentRotation = _rigid.rotation;
        _targetPosition = _rigid.position;
        _targetRotation = _rigid.rotation;
        _correctionPositionOffset = Vector3.zero;
        _correctionRotationOffset = Quaternion.identity;
    }

    private void Update()
    {
        var beforeRotation = _currentRotation;

        if (_isControlled)
        {
            _correctionPositionOffset = Vector3.MoveTowards(
                _correctionPositionOffset,
                Vector3.zero,
                _correctionDecayMoveSpeed * Time.deltaTime);
            _correctionRotationOffset = Quaternion.RotateTowards(
                _correctionRotationOffset,
                Quaternion.identity,
                _correctionDecayTurnSpeedDegreesPerSecond * Time.deltaTime);

            var desiredPosition = _targetPosition + _correctionPositionOffset;
            var desiredRotation = _targetRotation * _correctionRotationOffset;

            _currentPosition = Vector3.MoveTowards(_currentPosition, desiredPosition, _followMoveSpeed * Time.deltaTime);
            _currentRotation = Quaternion.RotateTowards(
                _currentRotation,
                desiredRotation,
                _followTurnSpeedDegreesPerSecond * Time.deltaTime);
        }
        else
        {
            _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, RemoteInterpolationAlpha);
            _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, RemoteInterpolationAlpha);
        }

        _rigid.position = _currentPosition;
        _rigid.rotation = _currentRotation;

        LogUnexpectedTurnIfNeeded(beforeRotation, _currentRotation);

        if (_isControlled && MainUI.Instance != null)
        {
            MainUI.Instance.OnClientPosChanged(_currentPosition);
        }
    }

    public Vector3 CurrentPosition => _currentPosition;

    public Quaternion CurrentRotation => _currentRotation;

    public Vector3 TargetPosition => _targetPosition;

    public Quaternion TargetRotation => _targetRotation;

    public void SetTargetPose(Vector3 position, Quaternion rotation)
    {
        _targetPosition = position;
        _targetRotation = rotation;
    }

    public void SetExpectedTurnInput(float expectedTurnInput)
    {
        _expectedTurnInput = expectedTurnInput;
    }

    public void BlendToPoseFromCurrent(Vector3 position, Quaternion rotation)
    {
        _targetPosition = position;
        _targetRotation = rotation;
        _correctionPositionOffset = _currentPosition - position;
        _correctionRotationOffset = Quaternion.Inverse(rotation) * _currentRotation;
    }

    public void SnapToPose(Vector3 position, Quaternion rotation)
    {
        _currentPosition = position;
        _currentRotation = rotation;
        _targetPosition = position;
        _targetRotation = rotation;
        _correctionPositionOffset = Vector3.zero;
        _correctionRotationOffset = Quaternion.identity;
        _rigid.position = position;
        _rigid.rotation = rotation;
    }

    private void LogUnexpectedTurnIfNeeded(Quaternion beforeRotation, Quaternion afterRotation)
    {
        if (!_isControlled || Mathf.Abs(_expectedTurnInput) < 0.01f)
        {
            return;
        }

        var beforeError = Quaternion.Angle(beforeRotation, _targetRotation);
        var afterError = Quaternion.Angle(afterRotation, _targetRotation);
        if (beforeError < 0.1f && afterError < 0.1f)
        {
            return;
        }

        var deltaYaw = Mathf.DeltaAngle(beforeRotation.eulerAngles.y, afterRotation.eulerAngles.y);
        if (Mathf.Abs(deltaYaw) < 0.01f)
        {
            return;
        }
        
        if (afterError <= beforeError + 0.05f)
        {
            return;
        }
        
        if (Time.time - _lastUnexpectedTurnLogTime < UnexpectedTurnLogCooldownSeconds)
        {
            return;
        }

        _lastUnexpectedTurnLogTime = Time.time;
        Debug.LogWarning(
            $"[UnexpectedTurnAwayFromTarget] expectedTurn={_expectedTurnInput:F2} deltaYaw={deltaYaw:F2} " +
            $"beforeYaw={beforeRotation.eulerAngles.y:F2} afterYaw={afterRotation.eulerAngles.y:F2} " +
            $"beforeError={beforeError:F2} afterError={afterError:F2} " +
            $"current=({_currentPosition.x:F3},{_currentPosition.y:F3},{_currentPosition.z:F3}) " +
            $"target=({_targetPosition.x:F3},{_targetPosition.y:F3},{_targetPosition.z:F3}) " +
            $"correctionRot={_correctionRotationOffset.eulerAngles.y:F2}");
    }
}
