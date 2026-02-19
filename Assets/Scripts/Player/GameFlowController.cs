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
    [SerializeField] private TravelToWarehouseTrigger _travelToWarehouseTrigger;
    [SerializeField] private Transform _warehousePoint;
    [SerializeField] private Transform _clientPoint;
    [SerializeField] private Transform _postVideoTablePoint;
    [SerializeField] private float _postVideoCameraPitchDown = -25f;

    [Header("Intro")]
    [SerializeField] private IntroView _introView;

    [Header("Fade to black (end of day)")]
    [SerializeField] private FadeToBlackView _fadeToBlackView;

    [Header("Localization (UI Text Table)")]
    [SerializeField] private TextTable _uiTextTable;
    [SerializeField] private string _language = "en";

    [Header("Tutorial")]
    [SerializeField] private TutorialHintView _tutorialHint;

    [Header("Delivery (optional)")]
    [SerializeField] private WarehouseDeliveryController _delivery;

    [Header("Free teleport (independent of story — свои точки, без записки; подсказки только из таблицы, здесь не показываем)")]
    [SerializeField] private Transform _freeTeleportToWarehousePoint;
    [SerializeField] private Transform _freeTeleportToClientPoint;

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

    /// <summary> После взятия посылки показывать empty вместо meet_client до следующего шага. </summary>
    private bool _preferEmptyOverMeetClient;

    /// <summary> Подсказка meet_client уже показывалась один раз — дальше только empty. </summary>
    private bool _meetClientHintShown;

    public bool ProviderCallDone => _providerCallDone;
    public bool PreferEmptyOverMeetClient => _preferEmptyOverMeetClient;
    public bool MeetClientHintAlreadyShown => _meetClientHintShown;
    public bool IsInClientDialogState => GameStateService.CurrentState == GameState.ClientDialog;

    public event Action<string> OnStoryProgressed;
    public event Action<string> OnClientEncountered;

    public event Action OnPlayerReturnedFromWarehouse;
    public event Action OnPlayerReturnedToClient;

    public static GameFlowController Instance;

    private TravelTarget _travelTarget = TravelTarget.None;
    private int _fixedPackageForNextWarehouse;
    private int _pendingDialogueReturnPackage;
    private bool _acceptAnyPackageForReturn;

    /// <summary> True, когда текущий _travelTarget выставлен свободной телепортацией (не сюжетом). </summary>
    private bool _freeTeleportTargetActive;

    /// <summary> True, когда переход на склад — учебный (без записки и задачи доставки), только в сюжетные моменты запускаем доставку. </summary>
    private bool _tutorialWarehouseVisit;

    /// <summary> Следующий телепорт к клиенту (по F в зоне выхода) — в точку _freeTeleportToClientPoint (обучение return_from_warehouse). </summary>
    private bool _useFreeTeleportPointForNextClientTravel;

    public event Action OnTeleportedToWarehouse;
    public event Action OnTeleportedToClient;
    public event Action OnRadioStoryCompleted;
    public event Action<string> OnRadioEventActivated;
    public event Action<string> OnTriggerFired;
    public event Action<string> OnExitZonePassed;
    public event Action OnComputerVideoEnded;

    public void NotifyComputerVideoEnded()
    {
        OnComputerVideoEnded?.Invoke();
    }

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

    public bool IsStoryExpectingTrigger(string triggerId)
    {
        return _storyDirector != null && _storyDirector.IsExpectingTrigger(triggerId);
    }

    public void NotifyExitZonePassed(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return;
        // #region agent log
        AgentDebugLog.Log("GameFlowController.cs:NotifyExitZonePassed", "invoke", "{\"zoneId\":\"" + zoneId + "\"}", "H3");
        // #endregion
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

    /// <summary> Текст подсказки только из таблицы локализации (по ключу). hintText игнорируется — для переводов один источник. </summary>
    public string ResolveHintText(string hintText, string fallbackLocalizationKey)
    {
        return GetUIText(fallbackLocalizationKey ?? "");
    }

    public void PlayFadeToBlack(float durationSeconds, Action onComplete)
    {
        if (_fadeToBlackView != null)
            _fadeToBlackView.Play(durationSeconds, onComplete);
        else
            onComplete?.Invoke();
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
            _clientInteraction.ClientConversationStarted -= OnClientConversationStarted;
            _clientInteraction.ClientDialogueStepCompleted -= OnClientDialogueStepCompleted;
            _clientInteraction.RequestRemovePackageFromHands -= OnRequestRemovePackageFromHands;
        }

        UnsubscribeConversationEnded();
    }

    private void OnClientConversationStarted()
    {
        ShowEmptyHint();
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
            _clientInteraction.ClientConversationStarted -= OnClientConversationStarted;
            _clientInteraction.ClientConversationStarted += OnClientConversationStarted;
            _clientInteraction.ClientDialogueStepCompleted -= OnClientDialogueStepCompleted;
            _clientInteraction.ClientDialogueStepCompleted += OnClientDialogueStepCompleted;
            _clientInteraction.RequestRemovePackageFromHands -= OnRequestRemovePackageFromHands;
            _clientInteraction.RequestRemovePackageFromHands += OnRequestRemovePackageFromHands;
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
            // На складе F переносит к клиенту только в зоне двери (у выхода)
            if (_travelTarget == TravelTarget.Client && GameStateService.CurrentState == GameState.Warehouse && (_exitTrigger == null || !_exitTrigger.PlayerInside))
                ; // не телепортировать
            else
            {
                if (_travelTarget == TravelTarget.Client && _pendingDialogueReturnPackage > 0)
                {
                    if (TryPerformPendingReturnToClient())
                        return;
                }
                bool freeTeleport = _freeTeleportTargetActive;
                bool ignoreClientReq = freeTeleport && _travelTarget == TravelTarget.Client;
                if (PerformTravel(_travelTarget, ignoreClientRequirements: ignoreClientReq, freeTeleport: freeTeleport))
                {
                    _freeTeleportTargetActive = false;
                    return;
                }
            }
        }

        TickFreeTeleportZones();

        if (_storyDirector != null && _storyDirector.IsRunning)
        {
            _storyDirector.Tick();
            return;
        }

        if (GameStateService.CurrentState == GameState.ClientDialog && GameConfig.StoryStartOnClientInteract)
            HandleClientDialog();
    }

    /// <summary>
    /// Свободная телепортация (независимо от сюжета): в зоне перехода показываем подсказку и даём по F телепортироваться.
    /// </summary>
    private void TickFreeTeleportZones()
    {
        if (_travelToWarehouseTrigger == null && _exitTrigger == null) return;

        if (_travelTarget == TravelTarget.None)
        {
            if (GameStateService.CurrentState != GameState.Warehouse && _travelToWarehouseTrigger != null && _travelToWarehouseTrigger.PlayerInside)
            {
                _freeTeleportTargetActive = true;
                _travelTarget = TravelTarget.Warehouse;
            }
            else if (GameStateService.CurrentState == GameState.Warehouse && _exitTrigger != null && _exitTrigger.PlayerInside)
            {
                _freeTeleportTargetActive = true;
                _travelTarget = TravelTarget.Client;
            }
            return;
        }

        if (!_freeTeleportTargetActive) return;

        if (_travelTarget == TravelTarget.Warehouse && (_travelToWarehouseTrigger == null || !_travelToWarehouseTrigger.PlayerInside))
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            _tutorialHint?.Hide();
        }
        else if (_travelTarget == TravelTarget.Client && (_exitTrigger == null || !_exitTrigger.PlayerInside))
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            _tutorialHint?.Hide();
        }
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
        Debug.Log("[Tutorial] SetDialogueControlsLocked → обучение скрыто");
        HideHint();

        Cursor.visible = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void LockPlayerForDialogue(bool isLocked)
    {
        Debug.Log("[Tutorial] LockPlayerForDialogue → обучение скрыто");
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


    public void EnterClientDialogueState(bool isLocked, bool movePlayerToClient = true)
    {
        // #region agent log
        AgentDebugLog.Log("GameFlowController.cs:EnterClientDialogueState", "entry", "{\"isLocked\":" + isLocked.ToString().ToLowerInvariant() + ",\"movePlayerToClient\":" + movePlayerToClient.ToString().ToLowerInvariant() + ",\"controllerNotNull\":" + (_controller != null).ToString().ToLowerInvariant() + "}", "H_block");
        // #endregion
        _controller?.SetBlock(isLocked);
        Debug.Log("[Tutorial] EnterClientDialogueState → обучение скрыто");
        HideHint();

        if (isLocked && movePlayerToClient)
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

    /// <summary>
    /// Показать подсказку по посылке на складе (например после диалога Client_Day1.5.1).
    /// </summary>
    public void RefreshWarehouseDeliveryNote()
    {
        // #region agent log
        AgentDebugLog.Log("GameFlowController.cs:RefreshWarehouseDeliveryNote", "entry", "{\"deliveryNotNull\":" + (_delivery != null).ToString().ToLowerInvariant() + ",\"currentState\":\"" + GameStateService.CurrentState + "\",\"requiredNum\":" + (_delivery != null ? _delivery.RequiredNumber : -1) + "}", "H_state");
        // #endregion
        if (_delivery != null && GameStateService.CurrentState == GameState.Warehouse)
        {
            int num = _delivery.RequiredNumber;
            if (num > 0)
            {
                _delivery.ShowNoteForNumber(num);
                GameStateService.SetRequiredPackage(num, enforceOnly: false);
                // #region agent log
                AgentDebugLog.Log("GameFlowController.cs:RefreshWarehouseDeliveryNote", "note shown", "{\"num\":" + num + "}", "H_state");
                // #endregion
            }
        }
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

        Debug.Log("[Tutorial] OnClientDialogueFinishedByKey (F) → обучение скрыто, телепорт на склад");
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
        if (step != TutorialStep.None)
            _preferEmptyOverMeetClient = false;

        if (_tutorialHint == null) return;

        TutorialConfig t = GameConfig.Tutorial;
        switch (step)
        {
            case TutorialStep.PressSpace:
                _tutorialHint.Show(GetUIText(t.pressSpaceKey));
                Debug.Log("[Tutorial] SetTutorialStep: PressSpace → подсказка показана (tutorial.press_space)");
                break;

            case TutorialStep.GoToRouter:
                _tutorialHint.Show(GetUIText(t.routerHintKey));
                Debug.Log("[Tutorial] SetTutorialStep: GoToRouter → подсказка показана (tutorial.router_hint)");
                break;

            case TutorialStep.GoToPhone:
                _tutorialHint.Show(GetUIText(t.phoneHintKey));
                Debug.Log("[Tutorial] SetTutorialStep: GoToPhone → подсказка показана (tutorial.phone_hint)");
                break;

            case TutorialStep.None:
            default:
                _tutorialHint.Hide();
                Debug.Log("[Tutorial] SetTutorialStep: None → обучение скрыто");
                break;
        }
    }

    public void ShowPhonePutHintOnce()
    {
        _preferEmptyOverMeetClient = false;
        _tutorialHint?.Show(GetUIText(GameConfig.Tutorial.phonePutKey));
    }

    public void ShowPhoneHint() => SetTutorialStep(TutorialStep.GoToPhone);
    public void HideHint() => SetTutorialStep(TutorialStep.None);

    /// <summary> Показать подсказку по ключу tutorial.empty (оставьте пустым в таблице — поле будет пустым). </summary>
    public void ShowEmptyHint()
    {
        if (_tutorialHint == null) return;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.emptyKey));
    }

    /// <summary> Показать empty и не показывать meet_client до следующего шага (после взятия посылки). </summary>
    public void ShowEmptyHintAfterPackagePick()
    {
        if (_tutorialHint == null) return;
        _preferEmptyOverMeetClient = true;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.emptyKey));
    }

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

    private void OnRequestRemovePackageFromHands()
    {
        RemovePackageFromHands();
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
        _preferEmptyOverMeetClient = false;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.phoneCallProviderKey));
    }

    public void ShowRadioHintOnce()
    {
        if (_tutorialHint == null) return;
        _preferEmptyOverMeetClient = false;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.radioUseKey));
    }

    /// <summary> Показать подсказку «Чтобы взять посылку, подойдите к ней и нажмите [E]» (на складе после телепорта). </summary>
    public void ShowWarehousePickHint()
    {
        if (_tutorialHint == null) return;
        _preferEmptyOverMeetClient = false;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.warehousePickKey));
    }

    public void ShowMeetClientHintOnce()
    {
        if (_tutorialHint == null)
            return;

        if (_preferEmptyOverMeetClient || _meetClientHintShown)
        {
            _tutorialHint.Show(GetUIText(GameConfig.Tutorial.emptyKey));
            GameStateService.SetState(GameState.ClientDialog);
            return;
        }
        _preferEmptyOverMeetClient = false;
        _meetClientHintShown = true;
        _tutorialHint.Show(GetUIText(GameConfig.Tutorial.meetClientKey));
        GameStateService.SetState(GameState.ClientDialog);
    }

    public void ShowHintRaw(string text)
    {
        if (_tutorialHint == null) return;
        if (string.IsNullOrEmpty(text))
        {
            _tutorialHint.Hide();
            Debug.Log("[Tutorial] ShowHintRaw: пустой текст → обучение скрыто");
            return;
        }
        _preferEmptyOverMeetClient = false;
        _tutorialHint.Show(text);
        string preview = text.Length > 50 ? text.Substring(0, 50) + "..." : text;
        Debug.Log($"[Tutorial] ShowHintRaw: показана подсказка \"{preview.Replace("\n", " ")}\"");
    }

    public void SetTravelTarget(TravelTarget target, string hintText, bool useFreeTeleportPointForClient = false)
    {
        _freeTeleportTargetActive = false;
        _useFreeTeleportPointForNextClientTravel = useFreeTeleportPointForClient && target == TravelTarget.Client;
        _travelTarget = target;
        if (string.IsNullOrEmpty(hintText))
        {
            _tutorialHint?.Hide();
            Debug.Log($"[Tutorial] SetTravelTarget: {target}, hint пустой → обучение скрыто");
        }
        else
        {
            if (target != TravelTarget.Client)
                _preferEmptyOverMeetClient = false;
            _tutorialHint?.Show(hintText);
            string preview = hintText.Length > 50 ? hintText.Substring(0, 50) + "..." : hintText;
            Debug.Log($"[Tutorial] SetTravelTarget: {target}, показана подсказка \"{preview.Replace("\n", " ")}\"");
        }
    }

    public void SetTutorialWarehouseVisit(bool isTutorial)
    {
        _tutorialWarehouseVisit = isTutorial;
    }

    public void ForceTravel(TravelTarget target)
    {
        PerformTravel(target, ignoreClientRequirements: true);
    }

    public void SetAllowReturnToClientWithoutExitZone(bool allow) { }

    public void SetPendingDialogueReturnPackage(int packageNumber)
    {
        _pendingDialogueReturnPackage = packageNumber;
    }

    /// <summary>
    /// Для шагов вроде return_to_client_day1_5 (deliveryNoteNumber=0): разрешить выйти к клиенту с любой посылкой в руках.
    /// </summary>
    public void SetAcceptAnyPackageForReturn(bool acceptAny)
    {
        _acceptAnyPackageForReturn = acceptAny;
    }

    public bool TryPerformPendingReturnToClient()
    {
        if (_pendingDialogueReturnPackage <= 0) return false;
        if (!CanLeaveWarehouseWithPendingPackage()) return false;
        _pendingDialogueReturnPackage = 0;
        return PerformTravel(TravelTarget.Client, ignoreClientRequirements: true);
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

    private bool PerformTravel(TravelTarget target, bool ignoreClientRequirements, bool freeTeleport = false)
    {
        if (target == TravelTarget.Warehouse)
        {
            Transform point = freeTeleport && _freeTeleportToWarehousePoint != null ? _freeTeleportToWarehousePoint : _warehousePoint;
            Teleport(point);
            GameStateService.SetState(GameState.Warehouse);

            if (!freeTeleport && !_tutorialWarehouseVisit && _delivery != null && !_delivery.HasActiveTask)
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
            _tutorialWarehouseVisit = false;

            _travelTarget = TravelTarget.None;
            // Сначала даём сценарию показать подсказку для склада (например «уйди со склада» / «F — к клиенту»), потом закрываем UI клиента
            OnTeleportedToWarehouse?.Invoke();
            _clientInteraction?.CloseUI();
            // Подсказку не скрываем — сценарий уже вызвал SetTravelTarget и показал нужный текст
            return true;
        }

        if (target == TravelTarget.Client)
        {
            if (!ignoreClientRequirements)
            {
                if (!CanLeaveWarehouseToClient())
                {
                    if (_pendingDialogueReturnPackage > 0 && CanLeaveWarehouseWithPendingPackage())
                        _pendingDialogueReturnPackage = 0;
                    else
                        return false;
                }
            }

            _acceptAnyPackageForReturn = false;
            Transform point = _clientPoint;
            if (_useFreeTeleportPointForNextClientTravel && _freeTeleportToClientPoint != null)
            {
                point = _freeTeleportToClientPoint;
                _useFreeTeleportPointForNextClientTravel = false;
            }
            else if (freeTeleport && _freeTeleportToClientPoint != null)
                point = _freeTeleportToClientPoint;
            Teleport(point);
            GameStateService.SetState(GameState.ClientDialog);

            _travelTarget = TravelTarget.None;
            OnTeleportedToClient?.Invoke();
            _clientInteraction?.CloseUI();
            return true;
        }

        return false;
    }

    private bool CanLeaveWarehouseToClient()
    {
        bool inExitZone = _exitTrigger == null || _exitTrigger.PlayerInside;
        if (!inExitZone)
            return false;

        PlayerHands hands = HandsRegistry.Hands;

        // Шаг return_to_client_day1_5 (deliveryNoteNumber=0): достаточно любой посылки в руках
        if (_acceptAnyPackageForReturn)
            return hands != null && hands.Current is PackageHoldable;

        // Возврат к клиенту без посылки (например для диалога Client_Day1.5)
        if (GameStateService.RequiredPackageNumber <= 0)
            return true;

        if (hands == null || hands.Current is not PackageHoldable package)
            return false;

        if (package.Number != GameStateService.RequiredPackageNumber)
            return false;

        return true;
    }

    private bool CanLeaveWarehouseWithPendingPackage()
    {
        int packageNumber = 0;
        if (_pendingDialogueReturnPackage <= 0)
        {
            AgentDebugLog.Log("GameFlowController.cs:CanLeaveWarehouseWithPendingPackage", "fail", "{\"reason\":\"pendingDialogueReturnPackage<=0\",\"pending\":" + _pendingDialogueReturnPackage + "}", "H2");
            return false;
        }
        bool exitInside = _exitTrigger == null || _exitTrigger.PlayerInside;
        if (_exitTrigger != null && !_exitTrigger.PlayerInside)
            return false;
        PlayerHands hands = HandsRegistry.Hands;
        if (hands == null || hands.Current is not PackageHoldable pkg)
        {
            AgentDebugLog.Log("GameFlowController.cs:CanLeaveWarehouseWithPendingPackage", "fail", "{\"reason\":\"noPackageInHands\",\"handsNull\":" + (hands == null) + ",\"exitInside\":" + exitInside + "}", "H3");
            return false;
        }
        packageNumber = pkg.Number;
        bool ok = packageNumber == _pendingDialogueReturnPackage;
        // #region agent log
        AgentDebugLog.Log("GameFlowController.cs:CanLeaveWarehouseWithPendingPackage", ok ? "ok" : "fail", "{\"packageNumber\":" + packageNumber + ",\"pending\":" + _pendingDialogueReturnPackage + ",\"exitInside\":" + exitInside + ",\"result\":" + (ok ? "true" : "false") + "}", "H3");
        // #endregion
        return ok;
    }
}