using UnityEngine;

/// <summary>
/// 真实的 Unity 输入源
/// </summary>
public class UnityInputSource : IInputSource
{
    private readonly Transform _cameraTransform;

    public UnityInputSource(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
    }

    public Vector3 GetPlanarInput()
    {
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
    }

    public bool ConsumeShootInput()
    {
        return Input.GetMouseButtonDown(0);
    }

    public Vector3 GetAimDirection()
    {
        if (_cameraTransform != null)
        {
            return _cameraTransform.forward;
        }

        return Vector3.forward;
    }
}
