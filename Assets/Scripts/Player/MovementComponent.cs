using UnityEngine;

public class MovementComponent : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigid;
    private const float InterpolationAlpha = 0.15f;

    private bool _isControlled;
    private Vector3 _currentPosition;
    private Quaternion _currentRotation;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;

    private void Awake()
    {
        _rigid ??= GetComponent<Rigidbody>();
    }

    public void Init(bool isControlled)
    {
        _rigid ??= GetComponent<Rigidbody>();
        _isControlled = isControlled;
        _rigid.interpolation = isControlled ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        _rigid.isKinematic = !isControlled;
        _rigid.velocity = Vector3.zero;
        _rigid.angularVelocity = Vector3.zero;

        _currentPosition = _rigid.position;
        _currentRotation = _rigid.rotation;
        _targetPosition = _rigid.position;
        _targetRotation = _rigid.rotation;
    }

    private void Update()
    {
        _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, InterpolationAlpha);
        _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, InterpolationAlpha);

        _rigid.position = _currentPosition;
        _rigid.rotation = _currentRotation;

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

    public void SnapToPose(Vector3 position, Quaternion rotation)
    {
        _currentPosition = position;
        _currentRotation = rotation;
        _targetPosition = position;
        _targetRotation = rotation;
        _rigid.position = position;
        _rigid.rotation = rotation;
    }
}
