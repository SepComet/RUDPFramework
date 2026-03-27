using System;
using Network.NetworkTransport;
using UnityEngine;

public sealed class NetworkDiagnosticsOverlay : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
    [SerializeField] private bool _visible = true;
    [SerializeField] private float _refreshIntervalSeconds = 1f;
    [SerializeField] private Vector2 _panelSize = new Vector2(560f, 420f);
    [SerializeField] private Vector2 _panelOffset = new Vector2(16f, 16f);

    private readonly GUIStyle _panelStyle = new GUIStyle();
    private readonly GUIStyle _headerStyle = new GUIStyle();
    private readonly GUIStyle _bodyStyle = new GUIStyle();
    private Vector2 _scrollPosition;
    private string _diagnosisText = "暂无诊断报告。";
    private string _reportPath = "未找到";
    private float _nextRefreshAt;

    private void Awake()
    {
        ConfigureStyles();
        RefreshDiagnosis();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            _visible = !_visible;
        }

        if (Time.unscaledTime >= _nextRefreshAt)
        {
            RefreshDiagnosis();
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        var area = new Rect(_panelOffset.x, _panelOffset.y, _panelSize.x, _panelSize.y);
        GUILayout.BeginArea(area, GUIContent.none, _panelStyle);
        GUILayout.Label("Network Diagnosis", _headerStyle);
        GUILayout.Label("网络诊断面板", _headerStyle);
        GUILayout.Space(6f);
        GUILayout.Label("Toggle: F3", _bodyStyle);
        GUILayout.Label("最新报告: " + _reportPath, _bodyStyle);
        GUILayout.Space(8f);
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        GUILayout.Label(_diagnosisText, _bodyStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void ConfigureStyles()
    {
        _panelStyle.normal.background = MakeBackground(new Color(0.08f, 0.11f, 0.15f, 0.92f));
        _panelStyle.padding = new RectOffset(14, 14, 12, 12);

        _headerStyle.fontSize = 16;
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.normal.textColor = new Color(0.92f, 0.96f, 0.98f);
        _headerStyle.wordWrap = false;

        _bodyStyle.fontSize = 13;
        _bodyStyle.normal.textColor = new Color(0.82f, 0.88f, 0.92f);
        _bodyStyle.wordWrap = true;
        _bodyStyle.richText = false;
    }

    private void RefreshDiagnosis()
    {
        _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.25f, _refreshIntervalSeconds);
        try
        {
            var path = TransportMetricsReportLocator.TryGetLatestDiagnosisPath();
            _reportPath = string.IsNullOrWhiteSpace(path) ? "未找到" : path;
            _diagnosisText = TransportMetricsReportLocator.ReadLatestDiagnosisText() ?? "暂无诊断报告。";
        }
        catch (Exception exception)
        {
            _reportPath = "读取失败";
            _diagnosisText = "读取诊断报告失败:\n" + exception.Message;
        }
    }

    private static Texture2D MakeBackground(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
