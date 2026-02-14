using UnityEngine;

public sealed class PlayerWindowView
{
    private IPlayerInput _input;
    private IPlayerBlocker _playerController;
    private PlayerView _playerView;
    private WindowView _currentWindowView = null;
    private bool _isViewing = false;

    public PlayerWindowView(IPlayerInput input, IPlayerBlocker playerController, PlayerView playerView)
    {
        _input = input;
        _playerController = playerController;
        _playerView = playerView;
    }

    public void Tick()
    {
        UpdateCurrentWindow();

        if (_input != null && _currentWindowView != null && _input.InteractPressed)
        {
            _currentWindowView.ToggleView();

            _isViewing = !_isViewing;

            if (_isViewing)
                _playerController.SetBlock(true);
            else
                _playerController.SetBlock(false);
        }
    }

    private void UpdateCurrentWindow()
    {
        Vector3 playerPosition = _playerView.transform.position;
        WindowView closestWindow = WindowViewManager.Instance?.GetClosestWindow(playerPosition);

        if (_currentWindowView != null && !_currentWindowView.IsPlayerInZone && closestWindow == null)
        {
            if (_isViewing)
            {
                _currentWindowView.ExitView();
                _playerController.SetBlock(false);
                _isViewing = false;
            }

            _currentWindowView = null;
        }

        else if (closestWindow != null && closestWindow != _currentWindowView)
        {
            if (_isViewing && _currentWindowView != null)
            {
                _currentWindowView.ExitView();
                _playerController.SetBlock(false);
                _isViewing = false;
            }

            _currentWindowView = closestWindow;
        }
    }
}