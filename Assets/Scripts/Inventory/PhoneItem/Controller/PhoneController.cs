using System;
using UnityEngine;

public sealed class PhoneController
{
    private readonly PhoneModel _model;
    private readonly PhoneItemView _itemView;
    private readonly PhoneUIView _uiView;
    private readonly IPhoneCallService _callService;
    private readonly IPlayerBlocker _blocker;
    private readonly IGameFlowController _flow;

    private readonly PlayerView _playerView;
    private readonly Transform _usePoint;
    private readonly Transform _lookTarget;
    private readonly bool _returnOnClose;
    private readonly GameSoundController _gameSoundController;

    private Vector3 _prevPos;
    private Quaternion _prevRot;
    private Quaternion _prevCameraRot;

    public bool IsOpen { get; private set; }

    private readonly Func<bool> _isConversationActive;

    public PhoneController(
        PhoneModel model,
        PhoneItemView itemView,
        PhoneUIView uiView,
        IPhoneCallService callService,
        Func<bool> isConversationActive,
        IPlayerBlocker blocker,
        IGameFlowController flow,
        PlayerView playerView,
        Transform usePoint,
        Transform lookTarget,
        bool returnOnClose,
        GameSoundController gameSoundController = null)
    {
        _model = model;
        _itemView = itemView;
        _uiView = uiView;
        _callService = callService;
        _flow = flow;
        _gameSoundController = gameSoundController;

        _isConversationActive = isConversationActive;
        _blocker = blocker;

        _playerView = playerView;
        _usePoint = usePoint;
        _lookTarget = lookTarget;
        _returnOnClose = returnOnClose;

        _itemView.Taken += OnTaken;
        _itemView.Dropped += OnDropped;

        _uiView.DigitPressed += OnDigit;
        _uiView.BackspacePressed += OnBackspace;
        _uiView.CallPressed += OnCall;
        _uiView.ClosePressed += OnClose;

        if (_itemView != null)
        {
            _itemView.CanDrop = () =>
            {
                // IsRinging остаётся true до закрытия UI телефона — иначе телефон нельзя положить после звонка.
                // Блокировка «до конца Hero_AfterProviderCall» даёт ConversationActive + BlockPhoneDropUntilProviderCallOnTutorial;
                // после MarkProviderCallDone — можно положить даже при активном диалоге.
                // После Hero_AfterProviderCall (MarkProviderCallDone) — убрать можно, даже если DialogueManager ещё isConversationActive.
                if (_flow != null && _flow.ProviderCallDone)
                    return true;
                if (_flow != null && _flow.BlockPhoneDropUntilProviderCallOnTutorial)
                    return false;
                if (ConversationActive()) return false;
                return true;
            };
        }
    }

    private bool ConversationActive()
    {
        return PixelCrushers.DialogueSystem.DialogueManager.isConversationActive;
    }

    public void Tick()
    {
        if (IsOpen)
            _blocker?.SetBlock(true);
    }


    private void OnTaken()
    {
        IsOpen = true;
        if (_flow != null && _flow.BlockPhoneDropUntilProviderCallOnTutorial)
            _flow.ShowPhoneCallHint();

        if (_playerView != null && _returnOnClose)
        {
            _prevPos = _playerView.transform.position;
            _prevRot = _playerView.transform.rotation;
            if (_playerView.PlayerCamera != null)
                _prevCameraRot = _playerView.PlayerCamera.transform.rotation;
        }

        if (_playerView != null && _usePoint != null)
        {
            _playerView.TeleportTo(_usePoint.position, _usePoint.rotation);
        }
        AlignPlayerToPhonePose();

        _model.Open();
        _uiView.SetNumber(_model.Number);
        _uiView.SetCallInteractable(_model.CanCall() && !ConversationActive());
        _uiView.Show();

        _blocker?.SetBlock(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnClose()
    {
        if (ConversationActive())
            return;

        CloseUI(restorePosition: true);
    }

    private void OnDropped()
    {
        if (ConversationActive())
            return;

        // Сохраняем направление камеры «с телефоном в руках», чтобы после закрытия UI его не перезаписало
        Quaternion cameraRotWhileHolding = _playerView != null && _playerView.PlayerCamera != null
            ? _playerView.PlayerCamera.transform.rotation
            : Quaternion.identity;

        bool providerCallWasDone = _flow != null && _flow.ProviderCallDone;
        CloseUI(restorePosition: false);

        if (_playerView != null && _playerView.PlayerCamera != null)
        {
            _playerView.PlayerCamera.transform.rotation = cameraRotWhileHolding;
            _playerView.SyncRotationFromCamera();
        }

        _flow?.NotifyPhonePutDown();
        if (providerCallWasDone)
            _flow?.NotifyTrigger("provider_call");
    }


    private void CloseUI(bool restorePosition = true)
    {
        if (!IsOpen) return;
        IsOpen = false;

        _gameSoundController?.StopPhoneSounds();
        _model.Close();

        if (_callService != null && _callService.IsRinging)
            _callService.StopRinging();

        _uiView.Hide();
        _blocker?.SetBlock(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_playerView != null && _returnOnClose && restorePosition)
        {
            // Сохраняем направление камеры «с телефоном в руках», чтобы после возврата позиции камера смотрела туда же
            Quaternion cameraRotWhileHolding = _playerView.PlayerCamera != null ? _playerView.PlayerCamera.transform.rotation : _prevCameraRot;
            _playerView.TeleportTo(_prevPos, _prevRot);
            if (_playerView.PlayerCamera != null)
                _playerView.PlayerCamera.transform.rotation = cameraRotWhileHolding;
        }
    }

    private void AlignPlayerToPhonePose()
    {
        if (_playerView == null || _playerView.PlayerCamera == null)
            return;

        Camera cam = _playerView.PlayerCamera;
        Quaternion targetLookRotation;

        if (_lookTarget != null)
        {
            Vector3 toTarget = _lookTarget.position - cam.transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;
            targetLookRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }
        else if (_usePoint != null)
        {
            targetLookRotation = _usePoint.rotation;
        }
        else
        {
            return;
        }

        Vector3 euler = targetLookRotation.eulerAngles;
        _playerView.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        cam.transform.rotation = targetLookRotation;
    }

    private void OnDigit(char d)
    {
        _model.AddDigit(d);
        _uiView.SetNumber(_model.Number);
        _uiView.SetCallInteractable(_model.CanCall() && !ConversationActive());
    }

    private void OnBackspace()
    {
        _model.Backspace();
        _uiView.SetNumber(_model.Number);
        _uiView.SetCallInteractable(_model.CanCall() && !ConversationActive());
    }

    private void OnCall()
    {
        if (!_model.CanCall())
            return;
        if (ConversationActive())
            return;

        bool ok = _callService.TryCall(_model.Number);
        if (!ok)
        {
            _gameSoundController?.PlayPhoneWrongNumber();
            _uiView.ShowInvalidNumber();
        }
    }
}
