using UnityEngine;

/// <summary>
/// 模拟输入源（测试用），提供预设的输入序列
/// </summary>
public class SimulatedInputSource : IInputSource
{
    private readonly (float turn, float throttle)[] _inputSequence;
    private int _index;
    private Vector3 _lastAimDirection = Vector3.forward;
    private bool _shootTriggered;

    public SimulatedInputSource((float turn, float throttle)[] sequence)
    {
        _inputSequence = sequence;
        _index = 0;
    }

    public Vector3 GetPlanarInput()
    {
        if (_index >= _inputSequence.Length)
        {
            return Vector3.zero;
        }

        var (turn, throttle) = _inputSequence[_index];
        return new Vector3(turn, 0f, throttle);
    }

    public bool ConsumeShootInput()
    {
        if (_shootTriggered)
        {
            _shootTriggered = false;
            return true;
        }

        return false;
    }

    public Vector3 GetAimDirection()
    {
        return _lastAimDirection;
    }

    /// <summary>
    /// 推进到下一个输入
    /// </summary>
    public void Advance()
    {
        if (_index < _inputSequence.Length)
        {
            _index++;
        }
    }

    /// <summary>
    /// 是否还有更多输入
    /// </summary>
    public bool HasMore => _index < _inputSequence.Length;

    /// <summary>
    /// 设置射击触发（下次 ConsumeShootInput 返回 true）
    /// </summary>
    public void SetShootTriggered()
    {
        _shootTriggered = true;
    }

    /// <summary>
    /// 设置瞄准方向
    /// </summary>
    public void SetAimDirection(Vector3 direction)
    {
        _lastAimDirection = direction;
    }

    /// <summary>
    /// 获取当前输入索引
    /// </summary>
    public int CurrentIndex => _index;
}
