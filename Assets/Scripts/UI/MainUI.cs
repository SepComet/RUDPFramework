using System.Collections;
using System.Collections.Generic;
using Network.Defines;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vector3 = UnityEngine.Vector3;

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

    [Header("测试按钮")] [SerializeField] private Button _testOneFrameButton;
    [SerializeField] private Button _testFiveFramesButton;
    [SerializeField] private Button _testIntermittentButton;

    public UnityAction<Vector3> OnServerPosChanged;
    public UnityAction<Vector3> OnClientPosChanged;
    public UnityAction<long> OnServerTickChanged;
    public UnityAction<long> OnStartTickOffsetChanged;
    public UnityAction<long> OnClientTickChanged;
    public UnityAction<Vector3, Vector3, float, float> OnCorrectionMagnitudeChanged;
    public UnityAction<long> OnAcknowledgedMoveTickChanged;

    private const float MoveSpeed = 4f;
    private const float TurnSpeed = 180f;
    private const float DeltaTime = 0.05f; // 50ms 模拟步长

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

        _testOneFrameButton?.onClick.AddListener(OnTestOneFrameClicked);
        _testFiveFramesButton?.onClick.AddListener(OnTestFiveFramesClicked);
        _testIntermittentButton?.onClick.AddListener(OnTestIntermittentClicked);
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

        _testOneFrameButton?.onClick.RemoveListener(OnTestOneFrameClicked);
        _testFiveFramesButton?.onClick.RemoveListener(OnTestFiveFramesClicked);
        _testIntermittentButton?.onClick.RemoveListener(OnTestIntermittentClicked);
    }

    // ========== 测试按钮事件 ==========

    private void OnTestOneFrameClicked()
    {
        StartCoroutine(RunMovementTestCoroutine(
            playerId: MasterManager.Instance?.LocalPlayerId ?? "player",
            inputSequence: new[] { (0f, 1f) }, // 1帧：前进
            expectedTotalMovement: MoveSpeed * DeltaTime // 0.2
        ));
    }

    private void OnTestFiveFramesClicked()
    {
        StartCoroutine(RunMovementTestCoroutine(
            playerId: MasterManager.Instance?.LocalPlayerId ?? "player",
            inputSequence: new[]
            {
                (0f, 1f),
                (0f, 1f),
                (0f, 1f),
                (0f, 1f),
                (0f, 1f)
            }, // 5帧：连续前进
            expectedTotalMovement: MoveSpeed * DeltaTime * 5 // 1.0
        ));
    }

    private void OnTestIntermittentClicked()
    {
        StartCoroutine(RunMovementTestCoroutine(
            playerId: MasterManager.Instance?.LocalPlayerId ?? "player",
            inputSequence: new[]
            {
                (0f, 1f), // 帧1：前进
                (0f, 1f), // 帧2：前进
                (0f, 0f), // 帧3：停止
                (0f, 1f), // 帧4：前进
                (0f, 1f), // 帧5：前进
            },
            expectedTotalMovement: MoveSpeed * DeltaTime * 4 // 0.8（只有4帧在移动）
        ));
    }

    private IEnumerator RunMovementTestCoroutine(
        string playerId,
        (float turn, float throttle)[] inputSequence,
        float expectedTotalMovement)
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[Test] NetworkManager.Instance is null");
            yield break;
        }

        var player = MasterManager.Instance?.GetCurrentPlayer();
        if (player == null)
        {
            Debug.LogError("[Test] Current player is null, make sure you're logged in");
            yield break;
        }

        var movementComponent = player.GetComponent<MovementComponent>();
        if (movementComponent == null)
        {
            Debug.LogError("[Test] MovementComponent not found on player");
            yield break;
        }

        var inputComponent = player.GetComponent<InputComponent>();
        if (inputComponent == null)
        {
            Debug.LogError("[Test] InputComponent not found on player");
            yield break;
        }

        // 关闭服务器校正，只打印日志不应用位置
        //movementComponent.SetApplyServerCorrection(false);

        // 保存原始输入源并替换为模拟输入源
        var originalInputSource = inputComponent.GetInputSource();
        var simulatedInputSource = new SimulatedInputSource(inputSequence);
        inputComponent.SetInputSource(simulatedInputSource);
        //inputComponent.ResetTick(1); // 从 tick 1 开始

        // ========== 运行帧模拟 ==========
        // 每个输入需要两帧：一帧 Update() 发送 MoveInput，一帧 Advance() 推进
        for (int i = 0; i < inputSequence.Length; i++)
        {
            var (turnInput, throttleInput) = inputSequence[i];

            // yield return null 会触发一帧 Update()，SimulatedInputSource 提供当前输入
            yield return null;

            Debug.Log($"[Test Tick {i + 1}] Turn={turnInput}, Throttle={throttleInput}");

            // 发送后推进到下一个输入
            simulatedInputSource.Advance();
        }

        // 等待一段时间让服务器返回状态
        yield return new WaitForSeconds(0.5f);

        // 恢复原始输入源
        inputComponent.SetInputSource(originalInputSource);

        // 恢复服务器校正
        //movementComponent.SetApplyServerCorrection(true);

        Debug.Log($"========================================");
        Debug.Log($"[Test: {playerId}]");
        Debug.Log($"Expected Z Movement: {expectedTotalMovement:F4}");
        Debug.Log($"Client Position: {player.transform.position}");
        Debug.Log($"========================================");
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

    private void UpdateCorrectionText(Vector3 predictedPos, Vector3 authoritativePos, float positionError,
        float rotationError)
    {
        _correctionText.text = $"校正：pos差={positionError:F4} rot差={rotationError:F2}°";
    }

    private void UpdateAcknowledgedTickText(long tick)
    {
        _acknowledgedTickText.text = "AckTick：" + tick;
    }
}
