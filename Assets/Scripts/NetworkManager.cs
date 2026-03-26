using System.Collections;
using System.Net;
using Network.Defines;
using Network.NetworkApplication;
using Network.NetworkTransport;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;
    private ITransport _transport;
    private MessageManager _messageManager;
    private IPEndPoint _serverPoint;
    private uint _sequence = 0;
    [SerializeField] private GameObject _wrongWindow;

    private void Awake()
    {
        Instance = this;
        StartCoroutine(InitNetwork());
    }

    private IEnumerator InitNetwork()
    {
        _transport = new KcpTransport("127.0.0.1", 8080);

        var startTask = _transport.StartAsync();
        yield return new WaitUntil(() => startTask.IsCompleted);

        if (startTask.IsFaulted)
        {
            Debug.LogException(startTask.Exception);
            yield break;
        }

        _messageManager = new MessageManager(_transport);
        RegisterHandler();
        StartCoroutine(Heartbeat());
    }

    private void OnDestroy()
    {
        _transport?.Stop();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private IEnumerator Heartbeat()
    {
        while (true)
        {
            if (_serverPoint != null)
            {
                var heartbeat = new Heartbeat();
                _messageManager.SendMessage(heartbeat, MessageType.Heartbeat, _serverPoint);
            }

            yield return new WaitForSeconds(2.0f);
        }
    }

    private void RegisterHandler()
    {
        _messageManager.RegisterHandler(MessageType.LoginResponse, HandleLoginResponse);
        _messageManager.RegisterHandler(MessageType.PlayerState, HandlePlayerState);
        _messageManager.RegisterHandler(MessageType.HeartbeatResponse, HandleHeartbeatResponse);
        _messageManager.RegisterHandler(MessageType.LogoutRequest, HandleLogoutRequest);
        _messageManager.RegisterHandler(MessageType.PlayerJoin, HandlePlayerJoin);
    }

    private void HandleLoginResponse(byte[] data, IPEndPoint sender)
    {
        var response = LoginResponse.Parser.ParseFrom(data);
        _serverPoint = sender;
        if (response.Result)
        {
            MasterManager.Instance.InitPlayersState(response);
        }
        else
        {
            _wrongWindow.SetActive(true);
            Debug.LogError("UserId 已经存在");
        }
    }

    private void HandlePlayerState(byte[] data, IPEndPoint sender)
    {
        var message = PlayerState.Parser.ParseFrom(data);
        MasterManager.Instance.MovePlayer(message.PlayerId, message);
        Debug.Log($"收到PlayerState::PlayerID={message.PlayerId},Position=" + message.Position.ToVector3().ToString());
    }

    private void HandleHeartbeatResponse(byte[] data, IPEndPoint sender)
    {
        var response = HeartbeatResponse.Parser.ParseFrom(data);
        var player = MasterManager.Instance.GetCurrentPlayer();
        if (player != null)
        {
            player.SyncTick(response.ServerTick);
        }
    }

    private void HandleLogoutRequest(byte[] data, IPEndPoint sender)
    {
        var request = LogoutRequest.Parser.ParseFrom(data);
        MasterManager.Instance.UnregisterPlayer(request.PlayerId);
    }

    private void HandlePlayerJoin(byte[] data, IPEndPoint sender)
    {
        var playerJoin = PlayerJoin.Parser.ParseFrom(data);
        if (MasterManager.Instance.LocalPlayerId == playerJoin.PlayerId) return;
        MasterManager.Instance.RegisterRemotePlayer(playerJoin.PlayerId, playerJoin.Position.ToVector3());
    }

    public void SendPlayerInput(string playerId, Vector3 input)
    {
        var message = new PlayerInput()
        {
            PlayerId = playerId,
            Input = ProtoExtensions.ToProtoVector3(input)
        };
        _messageManager.SendMessage(message, MessageType.PlayerInput);
        Debug.Log($"PlayerMoveSeq: {_sequence++}");
    }

    public void SendPlayerInput(PlayerInput message)
    {
        _messageManager.SendMessage(message, MessageType.PlayerInput);
        Debug.Log($"PlayerMoveSeq: {_sequence++}");
    }

    public void SendLoginRequest(string playerId, int speed)
    {
        var request = new LoginRequest()
        {
            PlayerId = playerId,
            Speed = speed
        };
        _messageManager.SendMessage(request, MessageType.LoginRequest);
        Debug.Log($"Sent login request to player {playerId}");
    }

    public void SendLogoutRequest(string playerId)
    {
        var request = new LogoutRequest()
        {
            PlayerId = playerId
        };
        _messageManager.SendMessage(request, MessageType.LogoutRequest);
        Debug.Log($"Sent logout request to player {playerId}");
    }
}
