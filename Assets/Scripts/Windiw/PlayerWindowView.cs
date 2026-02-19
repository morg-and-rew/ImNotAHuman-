using UnityEngine;
using PixelCrushers.DialogueSystem;

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
            bool toggled = _currentWindowView.ToggleView();
            if (!toggled)
                return; // закрытие заблокировано — диалог ещё не дочитан

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
                if (!DialogueManager.isConversationActive)
                {
                    _currentWindowView.ExitView();
                    _playerController.SetBlock(false);
                    _isViewing = false;
                    _currentWindowView = null;
                }
                // иначе диалог ещё идёт — окно не закрываем, ссылку сохраняем
            }
            else
            {
                _currentWindowView = null;
            }
        }

        else if (closestWindow != null && closestWindow != _currentWindowView)
        {
            if (_isViewing && _currentWindowView != null)
            {
                if (!DialogueManager.isConversationActive)
                {
                    _currentWindowView.ExitView();
                    _playerController.SetBlock(false);
                    _isViewing = false;
                }
            }

            _currentWindowView = closestWindow;
        }
    }
}