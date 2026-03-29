using System.Collections;
using System.Net;
using System.Threading.Tasks;
using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class NetworkManager : MonoBehaviour
{
    private const int MaxNetworkMessagesPerFrame = 32;
    private const string DefaultServerIp = "127.0.0.1";
    private const int DefaultReliablePort = 8080;
    private const int DefaultSyncPort = 8081;

    [SerializeField] private GameObject _wrongWindow;
    [SerializeField] private bool _enableNetworkDiagnosticsOverlay = true;
    [SerializeField] private string _serverIp = DefaultServerIp;
    [SerializeField] private int _reliablePort = DefaultReliablePort;
    [SerializeField] private int _syncPort = DefaultSyncPort;
    
    public static NetworkManager Instance;
    private SharedNetworkRuntime _networkRuntime;
    private IPEndPoint _serverPoint;
    private uint _sequence = 0;
    private Task _networkDrainTask = Task.CompletedTask;

    private void Awake()
    {
        Instance = this;
        EnsureDiagnosticsOverlay();
        StartCoroutine(InitNetwork());
    }

    private IEnumerator InitNetwork()
    {
        var dispatcher = new MainThreadNetworkDispatcher();
        int? syncPort = _syncPort > 0 ? _syncPort : null;
        _networkRuntime = NetworkIntegrationFactory.CreateClientRuntime(
            _serverIp,
            _reliablePort,
            dispatcher,
            syncPort: syncPort);
        _networkRuntime.LifecycleChanged += HandleLifecycleChanged;

        var startTask = _networkRuntime.StartAsync();
        yield return new WaitUntil(() => startTask.IsCompleted);

        if (startTask.IsFaulted)
        {
            Debug.LogException(startTask.Exception);
            yield break;
        }

        RegisterHandler();
        StartCoroutine(Heartbeat());
    }

    private void Update()
    {
        if (_networkRuntime == null)
        {
            return;
        }

        _networkRuntime.UpdateLifecycle();

        if (!_networkDrainTask.IsCompleted)
        {
            return;
        }

        if (_networkDrainTask.IsFaulted)
        {
            Debug.LogException(_networkDrainTask.Exception);
        }

        _networkDrainTask = _networkRuntime.DrainPendingMessagesAsync(MaxNetworkMessagesPerFrame);
    }

    private void OnDestroy()
    {
        if (_networkRuntime != null)
        {
            _networkRuntime.LifecycleChanged -= HandleLifecycleChanged;
            _networkRuntime.Stop();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureDiagnosticsOverlay()
    {
        if (!_enableNetworkDiagnosticsOverlay)
        {
            return;
        }

        if (GetComponent<NetworkDiagnosticsOverlay>() == null)
        {
            gameObject.AddComponent<NetworkDiagnosticsOverlay>();
        }
    }

    private IEnumerator Heartbeat()
    {
        while (true)
        {
            if (_networkRuntime != null
                && _serverPoint != null
                && _networkRuntime.SessionManager.IsHeartbeatDue)
            {
                var heartbeat = new Heartbeat();
                _networkRuntime.MessageManager.SendMessage(heartbeat, MessageType.Heartbeat, _serverPoint);
                _networkRuntime.NotifyHeartbeatSent();
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private void RegisterHandler()
    {
        _networkRuntime.MessageManager.RegisterHandler(MessageType.LoginResponse, HandleLoginResponse);
        _networkRuntime.MessageManager.RegisterHandler(MessageType.PlayerState, HandlePlayerState);
        _networkRuntime.MessageManager.RegisterHandler(MessageType.CombatEvent, HandleCombatEvent);
        _networkRuntime.MessageManager.RegisterHandler(MessageType.HeartbeatResponse, HandleHeartbeatResponse);
        _networkRuntime.MessageManager.RegisterHandler(MessageType.LogoutRequest, HandleLogoutRequest);
        _networkRuntime.MessageManager.RegisterHandler(MessageType.PlayerJoin, HandlePlayerJoin);
    }

    private void HandleLoginResponse(byte[] data, IPEndPoint sender)
    {
        var response = LoginResponse.Parser.ParseFrom(data);
        _networkRuntime.NotifyInboundActivity();
        _networkRuntime.ClockSync.ObserveSample(response.ServerTick);
        _serverPoint = sender;
        if (response.Result)
        {
            _networkRuntime.NotifyLoginSucceeded();
            MasterManager.Instance.InitPlayersState(response);
        }
        else
        {
            _networkRuntime.NotifyLoginFailed("UserId already exists");
            _wrongWindow.SetActive(true);
            Debug.LogError("UserId 已经存在");
        }
    }

    private void HandlePlayerState(byte[] data, IPEndPoint sender)
    {
        _networkRuntime.NotifyInboundActivity();
        var message = PlayerState.Parser.ParseFrom(data);
        _networkRuntime.ObserveAuthoritativeState(message.Tick);
        MasterManager.Instance.MovePlayer(message.PlayerId, message);
        var player = MasterManager.Instance.GetCurrentPlayer();
        var currentServerTick = _networkRuntime.ClockSync.CurrentServerTick;
        if (player != null && currentServerTick.HasValue)
        {
            player.SyncTick(currentServerTick.Value);
        }

        Debug.Log($"收到PlayerState::PlayerID={message.PlayerId},Position=" + message.Position.ToVector3());
    }

    private void HandleHeartbeatResponse(byte[] data, IPEndPoint sender)
    {
        var response = HeartbeatResponse.Parser.ParseFrom(data);
        _networkRuntime.NotifyHeartbeatReceived(response.ServerTick);
        var player = MasterManager.Instance.GetCurrentPlayer();
        var currentServerTick = _networkRuntime.ClockSync.CurrentServerTick;
        if (player != null && currentServerTick.HasValue)
        {
            player.SyncTick(currentServerTick.Value);
        }
    }

    private void HandleCombatEvent(byte[] data, IPEndPoint sender)
    {
        _networkRuntime.NotifyInboundActivity();
        var combatEvent = CombatEvent.Parser.ParseFrom(data);
        MasterManager.Instance.ApplyCombatEvent(combatEvent);
        Debug.Log($"收到CombatEvent::Type={combatEvent.EventType},Attacker={combatEvent.AttackerId},Target={combatEvent.TargetId},Damage={combatEvent.Damage}");
    }

    private void HandleLogoutRequest(byte[] data, IPEndPoint sender)
    {
        _networkRuntime.NotifyInboundActivity();
        var request = LogoutRequest.Parser.ParseFrom(data);
        MasterManager.Instance.UnregisterPlayer(request.PlayerId);
    }

    private void HandlePlayerJoin(byte[] data, IPEndPoint sender)
    {
        _networkRuntime.NotifyInboundActivity();
        var playerJoin = PlayerJoin.Parser.ParseFrom(data);
        if (MasterManager.Instance.LocalPlayerId == playerJoin.PlayerId) return;
        MasterManager.Instance.RegisterRemotePlayer(playerJoin.PlayerId, playerJoin.Position.ToVector3());
    }

    private void HandleLifecycleChanged(SessionLifecycleEvent lifecycleEvent)
    {
        Debug.Log($"[NetworkManager] Session {lifecycleEvent.PreviousState} -> {lifecycleEvent.CurrentState} ({lifecycleEvent.Kind}) {lifecycleEvent.Reason}");
    }

    public void SendMoveInput(MoveInput message)
    {
        _networkRuntime.MessageManager.SendMessage(message, MessageType.MoveInput);
        Debug.Log($"PlayerMoveSeq: {_sequence++}");
    }

    public void SendShootInput(string playerId, Vector3 direction, long tick = 0, string targetId = "")
    {
        ClientGameplayInputFlow.SendShootInput(_networkRuntime.MessageManager, playerId, tick, direction, targetId);
        Debug.Log($"PlayerShootSeq: {_sequence++}");
    }

    public void SendShootInput(ShootInput message)
    {
        ClientGameplayInputFlow.SendShootInput(_networkRuntime.MessageManager, message);
        Debug.Log($"PlayerShootSeq: {_sequence++}");
    }

    public void SendLoginRequest(string playerId, int speed)
    {
        var request = new LoginRequest()
        {
            PlayerId = playerId,
            Speed = speed
        };
        _networkRuntime.NotifyLoginStarted();
        _networkRuntime.MessageManager.SendMessage(request, MessageType.LoginRequest);
        Debug.Log($"Sent login request to player {playerId}");
    }

    public void SendLogoutRequest(string playerId)
    {
        var request = new LogoutRequest()
        {
            PlayerId = playerId
        };
        _networkRuntime.MessageManager.SendMessage(request, MessageType.LogoutRequest);
        Debug.Log($"Sent logout request to player {playerId}");
    }
}
