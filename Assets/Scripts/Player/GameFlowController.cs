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
    [Tooltip("Точка спавна игрока в начале второго дня.")]
    [SerializeField] private Transform _playerSpawnPoint;

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

    [Header("Free teleport")]
    [SerializeField] private Transform _freeTeleportToWarehousePoint;
    [SerializeField] private Transform _freeTeleportToClientPoint;
    [Header("Doors for F teleport")]
    [SerializeField] private Transform _warehouseEntranceDoor;
    [SerializeField] private Transform _warehouseExitDoor;
    [SerializeField, Min(0.5f)] private float _doorTeleportMaxDistance = 2.5f;

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
    private TutorialPendingAction _tutorialPendingAction = TutorialPendingAction.None;
    private bool _initialized;

    private IClientInteraction _clientInteraction;
    private CustomDialogueUI _customDialogueUI;
    private string _awaitingPostVideoDialogueComplete;

    private bool _radioDay1_2ConversationStarted;
    private bool _playerDay1_2ReplicaCompleted;
    private string _pendingDialogueOnArriveAtClient;
    private bool _providerCallDone;
    private bool _preferEmptyOverMeetClient;
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
    /// <summary> Последняя зона, в которую телепортировались — чтобы не телепортировать повторно в ту же (глюк «остаёшься на месте»). </summary>
    private TravelTarget _lastTeleportDestination = TravelTarget.None;
    private int _fixedPackageForNextWarehouse;
    private int _pendingDialogueReturnPackage;
    private bool _acceptAnyPackageForReturn;

    private bool _freeTeleportTargetActive;
    /// <summary> True, если переход на склад подтверждается нажатием F из зоны клиента (без подхода к двери). Например после Client_Day1.4 ChoseToGivePackage5577. </summary>
    private bool _allowWarehouseConfirmFromClientArea;
    private bool _tutorialWarehouseVisit;
    private bool _useFreeTeleportPointForNextClientTravel;
    private float _lastTeleportToClientTime = -999f;
    /// <summary> Ключи туториалов, которые игрок уже выполнил — больше не показываем. </summary>
    private readonly HashSet<string> _hintKeysShownOnce = new HashSet<string>();
    /// <summary> Ключи туториалов, которые уже показаны, но игрок ещё не выполнил шаг — не спамим показом. </summary>
    private readonly HashSet<string> _hintKeysDisplayed = new HashSet<string>();
    private List<TravelZone> _travelZones = new List<TravelZone>();

    /// <summary> Флаг туториала: шаг с этим ключом уже выполнен игроком — принудительно не показываем снова. </summary>
    public bool IsTutorialStepAlreadyShown(string key) => !string.IsNullOrEmpty(key) && _hintKeysShownOnce.Contains(key);

    /// <summary> Пометить шаг туториала как выполненный (игрок совершил действие). После этого этот шаг больше не показывается. </summary>
    public void MarkTutorialStepCompleted(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        _hintKeysShownOnce.Add(key);
    }

    /// <summary> Пометить туториалы дня 1 как пройденные (для второго дня не показывать подсказки туториала). </summary>
    public void MarkDay1TutorialCompleted()
    {
        if (GameConfig.Tutorial == null) return;
        TutorialConfig t = GameConfig.Tutorial;
        MarkTutorialStepCompleted(t.doorWarehouseKey);
        MarkTutorialStepCompleted(t.returnPressFKey);
        MarkTutorialStepCompleted(t.returnToClientKey);
        MarkTutorialStepCompleted(t.goWarehouseKey);
        MarkTutorialStepCompleted(t.pressSpaceKey);
        MarkTutorialStepCompleted(t.routerHintKey);
        MarkTutorialStepCompleted(t.phoneHintKey);
        MarkTutorialStepCompleted(t.phoneCallProviderKey);
        MarkTutorialStepCompleted(t.phonePutKey);
        MarkTutorialStepCompleted(t.radioUseKey);
        MarkTutorialStepCompleted(t.radioBeforeClientKey);
        MarkTutorialStepCompleted(t.meetClientKey);
        MarkTutorialStepCompleted(t.warehousePickKey);
        MarkTutorialStepCompleted(t.warehouseReturnKey);
        MarkTutorialStepCompleted(t.windowLookKey);
        MarkTutorialStepCompleted(t.watchVideoKey);
        MarkTutorialStepCompleted(t.emptyKey);
        _meetClientHintShown = true;
        _tutorialHint?.Hide();
    }

    public event Action OnTeleportedToWarehouse;
    public event Action OnTeleportedToClient;
    public event Action OnRadioStoryCompleted;
    public event Action<string, float?> OnRadioEventActivated;
    public event Action<float> OnRadioStaticVolumeRequested;
    public event Action<string> OnTriggerFired;
    public event Action<string> OnExitZonePassed;
    public event Action OnComputerVideoEnded;

    public void NotifyComputerVideoEnded()
    {
        MarkTutorialStepCompleted(GameConfig.Tutorial.watchVideoKey);
        _tutorialHint?.Hide();
        OnComputerVideoEnded?.Invoke();
    }

    public TravelTarget CurrentTravelTarget => _travelTarget;
    public CustomDialogueUI CustomDialogueUI => _customDialogueUI;
    public Camera PlayerCamera => _player != null ? _player.PlayerCamera : null;
    public PlayerView Player => _player;

    public bool ShouldShowDoorHintFor(TravelTarget target)
    {
        return _travelTarget == target && IsPlayerInZoneTo(target);
    }

    public void NotifyRadioStoryCompleted()
    {
        OnRadioStoryCompleted?.Invoke();
    }

    public void NotifyRadioDay1_2Started()
    {
        _radioDay1_2ConversationStarted = true;
    }

    public void NotifyPlayerDay1_2ReplicaCompleted(string postVideoConversation)
    {
        _playerDay1_2ReplicaCompleted = true;
        if (!string.IsNullOrEmpty(postVideoConversation))
            _pendingDialogueOnArriveAtClient = postVideoConversation;
    }

    private bool BlockReturnUntilPlayerDay1_2ReplicaDone => _radioDay1_2ConversationStarted && !_playerDay1_2ReplicaCompleted;

    /// <summary> Текущий шаг — опциональное радио после Day1.2: игрок на складе может вернуться в зону выдачи в любой момент (не блокируем репликой Day1_2). </summary>
    private bool IsOptionalRadioVideoAfterDay12Step =>
        _storyDirector != null && string.Equals(_storyDirector.CurrentStepId, "optional_radio_video_after_day1_2", StringComparison.OrdinalIgnoreCase);

    public void NotifyTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        OnTriggerFired?.Invoke(triggerId);
    }

    public bool IsStoryExpectingTrigger(string triggerId)
    {
        return _storyDirector != null && _storyDirector.IsExpectingTrigger(triggerId);
    }

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
            return;
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

    public void TeleportToClientCounter()
    {
    }

    private void ApplyPostVideoCameraPitch()
    {
        if (_player == null) return;
        _player.SetCameraPitch(_postVideoCameraPitchDown);
    }

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

    /// <summary> Начало второго дня: телепорт в PlayerSpawnPoint, интро из чёрного в прозрачный (после показа интро скрываем fade-to-black). </summary>
    public void PlayDay2Intro(Action onComplete)
    {
        if (_playerSpawnPoint != null && _player != null)
        {
            Teleport(_playerSpawnPoint);
            _lastTeleportDestination = TravelTarget.None;
        }

        float duration = GameConfig.Intro != null ? GameConfig.Intro.fadeDuration : 3f;
        if (_introView != null)
        {
            _introView.PlayFadeFromBlack(duration, onComplete);
            _fadeToBlackView?.Hide();
        }
        else
        {
            _fadeToBlackView?.Hide();
            onComplete?.Invoke();
        }
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

        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationStarted -= OnDialogueSystemConversationStarted;

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
        // Туториал meet_client исчезает, когда игрок нажал E и начался диалог
        _tutorialHint?.Hide();
    }

    /// <summary> Во время разговора по радио игрок может свободно ходить — не блокируем управление. </summary>
    private void OnDialogueSystemConversationStarted(Transform _)
    {
        string title = DialogueManager.lastConversationStarted ?? "";
        if (string.IsNullOrEmpty(title)) return;
        bool isRadioConversation = title.StartsWith("Radio_", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Hero_Replic", StringComparison.OrdinalIgnoreCase);
        if (isRadioConversation)
            _controller?.SetBlock(false);
    }

    public void Init(PlayerView player, IPlayerBlocker controller, IPlayerInput input, IClientInteraction clientInteraction, DeliveryNoteView deliveryNoteView, CustomDialogueUI customDialogueUI = null)
    {
        if (_initialized) return;

        _initialized = true;

        // Интро только в 1-й день при старте с нуля; при загрузке сохранения не показываем
        if (_introView != null)
            _introView.gameObject.SetActive(false);

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

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnDialogueSystemConversationStarted;
            DialogueManager.instance.conversationStarted += OnDialogueSystemConversationStarted;
        }

        _storyDirector.Initialize(this, _input, controller, deliveryNoteView);

        DialogueManager.SetLanguage(_language);

        if (GameSaveSystem.LoadFromSaveAtStart && GameSaveSystem.LoadDay1() != null)
        {
            StartCoroutine(StartFromSavedGameDelayed());
            return;
        }

        EnterIntro();
    }

    private System.Collections.IEnumerator StartFromSavedGameDelayed()
    {
        if (_introView != null)
            _introView.gameObject.SetActive(false);
        yield return null;
        _controller.SetBlock(false);
        GameStateService.SetState(GameState.None);
        Day1SaveData loaded = GameSaveSystem.LoadDay1();
        if (loaded != null)
        {
            _storyDirector.ApplyDay1Save(loaded);
            MarkDay1TutorialCompleted();
            _storyDirector.StartStoryFromStepId("fade_to_black_day1_end");
        }
        else
        {
            _storyDirector.StartStory();
        }
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

        if (GameSaveSystem.LoadFromSaveAtStart)
        {
            Day1SaveData loaded = GameSaveSystem.LoadDay1();
            if (loaded != null)
            {
                _storyDirector.ApplyDay1Save(loaded);
                MarkDay1TutorialCompleted();
                _storyDirector.StartStoryFromStepId("fade_to_black_day1_end");
                yield break;
            }
        }

        _storyDirector.StartStory();
    }

    private void Update()
    {
        if (_input == null) return;

        if (_travelTarget != TravelTarget.None && _input.ConfirmPressed)
        {
            // Не телепортировать в ту же зону: на складе — только в зону выдачи, в зоне выдачи — только на склад
            bool onWarehouseNow = GameStateService.CurrentState == GameState.Warehouse;
            if (onWarehouseNow && _travelTarget == TravelTarget.Warehouse)
                _travelTarget = TravelTarget.None;
            else if (!onWarehouseNow && _travelTarget == TravelTarget.Client)
                _travelTarget = TravelTarget.None;

            if (_travelTarget != TravelTarget.None && CanConfirmTravelToCurrentTarget())
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
                    string doorKey = _travelTarget == TravelTarget.Warehouse ? GameConfig.Tutorial.doorWarehouseKey : GameConfig.Tutorial.returnPressFKey;
                    MarkTutorialStepCompleted(doorKey);
                    if (_travelTarget == TravelTarget.Client)
                        MarkTutorialStepCompleted(GameConfig.Tutorial.returnToClientKey);
                    _freeTeleportTargetActive = false;
                    _allowWarehouseConfirmFromClientArea = false;
                    return;
                }
            }
        }

        TickFreeTeleportZones();
        ApplyDoorHintFromZones();

        if (_freeTeleportTargetActive && _travelTarget != TravelTarget.None && _tutorialHint != null && CanConfirmTravelToCurrentTarget()
            && _tutorialPendingAction == TutorialPendingAction.None)
        {
            string key = _travelTarget == TravelTarget.Warehouse
                ? GameConfig.Tutorial.doorWarehouseKey
                : GameConfig.Tutorial.returnPressFKey;
            if (!IsTutorialStepAlreadyShown(key) && !_hintKeysDisplayed.Contains(key))
            {
                _hintKeysDisplayed.Add(key);
                _tutorialHint.Show(key);
            }
        }

        if (_storyDirector != null && _storyDirector.IsRunning)
        {
            _storyDirector.Tick();
            return;
        }

        // День 2: в шаге day2_start разрешаем взаимодействие с клиентом (Client_day2.1); вызываем HandleClientDialog даже при startTrigger=auto
        bool isDay2StartStep = _storyDirector != null && string.Equals(_storyDirector.CurrentStepId, "day2_start", StringComparison.OrdinalIgnoreCase);
        if (isDay2StartStep && _clientInteraction != null && _clientInteraction.IsPlayerInside && _clientInteraction.IsPlayerLookingAtClient(_player))
            GameStateService.SetState(GameState.ClientDialog);

        if ((GameStateService.CurrentState == GameState.ClientDialog && GameConfig.StoryStartOnClientInteract) || isDay2StartStep)
            HandleClientDialog();
    }

    private static bool IsRadioTutorialPlaying()
    {
        return DialogueManager.isConversationActive
            && string.Equals(DialogueManager.lastConversationStarted, "Radio_Tutorial", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary> True, если сюжет в процессе и ждёт действия игрока (например, «иди к радио», «возьми телефон»). </summary>
    private bool HasActiveTutorial()
    {
        return _storyDirector != null && _storyDirector.IsRunning;
    }

    private bool CanConfirmTravelToCurrentTarget()
    {
        if (_travelTarget == TravelTarget.Warehouse)
        {
            if (IsRadioTutorialPlaying())
                return false;
            if (GameStateService.CurrentState == GameState.Warehouse)
                return false;
            // Подтверждение из зоны клиента (напр. после Client_Day1.4 «отдать посылку 5577») — F без подхода к двери.
            if (_allowWarehouseConfirmFromClientArea)
                return true;
            // Отдельная проверка: после Client_Day1.4 с ChoseToGivePackage5577 = true — F из зоны клиента без подхода к двери.
            if (_storyDirector != null && _storyDirector.IsWaitingForWarehouseConfirm && DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool)
                return true;
            if (!IsPlayerLookingAt(_warehouseEntranceDoor))
                return false;
            if (_warehouseEntranceDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseEntranceDoor.position) > _doorTeleportMaxDistance)
                return false;
            return true;
        }

        if (_travelTarget == TravelTarget.Client && GameStateService.CurrentState == GameState.Warehouse)
        {
            if (IsRadioTutorialPlaying())
                return false;
            bool inZoneToClient = IsPlayerInZoneTo(TravelTarget.Client);
            bool nearExitDoor = _warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance;
            if (!inZoneToClient && !nearExitDoor)
                return false;
            if (!nearExitDoor)
                return false;
            if (!IsPlayerLookingAt(_warehouseExitDoor))
                return false;
            if (!IsOptionalRadioVideoAfterDay12Step && DialogueManager.isConversationActive && string.Equals(DialogueManager.lastConversationStarted, "Radio_Day1_2", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!IsOptionalRadioVideoAfterDay12Step && BlockReturnUntilPlayerDay1_2ReplicaDone)
                return false;
            if (_storyDirector != null && _storyDirector.IsRunning && !_storyDirector.IsStepAllowingTravelToClient)
                return false;
            if (GameStateService.RequiredPackageNumber > 0 && !CanLeaveWarehouseToClient())
                return false;
            return true;
        }

        if (_travelTarget == TravelTarget.Client && GameStateService.CurrentState != GameState.Warehouse)
            return false;

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

    private void TickFreeTeleportZones()
    {
        if (_travelZones.Count == 0)
            ResolveTravelZones();
        if (_travelZones.Count == 0)
            return;

        if (_travelTarget == TravelTarget.Warehouse && IsRadioTutorialPlaying())
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            HideHint();
        }
        if (_travelTarget == TravelTarget.Client && IsRadioTutorialPlaying())
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            HideHint();
        }

        bool onWarehouse = GameStateService.CurrentState == GameState.Warehouse;
        bool inZoneToClient = IsPlayerInZoneTo(TravelTarget.Client);
        bool inZoneToWarehouse = IsPlayerInZoneTo(TravelTarget.Warehouse);

        // Не предлагать переход в ту же зону: на складе — только в зону выдачи, в зоне выдачи — только на склад
        if (onWarehouse && _travelTarget == TravelTarget.Warehouse)
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            HideHint();
        }
        else if (!onWarehouse && _travelTarget == TravelTarget.Client)
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            HideHint();
        }

        if (_travelTarget == TravelTarget.None)
        {
            // На складе — цель только зона выдачи; в зоне выдачи — цель только склад
            bool canSetWarehouseTarget = !onWarehouse
                && !IsRadioTutorialPlaying()
                && GameStateService.CurrentState != GameState.Phone
                && (Time.time - _lastTeleportToClientTime) >= 2f
                && (_storyDirector == null || !string.Equals(_storyDirector.CurrentStepId, "go_to_phone", StringComparison.OrdinalIgnoreCase))
                && (_storyDirector == null || _storyDirector.IsStepAllowingTravelToWarehouse);
            if (canSetWarehouseTarget && inZoneToWarehouse)
            {
                _freeTeleportTargetActive = true;
                _travelTarget = TravelTarget.Warehouse;
            }
            else if (onWarehouse && !IsRadioTutorialPlaying())
            {
                string blockReason = GetWhyCannotReturnToClient();
                if (blockReason == null)
                {
                    _freeTeleportTargetActive = true;
                    _travelTarget = TravelTarget.Client;
                }
            }
            return;
        }

        if (!_freeTeleportTargetActive) return;

        // Не сбрасывать цель «склад», если подтверждение по F из зоны клиента (без подхода к двери).
        bool warehouseConfirmFromClientAllowed = _allowWarehouseConfirmFromClientArea
            || (_storyDirector != null && _storyDirector.IsWaitingForWarehouseConfirm && DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool);
        if (_travelTarget == TravelTarget.Warehouse && !inZoneToWarehouse && !warehouseConfirmFromClientAllowed)
        {
            _travelTarget = TravelTarget.None;
            _freeTeleportTargetActive = false;
            HideHint();
        }
        else if (_travelTarget == TravelTarget.Client)
        {
            bool nearExitDoor = _warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance;
            if (!inZoneToClient && !nearExitDoor)
            {
                _travelTarget = TravelTarget.None;
                _freeTeleportTargetActive = false;
                HideHint();
            }
        }
    }

    private void ApplyDoorHintFromZones()
    {
        if (PlayerHintView.Instance == null || _travelZones.Count == 0) return;
        Sprite doorHint = null;
        if (CanConfirmTravelToCurrentTarget() && _travelTarget != TravelTarget.None)
        {
            foreach (TravelZone zone in _travelZones)
            {
                if (zone != null && zone.Destination == _travelTarget)
                {
                    doorHint = zone.GetDoorHintSprite();
                    break;
                }
            }
        }
        PlayerHintView.Instance.SetDoorHint(doorHint);
    }

    private void HandleClientDialog()
    {
        if (_clientInteraction == null || _input == null) return;
        bool isDay2Start = _storyDirector != null && string.Equals(_storyDirector.CurrentStepId, "day2_start", StringComparison.OrdinalIgnoreCase);
        // Для обычного старта сюжета проверяем StoryStartOnClientInteract; для дня 2 (day2_start) всегда разрешаем
        if (!isDay2Start && !GameConfig.StoryStartOnClientInteract) return;
        // Если в руках предмет (телефон и т.д.) — E должен сначала положить его, а не запускать диалог с клиентом
        if (HandsRegistry.Hands != null && HandsRegistry.Hands.HasItem)
            return;
        if (_clientInteraction.IsPlayerLookingAtClient(_player) && !_clientInteraction.IsActive && _input.InteractPressed)
        {
            // День 2: выбор «сразу к клиентам» — запускаем Client_day2.1 и переходим к шагу day2_after_radio
            if (isDay2Start)
            {
                ExpireAllRadioAvailable();
                _storyDirector.AdvanceFromDay2StartToClient();
                return;
            }
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

        if (_tutorialPendingAction != TutorialPendingAction.None)
            return;
        _tutorialHint?.Show(GameConfig.Tutorial.goWarehouseKey);
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

        // Не телепортируем здесь: StopConversation вызовет conversationEnded → ClientDialogueStepCompleted → StoryDirector.Advance() → ForceTravel(Warehouse).
        // Так сработает OnTeleportedToWarehouse, разблокируется управление, покажется записка и закроется UI клиента.
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

    public void NotifyTutorialActionCompleted(TutorialPendingAction action)
    {
        if (action == TutorialPendingAction.WarehousePick)
        {
            MarkTutorialStepCompleted(GameConfig.Tutorial.warehousePickKey);
            _tutorialPendingAction = TutorialPendingAction.None;
            _tutorialStep = TutorialStep.None;
            _tutorialHint?.Hide();
            return;
        }
        if (_tutorialPendingAction != action) return;
        if (action == TutorialPendingAction.PressSpace)
            MarkTutorialStepCompleted(GameConfig.Tutorial.pressSpaceKey);
        _tutorialPendingAction = TutorialPendingAction.None;
        _tutorialStep = TutorialStep.None;
        _tutorialHint?.Hide();
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
                if (_tutorialPendingAction != TutorialPendingAction.None)
                    return;
                _tutorialHint?.Hide();
                break;
        }
    }

    public void ShowPhonePutHintOnce()
    {
        _preferEmptyOverMeetClient = false;
        ShowHintOnceByKey(GameConfig.Tutorial.phonePutKey);
    }

    public void ShowPhoneHint() => SetTutorialStep(TutorialStep.GoToPhone);
    public void HideHint()
    {
        if (_tutorialPendingAction != TutorialPendingAction.None)
            return;
        SetTutorialStep(TutorialStep.None);
    }

    public void ShowEmptyHint()
    {
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;
        if (HasActiveTutorial()) return;
        _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
    }

    public void ShowEmptyHintAfterPackagePick()
    {
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;
        if (HasActiveTutorial()) return;
        _preferEmptyOverMeetClient = true;
        _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
    }

    public void MarkProviderCallDone() => _providerCallDone = true;

    public void HidePhoneHint()
    {
        HideHint();
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

    public void ShowPhoneCallHint()
    {
        if (_tutorialHint == null) return;
        _preferEmptyOverMeetClient = false;
        ShowHintOnceByKey(GameConfig.Tutorial.phoneCallProviderKey);
    }

    public void ShowRadioHintOnce()
    {
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;
        _preferEmptyOverMeetClient = false;
        if (IsTutorialStepAlreadyShown(GameConfig.Tutorial.radioUseKey))
            return;
        ShowHintOnceByKey(GameConfig.Tutorial.radioUseKey);
    }

    public void NotifyPhonePutDown()
    {
        if (_tutorialHint == null) return;
        MarkTutorialStepCompleted(GameConfig.Tutorial.phonePutKey);
        // Показать tutorial.radio_use сразу после tutorial.phone_put: если игрок уже видел phone_put — показываем radio_use при опускании телефона
        bool phonePutWasShown = IsTutorialStepAlreadyShown(GameConfig.Tutorial.phonePutKey);
        if (!_providerCallDone && !phonePutWasShown) return;
        _preferEmptyOverMeetClient = false;
        if (IsTutorialStepAlreadyShown(GameConfig.Tutorial.radioUseKey))
            return;
        ShowHintOnceByKey(GameConfig.Tutorial.radioUseKey);
    }

    public void ShowWarehousePickHint()
    {
        if (_tutorialHint == null) return;
        if (!IsPackagePickAllowedByStory) return;
        _preferEmptyOverMeetClient = false;
        ShowHintOnceByKey(GameConfig.Tutorial.warehousePickKey);
    }

    public void ShowMeetClientHintOnce()
    {
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;

        if (_preferEmptyOverMeetClient || _meetClientHintShown)
        {
            if (!HasActiveTutorial())
                ShowHintOnceByKey(GameConfig.Tutorial.emptyKey);
            GameStateService.SetState(GameState.ClientDialog);
            return;
        }
        _preferEmptyOverMeetClient = false;
        _meetClientHintShown = true;
        ShowHintOnceByKey(GameConfig.Tutorial.meetClientKey);
        GameStateService.SetState(GameState.ClientDialog);
    }

    /// <summary> Показать подсказку по ключу (для туториала показывается спрайт по ключу в TutorialHintView). </summary>
    public void ShowHintRaw(string key)
    {
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;
        if (string.IsNullOrEmpty(key))
        {
            _tutorialHint?.Hide();
            return;
        }
        _preferEmptyOverMeetClient = false;
        _tutorialHint?.Show(key);
    }

    /// <summary> Подсказки router / return F / phone / radio_use всегда показываем для текущего шага сюжета, иначе при переходе шага или телепорте (HasActiveTutorial==false) показывался бы tutorial.empty. </summary>
    private bool IsCurrentStoryStepHint(string key)
    {
        if (_storyDirector == null || string.IsNullOrEmpty(key)) return false;
        string stepId = _storyDirector.CurrentStepId;
        if (string.IsNullOrEmpty(stepId)) return false;
        TutorialConfig t = GameConfig.Tutorial;
        if (key == t.routerHintKey) return string.Equals(stepId, "go_to_router", StringComparison.OrdinalIgnoreCase);
        if (key == t.returnPressFKey) return string.Equals(stepId, "return_from_warehouse", StringComparison.OrdinalIgnoreCase);
        if (key == t.phoneHintKey) return string.Equals(stepId, "go_to_phone", StringComparison.OrdinalIgnoreCase);
        if (key == t.radioUseKey) return string.Equals(stepId, "go_to_radio", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public void ShowHintOnceByKey(string key)
    {
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;
        if (string.IsNullOrEmpty(key))
        {
            if (!HasActiveTutorial())
                _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
            return;
        }
        // Шаг уже выполнен игроком — не показываем снова (кроме подсказок текущего шага сюжета).
        if (IsTutorialStepAlreadyShown(key))
        {
            if (IsCurrentStoryStepHint(key))
            {
                _hintKeysDisplayed.Add(key);
                _tutorialHint?.Show(key);
            }
            else if (!HasActiveTutorial())
                _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
            return;
        }
        // Уже показали этот шаг, ждём выполнения — не спамим (кроме подсказок текущего шага сюжета).
        if (_hintKeysDisplayed.Contains(key))
        {
            if (IsCurrentStoryStepHint(key))
                _tutorialHint?.Show(key);
            else if (!HasActiveTutorial())
                _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
            return;
        }
        _hintKeysDisplayed.Add(key);
        _preferEmptyOverMeetClient = false;
        string pressSpaceKey = GameConfig.Tutorial.pressSpaceKey;
        string warehousePickKey = GameConfig.Tutorial.warehousePickKey;
        if (key == pressSpaceKey)
            _tutorialPendingAction = TutorialPendingAction.PressSpace;
        else if (key == warehousePickKey)
            _tutorialPendingAction = TutorialPendingAction.WarehousePick;
        _tutorialHint?.Show(key);
    }

    public void SetTravelTarget(TravelTarget target, string hintKey, bool useFreeTeleportPointForClient = false, bool allowWarehouseConfirmFromClient = false)
    {
        _freeTeleportTargetActive = false;
        _allowWarehouseConfirmFromClientArea = allowWarehouseConfirmFromClient && target == TravelTarget.Warehouse;
        if (_allowWarehouseConfirmFromClientArea)
            _freeTeleportTargetActive = true;
        _useFreeTeleportPointForNextClientTravel = useFreeTeleportPointForClient && target == TravelTarget.Client;
        if (target == TravelTarget.Warehouse && IsRadioTutorialPlaying())
        {
            _travelTarget = TravelTarget.None;
            _tutorialHint?.Hide();
            return;
        }
        if (target == TravelTarget.Client && IsRadioTutorialPlaying())
        {
            _travelTarget = TravelTarget.None;
            _tutorialHint?.Hide();
            return;
        }
        _travelTarget = target;
        if (string.IsNullOrEmpty(hintKey))
        {
            if (_tutorialPendingAction == TutorialPendingAction.None)
            {
                // Не скрывать туториал при шаге "идти на склад к радио" — оставляем подсказку radio_use видимой
                bool keepRadioTutorialVisible = _storyDirector != null
                    && string.Equals(_storyDirector.CurrentStepId, "go_to_warehouse_for_radio", StringComparison.OrdinalIgnoreCase);
                // Не скрывать подсказку meet_client, пока ждём нажатия E у клиента
                bool keepMeetClientHintVisible = _meetClientHintShown && GameStateService.CurrentState == GameState.ClientDialog;
                if (!keepRadioTutorialVisible && !keepMeetClientHintVisible)
                    _tutorialHint?.Hide();
            }
        }
        else
        {
            if (_tutorialPendingAction != TutorialPendingAction.None) return;
            if (target != TravelTarget.Client)
                _preferEmptyOverMeetClient = false;
            // Радио приоритетно: пока ждём нажатия E у радио — показываем подсказку только если шаг ещё не показывался.
            bool forceShowForRadio = target == TravelTarget.Client && _storyDirector != null && _storyDirector.IsWaitingForRadioComplete;
            if (!forceShowForRadio)
            {
                string key = target == TravelTarget.Warehouse ? GameConfig.Tutorial.doorWarehouseKey : GameConfig.Tutorial.returnPressFKey;
                if (IsTutorialStepAlreadyShown(key))
                    return;
                if (_hintKeysDisplayed.Contains(key))
                    return;
                _hintKeysDisplayed.Add(key);
            }
            else if (IsTutorialStepAlreadyShown(GameConfig.Tutorial.radioUseKey))
            {
                hintKey = GameConfig.Tutorial.emptyKey; // показать пустую подсказку по ключу
            }
            _tutorialHint?.Show(hintKey);
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

    public void SetAcceptAnyPackageForReturn(bool acceptAny)
    {
        _acceptAnyPackageForReturn = acceptAny;
    }

    public bool AcceptAnyPackageForReturn => _acceptAnyPackageForReturn;

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

    public void ActivateRadioEvent(string id, float? staticVolumeOverride = null)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_radioPlayed.Contains(id)) return;
        if (_radioExpired.Contains(id)) return;

        _radioAvailable.Add(id);
        OnRadioEventActivated?.Invoke(id, staticVolumeOverride);
    }

    public void SetRadioStaticVolume(float volume)
    {
        OnRadioStaticVolumeRequested?.Invoke(volume);
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
        // Игрок нажал E у радио — шаг туториала «подойдите к радио» выполнен.
        MarkTutorialStepCompleted(GameConfig.Tutorial.radioUseKey);
    }

    public void ExpireAllRadioAvailable()
    {
        foreach (string id in _radioAvailable)
            _radioExpired.Add(id);
        _radioAvailable.Clear();
    }

    private bool PerformTravel(TravelTarget target, bool ignoreClientRequirements, bool freeTeleport = false)
    {
        // Не телепортировать в ту же зону, где уже находимся (отслеживаем последний телепорт)
        if (_lastTeleportDestination != TravelTarget.None && _lastTeleportDestination == target)
            return false;

        if (target == TravelTarget.Warehouse)
        {
            if (IsRadioTutorialPlaying())
                return false;
            Transform point = freeTeleport && _freeTeleportToWarehousePoint != null ? _freeTeleportToWarehousePoint : _warehousePoint;
            if (_player == null || point == null)
                return false;
            RemovePackageFromHands();
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
            _lastTeleportDestination = TravelTarget.Warehouse;
            OnTeleportedToWarehouse?.Invoke();
            _clientInteraction?.CloseUI();
            ShowHintOnceByKey(GameConfig.Tutorial.returnToClientKey);
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
            _lastTeleportDestination = TravelTarget.Client;
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

            return true;
        }

        return false;
    }

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

    private string GetWhyCannotReturnToClient()
    {
        // Опциональное радио: на шаге optional_radio_video_after_day1_2 игрок уже на складе, может не слушать или уйти в зону выдачи в любой момент.
        if (BlockReturnUntilPlayerDay1_2ReplicaDone && !IsOptionalRadioVideoAfterDay12Step)
            return "ждётся реплика Player_Day1_2_Replica (до конца диалога на радио)";
        if (_storyDirector != null && _storyDirector.IsRunning && !_storyDirector.IsStepAllowingTravelToClient)
            return "сценарий не разрешает возврат к клиенту (текущий шаг: " + (_storyDirector.CurrentStepId ?? "?") + ")";
        if (GameStateService.RequiredPackageNumber > 0 && !CanLeaveWarehouseToClient())
            return "нужна посылка в руках (требуется №" + GameStateService.RequiredPackageNumber + ")";
        return null;
    }

    private bool CanLeaveWarehouseToClient()
    {
        bool inExitZone = IsPlayerInZoneTo(TravelTarget.Client) || (_warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance);
        if (!inExitZone)
            return false;

        PlayerHands hands = HandsRegistry.Hands;

        if (_acceptAnyPackageForReturn)
            return hands != null && hands.Current is PackageHoldable;

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
        bool exitInside = IsPlayerInZoneTo(TravelTarget.Client) || (_warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance);
        if (!exitInside)
            return false;
        PlayerHands hands = HandsRegistry.Hands;
        if (hands == null || hands.Current is not PackageHoldable pkg)
            return false;
        packageNumber = pkg.Number;
        return packageNumber == _pendingDialogueReturnPackage;
    }
}