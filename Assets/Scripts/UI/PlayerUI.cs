using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Text _text;
    private Player _master;
    private Camera _mainCamera;
    private bool _isVisible = true;

    public void Init(Player master)
    {
        _canvas = this.transform.GetComponent<Canvas>();
        _mainCamera = Camera.main;
        this._master = master;
        RefreshText();
    }

    public void SyncAuthoritativeState(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        RefreshText(snapshot);
    }

    private void FixedUpdate()
    {
        if (_isVisible)
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }
            if (_mainCamera != null)
            {
                _canvas.transform.LookAt(_mainCamera.transform);
            }
        }
    }

    private void OnBecameVisible()
    {
        _isVisible = true;
    }

    private void OnBecameInvisible()
    {
        _isVisible = false;
    }

    private void RefreshText(ClientAuthoritativePlayerStateSnapshot snapshot = null)
    {
        if (_text == null || _master == null)
        {
            return;
        }

        if (snapshot == null)
        {
            _text.text = _master.PlayerId;
            return;
        }

        _text.text = $"{_master.PlayerId}\nHP:{snapshot.Hp} Tick:{snapshot.Tick}";
    }
}
