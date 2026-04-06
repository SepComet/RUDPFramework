using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public static MainUI Instance;

    [SerializeField] private Text _serverPositionText;
    [SerializeField] private Text _clientPositionText;
    [SerializeField] private Text _serverTickText;
    [SerializeField] private Text _startTickOffsetText;
    [SerializeField] private Text _clientTickText;
    [SerializeField] private Text _correctionText;
    [SerializeField] private Text _acknowledgedTickText;

    public UnityAction<Vector3> OnServerPosChanged;
    public UnityAction<Vector3> OnClientPosChanged;
    public UnityAction<long> OnServerTickChanged;
    public UnityAction<long> OnStartTickOffsetChanged;
    public UnityAction<long> OnClientTickChanged;
    public UnityAction<Vector3, Vector3, float, float> OnCorrectionMagnitudeChanged;
    public UnityAction<long> OnAcknowledgedMoveTickChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        OnServerPosChanged += UpdateServerPositionText;
        OnClientPosChanged += UpdateClientPositionText;
        OnServerTickChanged += UpdateServerTickText;
        OnClientTickChanged += UpdateClientTickText;
        OnStartTickOffsetChanged += UpdateStartTickOffsetText;
        OnCorrectionMagnitudeChanged += UpdateCorrectionText;
        OnAcknowledgedMoveTickChanged += UpdateAcknowledgedTickText;
    }

    private void OnDisable()
    {
        OnServerPosChanged -= UpdateServerPositionText;
        OnClientPosChanged -= UpdateClientPositionText;
        OnServerTickChanged -= UpdateServerTickText;
        OnClientTickChanged -= UpdateClientTickText;
        OnStartTickOffsetChanged -= UpdateStartTickOffsetText;
        OnCorrectionMagnitudeChanged -= UpdateCorrectionText;
        OnAcknowledgedMoveTickChanged -= UpdateAcknowledgedTickText;
    }

    private void UpdateServerPositionText(Vector3 pos)
    {
        _serverPositionText.text = "服务端位置：" + pos.ToString();
    }

    private void UpdateClientPositionText(Vector3 pos)
    {
        _clientPositionText.text = "客户端位置：" + pos.ToString();
    }

    private void UpdateServerTickText(long tick)
    {
        _serverTickText.text = "服务器Tick：" + tick;
    }

    private void UpdateStartTickOffsetText(long tick)
    {
        _startTickOffsetText.text = "初始Tick差：" + tick;
    }

    private void UpdateClientTickText(long tick)
    {
        _clientTickText.text = "客户端Tick：" + tick;
    }

    private void UpdateCorrectionText(Vector3 predictedPos, Vector3 authoritativePos, float positionError, float rotationError)
    {
        _correctionText.text = $"校正：pos差={positionError:F4} rot差={rotationError:F2}°";
    }

    private void UpdateAcknowledgedTickText(long tick)
    {
        _acknowledgedTickText.text = "AckTick：" + tick;
    }
}
