using PixelCrushers;
using PixelCrushers.DialogueSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static IGameFlowController;

[DefaultExecutionOrder(-100)]
public sealed class GameFlowController : MonoBehaviour, IGameFlowController
{
    [Header("Refs")]
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
    [Header("Объекты, на которые нужно смотреть для телепорта по F")]
    [Tooltip("Дверь/объект на основной локации — телепорт на склад только при взгляде на него.")]
    [SerializeField] private Transform _warehouseEntranceDoor;
    [Tooltip("Дверь «к клиенту» на складе — телепорт к клиенту только при взгляде на неё.")]
    [SerializeField] private Transform _warehouseExitDoor;

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

    /// <summary> Игрок начал слушать Radio_Day1_2 — до окончания Player_Day1_2_Replica нельзя вернуться к клиенту. </summary>
    private bool _radioDay1_2ConversationStarted;

    /// <summary> Игрок пролистал Player_Day1_2_Replica — можно вернуться к клиенту; при телепорте запустить диалог. </summary>
    private bool _playerDay1_2ReplicaCompleted;

    /// <summary> Диалог, который запустить при следующем прибытии к клиенту (после телепорта по F из зоны выхода). </summary>
    private string _pendingDialogueOnArriveAtClient;

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

    private float _lastTeleportToClientTime = -999f;
    private readonly HashSet<string> _hintKeysShownOnce = new HashSet<string>();

    private List<TravelZone> _travelZones = new List<TravelZone>();

    /// <summary> Чтобы не спамить в консоль: логируем причину «нельзя вернуться» не чаще раза в 2 сек и только при смене причины. </summary>
    private string _lastLoggedReturnBlockReason;
    private float _lastLoggedReturnBlockTime = -999f;
    private const float ReturnBlockLogInterval = 2f;

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
        OnRadioStoryCompleted?.Invoke();
    }

    /// <summary> Вызвать, когда начался диалог Radio_Day1_2 — до окончания Player_Day1_2_Replica возврат к клиенту блокируется. </summary>
    public void NotifyRadioDay1_2Started()
    {
        _radioDay1_2ConversationStarted = true;
    }

    /// <summary> Вызвать, когда игрок пролистал Player_Day1_2_Replica — разрешить возврат; при телепорте к клиенту запустить postVideoConversation. </summary>
    public void NotifyPlayerDay1_2ReplicaCompleted(string postVideoConversation)
    {
        _playerDay1_2ReplicaCompleted = true;
        if (!string.IsNullOrEmpty(postVideoConversation))
            _pendingDialogueOnArriveAtClient = postVideoConversation;
    }

    /// <summary> true, если нельзя вернуться со склада к клиенту (начали Radio_Day1_2, но ещё не пролистали Player_Day1_2_Replica). </summary>
    private bool BlockReturnUntilPlayerDay1_2ReplicaDone => _radioDay1_2ConversationStarted && !_playerDay1_2ReplicaCompleted;

    public void NotifyTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        OnTriggerFired?.Invoke(triggerId);
    }

    public bool IsStoryExpectingTrigger(string triggerId)
    {
        return _storyDirector != null && _storyDirector.IsExpectingTrigger(triggerId);
    }

    /// <summary> True, если можно брать телефон (обучение дошло до шага go_to_phone или сюжет не идёт). </summary>
    public bool IsPhonePickupAllowed()
    {
        return _storyDirector == null || _storyDirector.IsAtOrPastStep("go_to_phone");
    }

    public void NotifyExitZonePassed(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return;
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
            EnterClientDialogueState(true, movePlayerToClient: false);
            _clientInteraction?.StartClientDialogWithSpecificStep("", postVideoConversation);
        }
        else
        {
            SetPlayerControlBlocked(true);
        }
    }

    /// <summary>
    /// Вызывается из RadioController при сценарии «телепорт к клиенту для видео» (например Day1_2). Можно использовать для счётчика или логики.
    /// </summary>
    public void TeleportToClientCounter()
    {
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
        _storyDirector.StartStory();
    }

    private void Update()
    {
        if (_input == null) return;

        if (_travelTarget != TravelTarget.None && _input.ConfirmPressed)
        {
            Debug.LogWarning($"[Телепорт] F нажата: цель={_travelTarget}, state={GameStateService.CurrentState}, freeTeleport={_freeTeleportTargetActive}");
            if (CanConfirmTravelToCurrentTarget())
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

    private bool CanConfirmTravelToCurrentTarget()
    {
        Debug.LogWarning($"[Телепорт] CanConfirm: цель={_travelTarget}, state={GameStateService.CurrentState}");
        if (_travelTarget == TravelTarget.Warehouse)
        {
            if (GameStateService.CurrentState == GameState.Warehouse)
            {
                Debug.LogWarning("[Телепорт] CanConfirm: отказ — уже на складе.");
                return false;
            }
            if (!IsPlayerLookingAt(_warehouseEntranceDoor))
            {
                Debug.LogWarning("[Телепорт] CanConfirm: отказ — не смотрите на дверь на склад.");
                return false;
            }
            Debug.LogWarning("[Телепорт] CanConfirm: ПРИНЯТО — телепорт на склад.");
            return true;
        }

        if (_travelTarget == TravelTarget.Client && GameStateService.CurrentState == GameState.Warehouse)
        {
            bool inZoneToClient = IsPlayerInZoneTo(TravelTarget.Client);
            bool nearExitDoor = _warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= 5f;
            Debug.LogWarning($"[Телепорт] CanConfirm Client: inZoneToClient={inZoneToClient}, nearExitDoor={nearExitDoor}, lookAtDoor={IsPlayerLookingAt(_warehouseExitDoor)}, storyAllows={_storyDirector == null || !_storyDirector.IsRunning || _storyDirector.IsStepAllowingTravelToClient}, requiredPkg={GameStateService.RequiredPackageNumber}, BlockReturn={BlockReturnUntilPlayerDay1_2ReplicaDone}");
            if (!inZoneToClient && !nearExitDoor)
            {
                Debug.LogWarning("[Возврат к клиенту] Нельзя: подойдите к двери «к клиенту» или в зону выхода (сейчас не в зоне и не у двери 5м).");
                return false;
            }
            if (!IsPlayerLookingAt(_warehouseExitDoor))
            {
                Debug.LogWarning("[Возврат к клиенту] Нельзя: смотрите на дверь «к клиенту».");
                return false;
            }
            if (DialogueManager.isConversationActive && string.Equals(DialogueManager.lastConversationStarted, "Radio_Day1_2", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[Возврат к клиенту] Нельзя: идёт диалог Radio_Day1_2.");
                return false;
            }
            if (BlockReturnUntilPlayerDay1_2ReplicaDone)
            {
                Debug.LogWarning("[Возврат к клиенту] Нельзя: ждётся реплика Player_Day1_2_Replica.");
                return false;
            }
            if (_storyDirector != null && _storyDirector.IsRunning && !_storyDirector.IsStepAllowingTravelToClient)
            {
                Debug.LogWarning("[Возврат к клиенту] Нельзя: сценарий не разрешает возврат (текущий шаг: " + (_storyDirector.CurrentStepId ?? "?") + ").");
                return false;
            }
            if (GameStateService.RequiredPackageNumber > 0 && !CanLeaveWarehouseToClient())
            {
                Debug.LogWarning("[Возврат к клиенту] Нельзя: нужна посылка в руках (требуется №" + GameStateService.RequiredPackageNumber + ").");
                return false;
            }
            Debug.LogWarning("[Телепорт] CanConfirm: ПРИНЯТО — возврат к клиенту.");
            return true;
        }

        if (_travelTarget == TravelTarget.Client && GameStateService.CurrentState != GameState.Warehouse)
        {
            Debug.LogWarning("[Возврат к клиенту] Нельзя: вы не на складе (цель «к клиенту» доступна только со склада).");
            return false;
        }

        return false;
    }

    private void ResolveTravelZones()
    {
        _travelZones.Clear();
        TravelZone[] all = FindObjectsByType<TravelZone>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.activeInHierarchy && all[i].enabled)
                _travelZones.Add(all[i]);
        }
        if (_travelZones.Count > 0)
            Debug.LogWarning($"[Телепорт] ResolveTravelZones: найдено зон={_travelZones.Count} (имена: {string.Join(", ", _travelZones.Select(z => z.gameObject.name))})");
    }

    private bool IsPlayerInZoneTo(TravelTarget target)
    {
        if (_travelZones.Count == 0)
            ResolveTravelZones();
        bool onWarehouse = GameStateService.CurrentState == GameState.Warehouse;
        if (target == TravelTarget.Client && !onWarehouse) return false;
        if (target == TravelTarget.Warehouse && onWarehouse) return false;
        return _travelZones.Any(z => z.Destination == target && z.PlayerInside);
    }

    public bool IsWaitingForWarehouseStoryZoneExit()
    {
        return _storyDirector != null && _storyDirector.IsWaitingForWarehouseStoryZoneExit;
    }

    /// <summary>
    /// Свободная телепортация (независимо от сюжета): в зоне перехода показываем подсказку и даём по F телепортироваться.
    /// Учитываем все TravelZone; на складе разрешаем цель «к клиенту» также по близости к двери (5 м), если триггер не сработал. м
    /// </summary>
    private void TickFreeTeleportZones()
    {
        if (_travelZones.Count == 0)
            ResolveTravelZones();
        if (_travelZones.Count == 0)
        {
            Debug.LogWarning("[Телепорт] TickZones: зон нет (_travelZones.Count=0), цель не выставляется.");
            return;
        }

        bool onWarehouse = GameStateService.CurrentState == GameState.Warehouse;
        bool inZoneToClient = IsPlayerInZoneTo(TravelTarget.Client);
        bool inZoneToWarehouse = IsPlayerInZoneTo(TravelTarget.Warehouse);

        if (_travelTarget == TravelTarget.None)
        {
            bool canSetWarehouseTarget = !onWarehouse
                && GameStateService.CurrentState != GameState.Phone
                && (Time.time - _lastTeleportToClientTime) >= 2f
                && (_storyDirector == null || !string.Equals(_storyDirector.CurrentStepId, "go_to_phone", StringComparison.OrdinalIgnoreCase))
                && (_storyDirector == null || _storyDirector.IsStepAllowingTravelToWarehouse);
            if (canSetWarehouseTarget && inZoneToWarehouse)
            {
                _freeTeleportTargetActive = true;
                _travelTarget = TravelTarget.Warehouse;
                Debug.LogWarning("[Телепорт] TickZones: выставлена цель Warehouse (в зоне «на склад»).");
            }
            else if (onWarehouse)
            {
                bool nearExitDoor = _warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= 5f;
                if (inZoneToClient || nearExitDoor)
                {
                    string blockReason = GetWhyCannotReturnToClient();
                    if (blockReason == null)
                    {
                        _freeTeleportTargetActive = true;
                        _travelTarget = TravelTarget.Client;
                        _lastLoggedReturnBlockReason = null;
                        Debug.LogWarning($"[Телепорт] TickZones: выставлена цель Client (onWarehouse=true, inZoneToClient={inZoneToClient}, nearExitDoor={nearExitDoor}).");
                        LogWhyCanReturnToClientIfNeeded();
                    }
                    else
                        LogWhyCannotReturnToClientIfNeeded(blockReason);
                }
                else
                    LogWhyNotInReturnAreaIfNeeded(inZoneToClient, nearExitDoor);
            }
            return;
        }

        if (!_freeTeleportTargetActive) return;

        if (_travelTarget == TravelTarget.Warehouse && !inZoneToWarehouse)
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            _tutorialHint?.Hide();
            Debug.LogWarning("[Телепорт] TickZones: цель сброшена (вышли из зоны «на склад»).");
        }
        else if (_travelTarget == TravelTarget.Client)
        {
            bool nearExitDoor = _warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= 5f;
            if (!inZoneToClient && !nearExitDoor)
            {
                _travelTarget = TravelTarget.None;
                _freeTeleportTargetActive = false;
                _tutorialHint?.Hide();
                Debug.LogWarning("[Телепорт] TickZones: цель Client сброшена (вышли из зоны/от двери).");
            }
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
                _player.ApplyDialogueCameraOffset();
            }
        }
        else
        {
            if (_player != null)
                _player.ClearDialogueCameraOffset();
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
        _controller?.SetBlock(isLocked);
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
                _player.ApplyDialogueCameraOffset();
            }
        }
        else if (!isLocked && _player != null)
        {
            _player.ClearDialogueCameraOffset();
        }

        Cursor.visible = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
    }

    /// <summary>
    /// Показать подсказку по посылке на складе (например после диалога Client_Day1.5.1).
    /// </summary>
    public void RefreshWarehouseDeliveryNote()
    {
        if (_delivery != null && GameStateService.CurrentState == GameState.Warehouse)
        {
            int num = _delivery.RequiredNumber;
            if (num > 0)
            {
                _delivery.ShowNoteForNumber(num);
                GameStateService.SetRequiredPackage(num, enforceOnly: false);
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
                ShowHintOnceByKey(t.pressSpaceKey);
                break;

            case TutorialStep.GoToRouter:
                ShowHintOnceByKey(t.routerHintKey);
                break;

            case TutorialStep.GoToPhone:
                ShowHintOnceByKey(t.phoneHintKey);
                break;

            case TutorialStep.None:
            default:
                _tutorialHint.Hide();
                break;
        }
    }

    public void ShowPhonePutHintOnce()
    {
        _preferEmptyOverMeetClient = false;
        ShowHintOnceByKey(GameConfig.Tutorial.phonePutKey);
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
        _player?.ClearDialogueCameraOffset();

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
        _meetClientHintShown = true;

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

    /// <summary> Подсказка «позвонить провайдеру» — только один раз при первом взятии телефона; при повторном взятии не показываем. </summary>
    public void ShowPhoneCallHint()
    {
        if (_tutorialHint == null) return;
        _preferEmptyOverMeetClient = false;
        ShowHintOnceByKey(GameConfig.Tutorial.phoneCallProviderKey);
    }

    public void ShowRadioHintOnce()
    {
        // Подсказку «радио» показываем один раз после опускания телефона в NotifyPhonePutDown, не при входе на шаг
    }

    public void NotifyPhonePutDown()
    {
        if (_tutorialHint == null) return;
        if (!_providerCallDone) return;
        _preferEmptyOverMeetClient = false;
        // Сразу после выполнения действия «положить телефон» показываем подсказку «радио» один раз
        ShowHintOnceByKey(GameConfig.Tutorial.radioUseKey);
    }

    /// <summary> Показать подсказку «Чтобы взять посылку, подойдите к ней и нажмите [E]» один раз за игру (обучение). Только если сюжет сейчас требует посылку. </summary>
    public void ShowWarehousePickHint()
    {
        if (_tutorialHint == null) return;
        if (!IsPackagePickAllowedByStory) return;
        _preferEmptyOverMeetClient = false;
        ShowHintOnceByKey(GameConfig.Tutorial.warehousePickKey);
    }

    public void ShowMeetClientHintOnce()
    {
        if (_tutorialHint == null)
            return;

        if (_preferEmptyOverMeetClient || _meetClientHintShown)
        {
            ShowHintOnceByKey(GameConfig.Tutorial.emptyKey);
            GameStateService.SetState(GameState.ClientDialog);
            return;
        }
        _preferEmptyOverMeetClient = false;
        _meetClientHintShown = true;
        ShowHintOnceByKey(GameConfig.Tutorial.meetClientKey);
        GameStateService.SetState(GameState.ClientDialog);
    }

    public void ShowHintRaw(string text)
    {
        if (_tutorialHint == null) return;
        if (string.IsNullOrEmpty(text))
        {
            _tutorialHint.Hide();
            return;
        }
        _preferEmptyOverMeetClient = false;
        _tutorialHint.Show(text);
        string preview = text.Length > 50 ? text.Substring(0, 50) + "..." : text;
    }

    /// <summary> Показать подсказку по ключу один раз за сессию; при повторном вызове с тем же ключом — пустая подсказка. </summary>
    public void ShowHintOnceByKey(string key)
    {
        if (_tutorialHint == null) return;
        if (string.IsNullOrEmpty(key))
        {
            _tutorialHint.Show(GetUIText(GameConfig.Tutorial.emptyKey));
            return;
        }
        if (_hintKeysShownOnce.Contains(key))
        {
            _tutorialHint.Show(GetUIText(GameConfig.Tutorial.emptyKey));
            return;
        }
        _hintKeysShownOnce.Add(key);
        _preferEmptyOverMeetClient = false;
        _tutorialHint.Show(GetUIText(key));
    }

    public void SetTravelTarget(TravelTarget target, string hintText, bool useFreeTeleportPointForClient = false)
    {
        _freeTeleportTargetActive = false;
        _useFreeTeleportPointForNextClientTravel = useFreeTeleportPointForClient && target == TravelTarget.Client;
        _travelTarget = target;
        if (string.IsNullOrEmpty(hintText))
        {
            _tutorialHint?.Hide();
        }
        else
        {
            if (target != TravelTarget.Client)
                _preferEmptyOverMeetClient = false;
            _tutorialHint?.Show(hintText);
            string preview = hintText.Length > 50 ? hintText.Substring(0, 50) + "..." : hintText;
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

    public bool AcceptAnyPackageForReturn => _acceptAnyPackageForReturn;

    /// <summary> True, если по сюжету сейчас нужно взять посылку и принести клиенту (тогда на складе разрешено брать посылку). </summary>
    public bool IsPackagePickAllowedByStory => GameStateService.RequiredPackageNumber > 0 || _acceptAnyPackageForReturn;

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
        _acceptAnyPackageForReturn = false;
        GameStateService.SetRequiredPackage(number, enforceOnly: false);
        GameStateService.SetPackageDropLocked(false);
        if (number > 0 && _delivery != null)
            _delivery.ShowNoteForNumber(number);
        else if (_delivery != null)
            _delivery.ClearTask();
    }

    /// <summary> Запустить случайную задачу доставки (из оставшихся, без сюжетных номеров вроде 5577) и выставить её как требуемую для возврата к клиенту. </summary>
    public void StartRandomDeliveryTaskAndSetRequiredForReturn()
    {
        if (_delivery == null) return;
        _delivery.StartNewDeliveryTask(enforceOnlyAfterWrong: false);
        SetRequiredPackageForReturn(_delivery.RequiredNumber);
    }

    public void ShowSkepticPhoneNote()
    {
        if (_skepticPhoneNoteObject != null)
            _skepticPhoneNoteObject.SetActive(true);
    }

    public void ActivateRadioEvent(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_radioPlayed.Contains(id)) return;
        if (_radioExpired.Contains(id)) return;

        _radioAvailable.Add(id);
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
    }

    public void ExpireAllRadioAvailable()
    {
        foreach (string id in _radioAvailable)
            _radioExpired.Add(id);
        _radioAvailable.Clear();
    }

    private bool PerformTravel(TravelTarget target, bool ignoreClientRequirements, bool freeTeleport = false)
    {
        if (target == TravelTarget.Warehouse)
        {
            RemovePackageFromHands();
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
            Debug.LogWarning($"[Телепорт] PerformTravel(Client): ignoreClientReq={ignoreClientRequirements}");
            if (!ignoreClientRequirements)
            {
                if (!CanLeaveWarehouseToClient())
                {
                    if (_pendingDialogueReturnPackage > 0 && CanLeaveWarehouseWithPendingPackage())
                        _pendingDialogueReturnPackage = 0;
                    else
                    {
                        Debug.LogWarning("[Телепорт] PerformTravel(Client): отказ — CanLeaveWarehouseToClient=false.");
                        return false;
                    }
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
            _lastTeleportToClientTime = Time.time;
            OnTeleportedToClient?.Invoke();
            _clientInteraction?.CloseUI();

            if (!string.IsNullOrEmpty(_pendingDialogueOnArriveAtClient))
            {
                string conv = _pendingDialogueOnArriveAtClient;
                _pendingDialogueOnArriveAtClient = null;
                _awaitingPostVideoDialogueComplete = conv;
                EnterClientDialogueState(true, movePlayerToClient: false);
                _clientInteraction?.StartClientDialogWithSpecificStep("", conv);
            }

            Debug.LogWarning("[Телепорт] PerformTravel(Client): выполнено успешно.");
            return true;
        }

        Debug.LogWarning("[Телепорт] PerformTravel: неизвестная цель или отказ.");
        return false;
    }

    /// <summary> Игрок смотрит в сторону объекта (направление камеры, горизонтальная плоскость). Если target не задан — считаем true. </summary>
    private bool IsPlayerLookingAt(Transform target)
    {
        if (target == null || _player == null || _player.PlayerCamera == null) return true;
        Vector3 toTarget = (target.position - _player.transform.position).normalized;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return true;
        toTarget.Normalize();
        Vector3 camForward = _player.PlayerCamera.transform.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.0001f) return false;
        camForward.Normalize();
        return Vector3.Dot(camForward, toTarget) >= 0.5f;
    }

    /// <summary> Причина, по которой нельзя вернуться к клиенту (цель не выставляется в TickFreeTeleportZones). null = можно. Вызывать только когда игрок на складе и (в зоне выхода или у двери). </summary>
    private string GetWhyCannotReturnToClient()
    {
        if (BlockReturnUntilPlayerDay1_2ReplicaDone)
            return "ждётся реплика Player_Day1_2_Replica (до конца диалога на радио)";
        if (_storyDirector != null && _storyDirector.IsRunning && !_storyDirector.IsStepAllowingTravelToClient)
            return "сценарий не разрешает возврат к клиенту (текущий шаг: " + (_storyDirector.CurrentStepId ?? "?") + ")";
        if (GameStateService.RequiredPackageNumber > 0 && !CanLeaveWarehouseToClient())
            return "нужна посылка в руках (требуется №" + GameStateService.RequiredPackageNumber + ")";
        return null;
    }

    private void LogWhyCannotReturnToClientIfNeeded(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return;
        if (reason == _lastLoggedReturnBlockReason && (Time.time - _lastLoggedReturnBlockTime) < ReturnBlockLogInterval)
            return;
        _lastLoggedReturnBlockReason = reason;
        _lastLoggedReturnBlockTime = Time.time;
        Debug.LogWarning("[Возврат к клиенту] Нельзя: " + reason);
    }

    private float _lastLoggedReturnAllowTime = -999f;
    private float _lastLoggedNotInReturnAreaTime = -999f;

    private void LogWhyNotInReturnAreaIfNeeded(bool inZoneToClient, bool nearExitDoor)
    {
        if ((Time.time - _lastLoggedNotInReturnAreaTime) < ReturnBlockLogInterval)
            return;
        _lastLoggedNotInReturnAreaTime = Time.time;
        float dist = _warehouseExitDoor != null && _player != null ? Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) : -1f;
        Debug.LogWarning($"[Телепорт] На складе, но цель Client не выставляется: не в зоне выхода (inZoneToClient={inZoneToClient}) и не у двери 5м (nearExitDoor={nearExitDoor}, dist={dist:F1}). Подойдите к двери «к клиенту» или в зону выхода.");
    }

    private void LogWhyCanReturnToClientIfNeeded()
    {
        if ((Time.time - _lastLoggedReturnAllowTime) < ReturnBlockLogInterval)
            return;
        _lastLoggedReturnAllowTime = Time.time;
        Debug.LogWarning("[Возврат к клиенту] Можно: вы в зоне выхода/у двери, условия выполнены — смотрите на дверь «к клиенту» и нажмите F.");
    }

    private bool CanLeaveWarehouseToClient()
    {
        bool inExitZone = IsPlayerInZoneTo(TravelTarget.Client) || (_warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= 5f);
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
            return false;
        bool exitInside = IsPlayerInZoneTo(TravelTarget.Client) || (_warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= 5f);
        if (!exitInside)
            return false;
        PlayerHands hands = HandsRegistry.Hands;
        if (hands == null || hands.Current is not PackageHoldable pkg)
            return false;
        packageNumber = pkg.Number;
        return packageNumber == _pendingDialogueReturnPackage;
    }
}