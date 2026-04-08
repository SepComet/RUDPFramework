using UnityEngine;

/// <summary>
/// 输入源接口，用于解耦输入捕获
/// </summary>
public interface IInputSource
{
    Vector3 GetPlanarInput();
    bool ConsumeShootInput();
    Vector3 GetAimDirection();
}
