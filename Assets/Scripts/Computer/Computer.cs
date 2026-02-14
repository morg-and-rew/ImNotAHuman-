using UnityEngine;

public sealed class Computer : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;
    private Camera _mainCamera;

    private bool _isPlayerInZone = false;
    private Camera _activeCamera;
    public bool IsPlayerInZone => _isPlayerInZone;

    public void Initialize(Camera mainCamera)
    {
        _mainCamera = mainCamera;
    }

    private void Start()
    {
        if (_mainCamera != null)
        {
            _mainCamera.enabled = true;
        }

        if (_targetCamera != null)
        {
            _targetCamera.enabled = false;
        }

        _activeCamera = _mainCamera;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerView playerView))
        {
            _isPlayerInZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerView playerView))
        {
            _isPlayerInZone = false;
            if (_activeCamera != _mainCamera)
            {
                SwitchToMainCamera();
            }
        }
    }

    public void SwitchCamera()
    {
        Camera newActiveCamera = _activeCamera == _mainCamera ? _targetCamera : _mainCamera;

        if (_activeCamera != null)
        {
            _activeCamera.enabled = false;
        }

        if (newActiveCamera != null)
        {
            newActiveCamera.enabled = true;
        }

        _activeCamera = newActiveCamera;
    }

    private void SwitchToMainCamera()
    {
        if (_activeCamera != _mainCamera)
        {
            if (_activeCamera != null)
            {
                _activeCamera.enabled = false;
            }

            if (_mainCamera != null)
            {
                _mainCamera.enabled = true;
            }

            _activeCamera = _mainCamera;
        }
    }
}