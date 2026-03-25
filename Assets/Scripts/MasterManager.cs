using System.Collections.Generic;
using Network.Defines;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class MasterManager : MonoBehaviour
{
    public static MasterManager Instance;
    private readonly Dictionary<string, Player> _players = new();
    public string LocalPlayerId { get; set; }

    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private Transform _playerParent;

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
    }

    public void InitPlayersState(LoginResponse response)
    {
        for (int i = 0; i < response.Positions.Count; i++)
        {
            string id = response.PlayerId[i];
            if (_players.ContainsKey(id)) continue;
            Vector3 pos = response.Positions[i].ToVector3();
            if (string.IsNullOrEmpty(LocalPlayerId) || LocalPlayerId != id)
            {
                RegisterRemotePlayer(id, pos);
            }
            else
            {
                RegisterLocalPlayer(response.Speed, response.ServerTick);
                var ui = GameObject.Find("RegisterCanvas");
                ui.SetActive(false);
            }
        }
    }

    private void RegisterLocalPlayer(int speed, long serverTick)
    {
        Player player = GameObject.Instantiate(_playerPrefab, _playerParent).GetComponent<Player>();
        player.LocalInit(LocalPlayerId, speed, serverTick);
        _players.Add(LocalPlayerId, player);
    }

    public void RegisterRemotePlayer(string playerId, Vector3 pos)
    {
        Player player = GameObject.Instantiate(_playerPrefab, _playerParent).GetComponent<Player>();
        player.RemoteInit(playerId, pos);
        _players.Add(playerId, player);
    }

    public bool UnregisterPlayer(string playerId)
    {
        if (_players.TryGetValue(playerId, out var player))
        {
            GameObject.Destroy(player.gameObject);
            _players.Remove(playerId);
            return true;
        }

        return false;
    }

    public void MovePlayer(string playerId, PlayerState movement)
    {
        if (_players.TryGetValue(playerId, out Player player))
        {
            player.SyncPosition(movement);
        }
        else Debug.LogWarning("Player not found");
    }

    public Player GetCurrentPlayer()
    {
        return _players[LocalPlayerId];
    }
}