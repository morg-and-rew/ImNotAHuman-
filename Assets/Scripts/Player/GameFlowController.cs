using PixelCrushers;
using PixelCrushers.DialogueSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using static IGameFlowController;

[DefaultExecutionOrder(-100)]
public sealed class GameFlowController : MonoBehaviour, IGameFlowController
{
    [Header("Refs")]
    [SerializeField] private WarehouseExitTrigger _exitTrigger;
    [SerializeField] private Transform _warehousePoint;
    [SerializeField] private Transform _clientPoint;
    [SerializeField] private Transform _postVideoTablePoint;
    [SerializeField] private float _postVideoCameraPitchDown = -25f;

    [Header("Intro")]
    [SerializeField] private IntroView _introView;

    [Header("Localization (UI Text Table)")]
    [SerializeField] private TextTable _uiTextTable;
    [SerializeField] private string _language = "en";

    [Header("Tutorial")]
    [SerializeField] private TutorialHintView _tutorialHint;

    [Header("Delivery (optional)")]
    [SerializeField] private WarehouseDeliveryController _delivery;

    [SerializeField] private StoryDirector _storyDirector;
    [SerializeField] private Transform _dialogueLookPoint;
    [SerializeField] private GameObject _skepticPhoneNoteObject;
    [SerializeField] private DialogueSystemController _dialogueSystemController;

    private readonly HashSet<string> _radioAvailable = new();
    private readonly HashSet<string> _radioPlayed = new();
    private readonly HashSet<string> _radioExpired = new();
    private string _currentRadioEventId;

    private PlayerView _player;
    private IPlayerBlocker _controller;
    private IPlayerInput _input;

    private TutorialStep _tutorialStep = TutorialStep.None;
    private bool _initialized;

    private IClientInteraction _clientInteraction;
    private CustomDialogueUI _customDialogueUI;
    private string _awaitingPostVideoDialogueComplete;

    private bool _providerCallDone;

    public bool ProviderCallDone => _providerCallDone;
    public bool IsInClientDialogState => GameStateService.CurrentState == GameState.ClientDialog;

    public event Action<string> OnStoryProgressed;
    public event Action<string> OnClientEncountered;

    public event Action OnPlayerReturnedFromWarehouse;
    public event Action OnPlayerReturnedToClient;

    public static GameFlowController Instance;

    private TravelTarget _travelTarget = TravelTarget.None;
    private int _fixedPackageForNextWarehouse;

    public event Action OnTeleportedToWarehouse;
    public event Action OnTeleportedToClient;
    public event Action OnRadioStoryCompleted;
    public event Action<string> OnRadioEventActivated;
    public event Action<string> OnTriggerFired;
    public event Action<string> OnExitZonePassed;

    public TravelTarget CurrentTravelTarget => _travelTarget;
    public CustomDialogueUI CustomDialogueUI => _customDialogueUI;

    public void NotifyRadioStoryCompleted()
    {
        Debug.Log("[Flow] NotifyRadioStoryCompleted");
        OnRadioStoryCompleted?.Invoke();
    }

    public void NotifyTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        Debug.Log($"[Flow] NotifyTrigger: {triggerId}");
        OnTriggerFired?.Invoke(triggerId);
    }

    public void NotifyExitZonePassed(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return;
        Debug.Log($"[Flow] ExitZonePassed: {zoneId}");
        OnExitZonePassed?.Invoke(zoneId);
    }

    public void TeleportToTableAndFixPosition(string postVideoConversation = null)
    {
        if (_postVideoTablePoint == null)
        {
            Debug.LogWarning("[Flow] PostVideoTablePoint not assigned. Cannot teleport.");
            return;
        }
        Teleport(_postVideoTablePoint);
        ApplyPostVideoCameraPitch();
        if (!string.IsNullOrEmpty(postVideoConversation))
        {
            _awaitingPostVideoDialogueComplete = postVideoConversation;
            GameStateService.SetState(GameState.ClientDialog);
            EnterClientDialogueState(true);
            _clientInteraction?.StartClientDialogWithSpecificStep("", postVideoConversation);
        }
        else
        {
            SetPlayerControlBlocked(true);
        }
        Debug.Log("[Flow] Teleported to table, position fixed.");
    }

    private void ApplyPostVideoCameraPitch()
    {
        if (_player == null) return;
        _player.SetCameraPitch(_postVideoCameraPitchDown);
    }

    public string ResolveHintText(string hintText, string fallbackLocalizationKey)
    {
        if (!string.IsNullOrWhiteSpace(hintText)) return hintText;
        string fromTable = GetUIText(fallbackLocalizationKey ?? "");
        if (!string.IsNullOrWhiteSpace(fromTable)) return fromTable;
        return null;
    }

    private void OnEnable()
    {
        Instance = this;
        if (_customDialogueUI == null) _customDialogueUI = _dialogueSystemController?.DialogueUI as CustomDialogueUI;
        if (_customDialogueUI != null)
            _customDialogueUI.OnClientDialogueFinishedByKey += OnClientDialogueFinishedByKey;
    }

    private void OnDisable()
    {
        if (_customDialogueUI != null)
            _customDialogueUI.OnClientDialogueFinishedByKey -= OnClientDialogueFinishedByKey;

        if (_clientInteraction != null)
        {
            _clientInteraction.ClientDialogueFinished -= OnClientDialogueFinished;
            _clientInteraction.ClientDialogueStepCompleted -= OnClientDialogueStepCompleted;
        }

        UnsubscribeConversationEnded();
    }

    public void Init(PlayerView player, IPlayerBlocker controller, IPlayerInput input, IClientInteraction clientInteraction, DeliveryNoteView deliveryNoteView, CustomDialogueUI customDialogueUI = null)
    {
        if (_initialized) return;

        _initialized = true;

        _player = player;
        _controller = controller;
        _input = input;
        _clientInteraction = clientInteraction;
        _customDialogueUI = customDialogueUI ?? (_dialogueSystemController?.DialogueUI as CustomDialogueUI);

        if (_clientInteraction != null)
        {
            _clientInteraction.ClientDialogueFinished -= OnClientDialogueFinished;
            _clientInteraction.ClientDialogueFinished += OnClientDialogueFinished;
            _clientInteraction.ClientDialogueStepCompleted -= OnClientDialogueStepCompleted;
            _clientInteraction.ClientDialogueStepCompleted += OnClientDialogueStepCompleted;
        }

        _storyDirector.Initialize(this, _input, controller, deliveryNoteView);

        DialogueManager.SetLanguage(_language);

        EnterIntro();
    }

    private void EnterIntro()
    {
        GameStateService.SetState(GameState.Intro);
        _controller.SetBlock(true);

        IntroConfig intro = GameConfig.Intro;
        string quote = GetUIText(intro.quoteKey);

        if (_introView != null)
            _introView.Play(quote, intro.fadeDuration, ExitIntro);
        else
            ExitIntro();
    }

    private void ExitIntro()
    {
        _controller.SetBlock(false);
        GameStateService.SetState(GameState.Router);

        if (!string.IsNullOrWhiteSpace(GameConfig.Intro.monologueConversation))
            DialogueManager.StartConversation(GameConfig.Intro.monologueConversation);

        SetTutorialStep(TutorialStep.PressSpace);

        if (GameConfig.StoryAutoStart)
            StartCoroutine(StartStoryDelayed(GameConfig.StoryStartDelay));
    }

    private System.Collections.IEnumerator StartStoryDelayed(float seconds)
    {
        yield return WaitForSecondsCache.Get(seconds);
        Debug.Log("[Flow] Story start (config: startTrigger=auto).");
        _storyDirector.StartStory();
    }

    private void Update()
    {
        if (_input == null) return;

        if (_travelTarget != TravelTarget.None && _input.ConfirmPressed)
        {
            if (PerformTravel(_travelTarget, ignoreClientRequirements: false))
                return;
        }

        if (_storyDirector != null && _storyDirector.IsRunning)
        {
            _storyDirector.Tick();
            return;
        }

        if (GameStateService.CurrentState == GameState.ClientDialog && GameConfig.StoryStartOnClientInteract)
            HandleClientDialog();
    }

    private void HandleClientDialog()
    {
        if (_clientInteraction == null || _input == null) return;
        if (!GameConfig.StoryStartOnClientInteract) return;
        if (_clientInteraction.IsPlayerInside && !_clientInteraction.IsActive && _input.InteractPressed)
        {
            ExpireAllRadioAvailable();
            _storyDirector.StartStory();
        }
    }

    private void SetDialogueControlsLocked(bool isLocked)
    {
        _controller?.SetBlock(isLocked);
        HideHint();

        Cursor.visible = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void LockPlayerForDialogue(bool isLocked)
    {
        HideHint();

        bool clientDialogue = GameStateService.CurrentState == GameState.ClientDialog;

        if (!clientDialogue)
            return;

        _controller?.SetBlock(isLocked);

        if (isLocked)
        {
            Transform look = _dialogueLookPoint != null ? _dialogueLookPoint : _clientPoint;
            if (look != null && _player != null)
            {
                _player.transform.position = look.position;
                _player.transform.rotation = Quaternion.Euler(Vector3.zero);

                if (_player.PlayerCamera != null)
                    _player.PlayerCamera.transform.rotation = Quaternion.Euler(0, 17, 0);
            }
        }

        Cursor.visible = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void SetPlayerControlBlocked(bool isBlocked)
    {
        _controller?.SetBlock(isBlocked);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }


    public void EnterClientDialogueState(bool isLocked)
    {
        _controller?.SetBlock(isLocked);
        HideHint();

        if (isLocked)
        {
            Transform look = _dialogueLookPoint != null ? _dialogueLookPoint : _clientPoint;
            if (look != null && _player != null)
            {
                _player.transform.position = look.position;
                _player.transform.rotation = Quaternion.Euler(Vector3.zero);

                if (_player.PlayerCamera != null)
                    _player.PlayerCamera.transform.rotation = Quaternion.Euler(0, 17, 0);
            }
        }

        Cursor.visible = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
    }


    private void UnsubscribeConversationEnded()
    {
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnClientConversationEnded;
    }

    private void OnClientConversationEnded(Transform actor)
    {
        UnsubscribeConversationEnded();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _tutorialHint?.Show(GetUIText(GameConfig.Tutorial.goWarehouseKey));
    }

    private void OnClientDialogueFinishedByKey()
    {
        if (GameStateService.CurrentState == GameState.Phone)
            return;

        UnsubscribeConversationEnded();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        HideHint();

        ExpireAllRadioAvailable();

        Teleport(_warehousePoint);
        GameStateService.SetState(GameState.Warehouse);
    }

    private void Teleport(Transform point)
    {
        if (_player == null || point == null) return;

        CharacterController cc = _player.Controller;
        if (cc != null) cc.enabled = false;

        _player.transform.position = point.position;
        _player.transform.rotation = point.rotation;

        if (cc != null) cc.enabled = true;
    }

    private string GetUIText(string key)
    {
        if (_uiTextTable == null || string.IsNullOrWhiteSpace(key))
            return "";

        string lang = string.IsNullOrWhiteSpace(_language) ? "ru" : _language;

        string text = _uiTextTable.GetFieldTextForLanguage(key, lang);
        if (string.IsNullOrEmpty(text) && lang != "ru")
            text = _uiTextTable.GetFieldTextForLanguage(key, "ru");

        return text.Replace("\\r\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public void SetTutorialStep(TutorialStep step)
    {
        _tutorialStep = step;

        if (_tutorialHint == null) return;

        TutorialConfig t = GameConfig.Tutorial;
        switch (step)
        {
            case TutorialStep.PressSpace:
                _tutorialHint.Show(GetUIText(t.pressSpaceKey));
                break;

            case TutorialStep.GoToRouter:
                _tutorialHint.Show(GetUIText(t.routerHintKey));
                break;

            case TutorialStep.GoToPhone:
                _tutorialHint.Show(GetUIText(t.phoneHintKey));
                break;

            case TutorialStep.None:
            default:
                _tutorialHint.Hide();
                break;
        }
    }

    public void ShowPhonePutHintOnce()
    {
        _tutorialHint?.Show(GetUIText(GameConfig.Tutorial.phonePutKey));
    }

    public void ShowPhoneHint() => SetTutorialStep(TutorialStep.GoToPhone);
    public void HideHint() => SetTutorialStep(TutorialStep.None);

    public void MarkProviderCallDone() => _providerCallDone = true;

    public void HidePhoneHint()
    {
        _tutorialHint?.Hide();
    }

    private void OnClientDialogueStepCompleted(ClientDialogueStepCompletionData data)
    {
        if (string.IsNullOrEmpty(_awaitingPostVideoDialogueComplete)) return;
        if (!string.Equals(data.ConversationTitle, _awaitingPostVideoDialogueComplete, StringComparison.OrdinalIgnoreCase))
            return;

        _awaitingPostVideoDialogueComplete = null;
        GameStateService.SetState(GameState.None);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void OnClientDialogueFinished()
    {
        RemovePackageFromHands();

        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        GameStateService.SetWrongPackageDialogue(false);
    }

    public void RemovePackageFromHands()
    {
        PlayerHands hands = HandsRegistry.Hands;
        if (hands == null) return;

        if (hands.Current is not PackageHoldable)
            return;

        hands.DestroyCurrentItem();
    }

    public void ShowPhoneCallHint()
    {
        if (_tutorialHint == null) return;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.phoneCallProviderKey));
    }

    public void ShowRadioHintOnce()
    {
        if (_tutorialHint == null) return;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.radioUseKey));
    }

    public void ShowMeetClientHintOnce()
    {
        if (_tutorialHint == null)
            return;

        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.meetClientKey));
        GameStateService.SetState(GameState.ClientDialog);
    }

    public void ShowHintRaw(string text)
    {
        if (_tutorialHint == null) return;
        if (string.IsNullOrEmpty(text)) { _tutorialHint.Hide(); return; }
        _tutorialHint.Show(text);
    }

    public void SetTravelTarget(TravelTarget target, string hintText)
    {
        _travelTarget = target;
        if (string.IsNullOrEmpty(hintText)) _tutorialHint?.Hide();
        else _tutorialHint?.Show(hintText);
    }

    public void ForceTravel(TravelTarget target)
    {
        PerformTravel(target, ignoreClientRequirements: true);
    }

    public void SetFixedPackageForNextWarehouse(int number)
    {
        _fixedPackageForNextWarehouse = number;
    }

    public void SetRequiredPackageForReturn(int number)
    {
        GameStateService.SetRequiredPackage(number, enforceOnly: false);
        GameStateService.SetPackageDropLocked(false);
        if (number > 0 && _delivery != null)
            _delivery.ShowNoteForNumber(number);
        else if (_delivery != null)
            _delivery.ClearTask();
    }

    public void ShowSkepticPhoneNote()
    {
        if (_skepticPhoneNoteObject != null)
            _skepticPhoneNoteObject.SetActive(true);
    }

    public void ActivateRadioEvent(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_radioPlayed.Contains(id)) { Debug.Log($"[Flow] ActivateRadioEvent: {id} already played, skip."); return; }
        if (_radioExpired.Contains(id)) { Debug.Log($"[Flow] ActivateRadioEvent: {id} expired, skip."); return; }

        _radioAvailable.Add(id);
        Debug.Log($"[Flow] Activated radio event: {id}. Available: {string.Join(", ", _radioAvailable)}");
        OnRadioEventActivated?.Invoke(id);
    }

    public bool IsRadioEventAvailable(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        bool ok = _radioAvailable.Contains(id) && !_radioPlayed.Contains(id) && !_radioExpired.Contains(id);
        return ok;
    }

    public void ConsumeRadioEvent(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        _radioAvailable.Remove(id);
        _radioPlayed.Add(id);
        _currentRadioEventId = id;
        Debug.Log($"[Flow] Consumed radio event: {id}.");
    }

    public void ExpireAllRadioAvailable()
    {
        int count = _radioAvailable.Count;
        foreach (string id in _radioAvailable)
            _radioExpired.Add(id);
        _radioAvailable.Clear();
        Debug.Log($"[Flow] Expired {count} radio events.");
    }

    private bool PerformTravel(TravelTarget target, bool ignoreClientRequirements)
    {
        if (target == TravelTarget.Warehouse)
        {
            Teleport(_warehousePoint);
            GameStateService.SetState(GameState.Warehouse);

            if (_delivery != null && !_delivery.HasActiveTask)
            {
                _delivery.ClearTask();
                if (_fixedPackageForNextWarehouse > 0)
                {
                    _delivery.StartFixedDeliveryTask(_fixedPackageForNextWarehouse, enforceOnlyAfterWrong: false);
                    _fixedPackageForNextWarehouse = 0;
                }
                else
                {
                    _delivery.StartNewDeliveryTask(enforceOnlyAfterWrong: false);
                }
            }

            _clientInteraction?.CloseUI();

            _travelTarget = TravelTarget.None;
            _tutorialHint?.Hide();

            OnTeleportedToWarehouse?.Invoke();
            return true;
        }

        if (target == TravelTarget.Client)
        {
            if (!ignoreClientRequirements && !CanLeaveWarehouseToClient())
                return false;

            Teleport(_clientPoint);
            GameStateService.SetState(GameState.ClientDialog);

            _travelTarget = TravelTarget.None;
            _tutorialHint?.Hide();

            OnTeleportedToClient?.Invoke();
            return true;
        }

        return false;
    }

    private bool CanLeaveWarehouseToClient()
    {
        bool requiresPackageCheck = GameStateService.IsWarehouse || GameStateService.RequiredPackageNumber > 0;
        if (!requiresPackageCheck)
            return true;

        bool inExitZone = _exitTrigger == null || _exitTrigger.PlayerInside;
        if (!inExitZone)
            return false;

        PlayerHands hands = HandsRegistry.Hands;
        if (hands == null || hands.Current is not PackageHoldable package)
            return false;

        if (GameStateService.RequiredPackageNumber > 0 && package.Number != GameStateService.RequiredPackageNumber)
            return false;

        return true;
    }
}