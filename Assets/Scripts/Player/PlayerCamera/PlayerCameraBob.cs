using UnityEngine;

public sealed class PlayerCameraBob
{
    private IPlayerInput _input;
    private Computer _switchZone;
    private IPlayerBlocker _playerController;
    private bool _isCameraSwitched = false;

    public void Initialize(IPlayerInput input, Computer switchZone, IPlayerBlocker playerController)
    {
        _input = input;
        _switchZone = switchZone;
        _playerController = playerController;
    }

    public void Tick()
    {
        if (_input != null && _switchZone != null && _switchZone.IsPlayerInZone && _input.InteractPressed)
        {
            _switchZone.SwitchCamera();

            _isCameraSwitched = !_isCameraSwitched;

            if (_isCameraSwitched)
            {
                _playerController.SetBlock(true);
            }
            else
            {
                _playerController.SetBlock(false);
            }
        }
    }
}