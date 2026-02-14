using System;
using UnityEngine;

public sealed class PhoneController
{
    private readonly PhoneModel _model;
    private readonly PhoneItemView _itemView;
    private readonly PhoneUIView _uiView;
    private readonly IPhoneCallService _callService;
    private readonly IPlayerBlocker _blocker;

    private readonly PlayerView _playerView;
    private readonly Transform _usePoint;
    private readonly Transform _lookTarget;
    private readonly bool _returnOnClose;

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
        PlayerView playerView,
        Transform usePoint,
        Transform lookTarget,
        bool returnOnClose)
    {
        _model = model;
        _itemView = itemView;
        _uiView = uiView;
        _callService = callService;

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
                if (ConversationActive()) return false;
                if (_callService != null && _callService.IsRinging) return false;
                return true;
            };
        }

        _itemView.CanDrop = () =>
        {
            return !ConversationActive();
        };


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
        _uiView.SetCallInteractable(_model.CanCall());
        _uiView.Show();

        _blocker?.SetBlock(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnClose()
    {
        if (ConversationActive())
            return;

        CloseUI();
    }

    private void OnDropped()
    {
        if (ConversationActive())
            return;

        CloseUI();
    }


    private void CloseUI()
    {
        if (!IsOpen) return;
        IsOpen = false;

        _model.Close();

        if (_callService != null && _callService.IsRinging)
            _callService.StopRinging();

        _uiView.Hide();
        _blocker?.SetBlock(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_playerView != null && _returnOnClose)
        {
            _playerView.TeleportTo(_prevPos, _prevRot);
            if (_playerView.PlayerCamera != null)
                _playerView.PlayerCamera.transform.rotation = _prevCameraRot;
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
        _uiView.SetCallInteractable(_model.CanCall());
    }

    private void OnBackspace()
    {
        _model.Backspace();
        _uiView.SetNumber(_model.Number);
        _uiView.SetCallInteractable(_model.CanCall());
    }

    private void OnCall()
    {
        if (!_model.CanCall())
            return;

        bool ok = _callService.TryCall(_model.Number);
        if (!ok)
            _uiView.ShowInvalidNumber();
    }
}
