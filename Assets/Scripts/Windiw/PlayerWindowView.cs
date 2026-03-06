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

        if (PlayerHintView.Instance != null)
        {
            bool showWindow = _currentWindowView != null && !_isViewing && _currentWindowView.IsPlayerLookingAtMe(_playerView);
            Sprite sprite = showWindow ? _currentWindowView.HintSprite : null;
            PlayerHintView.Instance.SetWindowHint(sprite);
        }

        if (_input != null && _currentWindowView != null && _input.InteractPressed)
        {
            if (_isViewing)
            {
                if (DialogueManager.isConversationActive)
                    return;
                _currentWindowView.ExitView(() => _playerController.SetBlock(false));
                _isViewing = false;
            }
            else
            {
                bool toggled = _currentWindowView.ToggleView();
                if (!toggled)
                    return;
                _isViewing = true;
                _playerController.SetBlock(true);
            }
        }
    }

    private void UpdateCurrentWindow()
    {
        Vector3 playerPosition = _playerView.transform.position;
        WindowView closestWindow = WindowViewManager.Instance?.GetClosestWindow(playerPosition);
        if (closestWindow != null && !_isViewing && !closestWindow.IsPlayerLookingAtMe(_playerView))
            closestWindow = null;

        if (_currentWindowView != null && !_currentWindowView.IsPlayerInZone && closestWindow == null)
        {
            if (_isViewing)
            {
                if (!DialogueManager.isConversationActive)
                {
                    WindowView w = _currentWindowView;
                    _currentWindowView = null;
                    _isViewing = false;
                    w.ExitView(() => _playerController.SetBlock(false));
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
                    WindowView w = _currentWindowView;
                    _isViewing = false;
                    w.ExitView(() => _playerController.SetBlock(false));
                }
            }

            _currentWindowView = closestWindow;
        }
    }
}