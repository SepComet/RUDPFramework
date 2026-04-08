using Network.Defines;
using Network.NetworkApplication;
using UnityEngine;

public class Player : MonoBehaviour
{
    //[SerializeField] private float _moveSpeed = 10f;
    public string PlayerId { get; private set; } = "1001";
    public ClientAuthoritativePlayerStateSnapshot AuthoritativeState => _authoritativeState.Current;
    public ClientCombatPresentationSnapshot CombatPresentation => _authoritativeState.CombatPresentation;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private Material[] _materials;
    [SerializeField] private Camera _camera;
    [SerializeField] private MovementComponent _movement;
    [SerializeField] private MovementResolverComponent _movementResolver;
    [SerializeField] private PlayerUI _playerUI;
    [SerializeField] private bool _isControlled;
    private readonly ClientAuthoritativePlayerState _authoritativeState = new();

    private void Awake()
    {
        _movement ??= GetComponent<MovementComponent>();
        if (_movement == null)
        {
            _movement = gameObject.AddComponent<MovementComponent>();
        }

        _movementResolver ??= GetComponent<MovementResolverComponent>();
        if (_movementResolver == null)
        {
            _movementResolver = gameObject.AddComponent<MovementResolverComponent>();
        }
    }

    public void LocalInit(string playerId, ClientMovementBootstrap bootstrap)
    {
        this.PlayerId = playerId;
        this._isControlled = true;

        int idx = Random.Range(0, _materials.Length);
        _meshRenderer.material = _materials[idx];

        _playerUI.Init(this);
        _movementResolver.Init(true, this, bootstrap);
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
        _movementResolver.Init(false, this);
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

        _playerUI?.SyncAuthoritativeState(snapshot, CombatPresentation);
        _movementResolver?.OnAuthoritativeState(snapshot);
    }

    public bool ApplyCombatEvent(CombatEvent combatEvent)
    {
        if (!_authoritativeState.TryApplyCombatEvent(combatEvent, PlayerId, out var snapshot, out var combatSnapshot))
        {
            return false;
        }

        _playerUI?.SyncAuthoritativeState(snapshot, combatSnapshot);
        return true;
    }

    public void SyncTick(long serverTick)
    {
        _movementResolver.SetServerTick(serverTick);
    }
}
