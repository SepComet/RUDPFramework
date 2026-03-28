using Network.Defines;
using UnityEngine;

public class Player : MonoBehaviour
{
    //[SerializeField] private float _moveSpeed = 10f;
    public string PlayerId { get; private set; } = "1001";
    public ClientAuthoritativePlayerStateSnapshot AuthoritativeState => _authoritativeState.Current;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private Material[] _materials;
    [SerializeField] private Camera _camera;
    [SerializeField] private MovementComponent _movement;
    [SerializeField] private PlayerUI _playerUI;
    [SerializeField] private bool _isControlled;
    private readonly ClientAuthoritativePlayerState _authoritativeState = new();

    public void LocalInit(string playerId, int speed, long serverTick)
    {
        this.PlayerId = playerId;
        this._isControlled = true;

        int idx = Random.Range(0, _materials.Length);
        _meshRenderer.material = _materials[idx];

        _playerUI.Init(this);
        _movement.Init(true, this, speed, serverTick);
    }

    public void RemoteInit(string playerId, UnityEngine.Vector3 pos)
    {
        this.PlayerId = playerId;
        this._isControlled = false;

        int idx = Random.Range(0, _materials.Length);
        _meshRenderer.material = _materials[idx];

        Destroy(_camera.gameObject);
        this.transform.position = pos;

        _playerUI.Init(this);
        _movement.Init(false, this);
    }

    private void OnApplicationQuit()
    {
        if (!string.IsNullOrEmpty(this.PlayerId) && _isControlled)
        {
            NetworkManager.Instance.SendLogoutRequest(this.PlayerId);
        }
    }

    public void SyncPosition(PlayerState movement)
    {
        if (!_authoritativeState.TryAccept(movement, out var snapshot))
        {
            return;
        }

        _playerUI?.SyncAuthoritativeState(snapshot);
        _movement?.OnAuthoritativeState(snapshot);
    }

    public void SyncTick(long serverTick)
    {
        _movement.SetServerTick(serverTick);
    }
}
