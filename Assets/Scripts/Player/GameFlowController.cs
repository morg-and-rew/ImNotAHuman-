using PixelCrushers;
using PixelCrushers.DialogueSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    [Tooltip("Наклон камеры вниз (градусы) после видео Radio_Day1_2 и телепорта к столу. Меняй здесь, если камера смотрит под неправильным углом.")]
    [SerializeField] private float _postVideoCameraPitchDown = 32.8f;
    [Tooltip("Поворот камеры по горизонтали (Yaw, градусы 0–360) после видео. Задаёт направление взгляда после телепорта к столу.")]
    [SerializeField] private float _postVideoCameraYaw = 244.4f;
    [Tooltip("Точка спавна игрока в начале второго дня.")]
    [SerializeField] private Transform _playerSpawnPoint;

    [Header("Intro")]
    [SerializeField] private IntroView _introView;

    // День 2: головокружение и падение (только const — значения из сцены не перезаписывают код).
    private const bool Day2DizzyFallAfterIntro = true;
    private const float Day2DizzyAndStepDuration = 3.55f;
    private const float Day2DizzyCameraSmoothTime = 0.28f;
    private const float Day2DizzyRollFrequencyHz = 0.32f;
    private const float Day2DizzyYawFrequencyHz = 0.24f;
    private const float Day2DizzyPitchFrequencyHz = 0.4f;
    private const float Day2DizzyHeadRollDegrees = 9.5f;
    private const float Day2DizzyHeadYawDegrees = 4.2f;
    private const float Day2DizzyHeadPitchDegrees = 3f;
    private const float Day2StepBackDistance = 0.38f;
    private const float Day2StepBackSmoothTime = 0.22f;
    private const float Day2FallDuration = 2.05f;
    private const float Day2FallCameraPitchDeltaFromSaved = -46f;
    private const float Day2FallDropMeters = 0.22f;
    private const float Day2FallBackSlideMeters = 0.035f;
    // Запрокидывание по u начинается позже; дальше цель идёт по Эйлерам + SmoothDampAngle (не Slerp кватов).
    private const float Day2FallHeadTiltDelayU = 0.18f;
    private const float Day2FallHeadPitchSmoothTime = 0.68f;
    private const float Day2FallHeadYawRollSmoothTime = 0.52f;
    private const float Day2FallBodyPosSmoothTime = 0.14f;
    // После этой доли u доводим голову Slerp к qSettled — без скачка после SmoothDampAngle.
    private const float Day2FallHeadMergeFromU = 0.83f;
    // К концу головокружения гасим только pitch-качание (вниз/вверх), не трогая yaw/roll.
    private const float Day2DizzyPitchSettleStartLinear = 0.84f;
    // Падение: ease-out на первой доле u, затем линейный дожим до 1 — иначе у u→1 скорость → 0 («лежим, а время ещё течёт»).
    private const float Day2FallEaseOutPower = 1.78f;
    private const float Day2FallPosEaseUntilU = 0.74f;
    private const float Day2FallPosEaseAnchor = 0.82f;
    // Раньше fade к чёрному: не залипаем в «уже лежим» на полном кадре.
    private const float Day2FallFadeStartU = 0.88f;
    private const float Day2FallTailDuration = 0.45f;
    private const float Day2TailEaseInPower = 1.22f;
    private const float Day2FallTailExtraDrop = 0.13f;
    private const float Day2FallTailExtraPitchDegrees = 16f;
    private const float Day2AfterFallFadeToBlackDuration = 1.05f;
    private const float Day2AfterBlackFadeInDuration = 1.2f;
    // TSV движения камеры (день 2). false — отключить запись на диск.
    private const bool Day2CameraMotionLog = false;

    private float _day2CamLogPrevHolderPitchX;
    private bool _day2CamLogPitchPrevValid;

    [Header("Fade to black (end of day)")]
    [SerializeField] private FadeToBlackView _fadeToBlackView;
    [Tooltip("Длительность затемнения при переходе склад ↔ зона выдачи (сек). 0 = без затемнения.")]
    [SerializeField, Min(0f)] private float _travelFadeDuration = 0.5f;
    [SerializeField] private GameSoundController _gameSoundController;
    [Header("Client Arrival Sound")]
    [SerializeField] private AudioClip _clientArriveBellClip;
    [SerializeField, Range(0f, 1f)] private float _clientArriveBellVolume = 0.7f;

    [Header("Localization (UI Text Table)")]
    [SerializeField] private TextTable _uiTextTable;
    [SerializeField] private string _language = "en";

    [Header("Tutorial")]
    [SerializeField] private TutorialHintView _tutorialHint;

    [Header("Main Menu (Start Screen)")]
    [Tooltip("Показывать стартовое меню (Continue / New Game / Options / Exit) перед началом сюжета.")]
    [SerializeField] private bool _showMainMenuOnStart = true;
    [Tooltip("Фоновая картинка для стартового меню (используется как обычный Image). Если не задана — будет черный фон.")]
    [SerializeField] private Sprite _mainMenuBackground;
    [Tooltip("Шрифт TextMeshPro для кнопок стартового меню. Если не задан — используется TMP default.")]
    [SerializeField] private TMPro.TMP_FontAsset _mainMenuFont;

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

    public bool BlockPhoneDropUntilProviderCallOnTutorial =>
        _storyDirector != null
        && string.Equals(_storyDirector.CurrentStepId, "go_to_phone", StringComparison.OrdinalIgnoreCase)
        && !_providerCallDone;

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
    private string _pendingStoryCarryItemId;
    private bool _acceptAnyPackageForReturn;

    private bool _freeTeleportTargetActive;
    private bool _flyModeActive;
    /// <summary> True, если переход на склад подтверждается нажатием F из зоны клиента (без подхода к двери). Например после Client_Day1.4 ChoseToGivePackage5577. </summary>
    private bool _allowWarehouseConfirmFromClientArea;
    private bool _tutorialWarehouseVisit;
    private bool _useFreeTeleportPointForNextClientTravel;
    private float _lastTeleportToClientTime = -999f;
    private bool _isTravelFading;
    /// <summary> True, когда переход на склад из диалога идёт через «сначала полное затемнение, потом телепорт» — StoryDirector не должен вызывать ForceTravel. </summary>
    private bool _warehouseTravelFromDialogueAfterFade;

    /// <summary> True, если сейчас выполняется переход на склад с предварительным полным затемнением (F из диалога Client_Day1.4). </summary>
    public bool IsWarehouseTravelFromDialogueAfterFade => _warehouseTravelFromDialogueAfterFade;

    private enum MainMenuChoice
    {
        None,
        Continue,
        NewGame
    }

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

    public bool ShouldShowClientInteractHint()
    {
        if (_clientInteraction != null && (_clientInteraction.IsActive || _clientInteraction.IsWaitingForContinue))
            return true;

        if (_storyDirector == null)
            return false;

        if (string.Equals(_storyDirector.CurrentStepId, "day2_start", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!_storyDirector.HasStoryStarted)
            return GameConfig.StoryStartOnClientInteract;

        // Подсказка "пустить клиента" должна следовать за сюжетом, а не только за глобальным GameState.
        // Иначе бывают шаги, где взаимодействие по E уже разрешено, но GameState ещё не ClientDialog.
        return _storyDirector.IsWaitingForClientInteraction;
    }

    public bool IsDay2OrLater()
    {
        if (_storyDirector == null)
            return false;
        string stepId = _storyDirector.CurrentStepId;
        return !string.IsNullOrEmpty(stepId)
            && stepId.IndexOf("day2", StringComparison.OrdinalIgnoreCase) >= 0;
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
        _player.transform.rotation = Quaternion.Euler(0f, _postVideoCameraYaw, 0f);
        _player.SyncRotationFromCamera();
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
            _introView.PlayFadeFromBlack(duration, () =>
            {
                if (Day2DizzyFallAfterIntro && _player != null)
                    StartCoroutine(Day2PostIntroTempDizzyFallRoutine(onComplete));
                else
                    onComplete?.Invoke();
            });
            _fadeToBlackView?.Hide();
        }
        else
        {
            _fadeToBlackView?.Hide();
            if (Day2DizzyFallAfterIntro && _player != null)
                StartCoroutine(Day2PostIntroTempDizzyFallRoutine(onComplete));
            else
                onComplete?.Invoke();
        }
    }

    private IEnumerator Day2PostIntroTempDizzyFallRoutine(Action onComplete)
    {
        SetPlayerControlBlocked(true);

        PlayerView p = _player;
        RuntimeDizzyBlurVolume blurVol = p != null
            ? RuntimeDizzyBlurVolume.TryCreate(p.PlayerCamera)
            : null;
        Vector3 flatPlayerFwd = new Vector3(p.transform.forward.x, 0f, p.transform.forward.z);
        RuntimeDizzyVillainSilhouette villainSil = RuntimeDizzyVillainSilhouette.TryCreate(
            p.transform.position,
            flatPlayerFwd,
            null,
            p.PlayerCamera);
        Transform holder = p.CameraHolder;
        CharacterController cc = p.Controller;

        float routineT = 0f;
        if (Day2CameraMotionLog)
        {
            _day2CamLogPitchPrevValid = false;
            try
            {
                File.WriteAllText(
                    Day2CameraMotionLogPath,
                    "routine_t\tphase\tlinear\tu\tk\tholderLx\tholderLy\tholderLz\tcamWx\tcamWy\tcamWz\t"
                    + "camPx\tcamPy\tcamPz\tplayerY\tdHolderPitchDegPerSec\taux1\taux2\taux3\tnote\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Day2 camera log init: {ex.Message}");
            }

            Debug.Log("[Day2] Camera TSV → " + Day2CameraMotionLogPath);
        }

        Vector3 savedPos = p.transform.position;
        Quaternion savedRot = p.transform.rotation;
        Vector3 savedHolderEuler = holder != null ? holder.localEulerAngles : Vector3.zero;

        Vector3 flatBack = new Vector3(-p.transform.forward.x, 0f, -p.transform.forward.z);
        if (flatBack.sqrMagnitude < 0.0001f)
            flatBack = new Vector3(0f, 0f, -1f);
        flatBack.Normalize();

        Vector3 stepStartXZ = new Vector3(p.transform.position.x, 0f, p.transform.position.z);
        Vector3 stepEndXZ = stepStartXZ + new Vector3(flatBack.x, 0f, flatBack.z) * Day2StepBackDistance;

        Vector3 shakeSmoothed = Vector3.zero;
        Vector3 shakeVel = Vector3.zero;
        Vector2 smoothStepXZ = new Vector2(stepStartXZ.x, stepStartXZ.z);
        Vector2 smoothStepXZVel = Vector2.zero;

        float phaseT = 0f;
        float phaseDur = Mathf.Max(0.5f, Day2DizzyAndStepDuration);

        // --- Параллельно: плавное «кружение» головы (SmoothDamp) и отход назад (двойной SmoothStep) ---
        while (phaseT < phaseDur)
        {
            phaseT += Time.deltaTime;
            routineT += Time.deltaTime;
            float linear = Mathf.Clamp01(phaseT / phaseDur);
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, linear)));

            float t = phaseT;
            float twoPi = Mathf.PI * 2f;
            Vector3 targetShake = new Vector3(
                Mathf.Sin(t * twoPi * Day2DizzyPitchFrequencyHz) * Day2DizzyHeadPitchDegrees,
                Mathf.Sin(t * twoPi * Day2DizzyYawFrequencyHz) * Day2DizzyHeadYawDegrees,
                Mathf.Sin(t * twoPi * Day2DizzyRollFrequencyHz) * Day2DizzyHeadRollDegrees
            ) * envelope;

            if (holder != null)
            {
                shakeSmoothed = Vector3.SmoothDamp(
                    shakeSmoothed,
                    targetShake,
                    ref shakeVel,
                    Day2DizzyCameraSmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime);
                float settleStart = Mathf.Clamp(Day2DizzyPitchSettleStartLinear, 0f, 0.95f);
                if (linear > settleStart)
                {
                    float settleT = (linear - settleStart) / Mathf.Max(0.06f, 1f - settleStart);
                    float settleK = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, settleT));
                    shakeSmoothed.x = Mathf.Lerp(shakeSmoothed.x, 0f, settleK);
                }

                holder.localEulerAngles = savedHolderEuler + shakeSmoothed;
            }

            float uStep = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, linear));
            if (cc != null)
            {
                Vector3 cur = p.transform.position;
                Vector3 delta = Vector3.zero;
                if (Day2StepBackDistance > 0.0001f)
                {
                    Vector3 wantXZ = Vector3.Lerp(stepStartXZ, stepEndXZ, uStep);
                    Vector2 want2 = new Vector2(wantXZ.x, wantXZ.z);
                    float stepSt = Mathf.Max(0.04f, Day2StepBackSmoothTime);
                    smoothStepXZ = Vector2.SmoothDamp(
                        smoothStepXZ,
                        want2,
                        ref smoothStepXZVel,
                        stepSt,
                        Mathf.Infinity,
                        Time.deltaTime);
                    delta.x = smoothStepXZ.x - cur.x;
                    delta.z = smoothStepXZ.y - cur.z;
                }

                if (delta.sqrMagnitude > 1e-8f)
                    cc.Move(delta);
            }

            if (blurVol != null)
            {
                float ramp = Mathf.Max(0.05f, RuntimeDizzyBlurVolume.RampInPhasePortion);
                float blurIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(linear / ramp));
                blurVol.SetWeight(blurIn * RuntimeDizzyBlurVolume.MaxVolumeWeight);
            }

            villainSil?.SetAlphaNormalized(RuntimeDizzyVillainSilhouette.EvaluateAlphaDizzy(linear));
            villainSil?.FaceCamera(GetDay2DizzyCameraWorldPosition(p));

            if (Day2CameraMotionLog && holder != null)
            {
                Day2LogCameraMotionTsv(
                    routineT,
                    "dizzy",
                    linear,
                    -1f,
                    -1f,
                    holder,
                    p,
                    shakeSmoothed.x,
                    uStep,
                    envelope,
                    "");
            }

            yield return null;
        }

        if (holder != null)
            holder.localEulerAngles = savedHolderEuler + shakeSmoothed;

        Vector3 eDizzyCam = holder != null ? holder.localEulerAngles : Vector3.zero;
        // Только запрокидывание от углов конца головокружения; не тянуть yaw/roll к saved (0,0) —
        // иначе в TSV видно рывок: из (~0,357°,9°) в (-46°,0,0) и лишние ощущения «вниз»/скачка.
        Vector3 eSettledCam = new Vector3(
            eDizzyCam.x + Day2FallCameraPitchDeltaFromSaved,
            eDizzyCam.y,
            eDizzyCam.z);
        Quaternion qSettled = Quaternion.Euler(eSettledCam);

        if (Day2CameraMotionLog && holder != null)
        {
            Day2LogCameraMotionTsv(
                routineT,
                "handoff",
                -1f,
                -1f,
                -1f,
                holder,
                p,
                eDizzyCam.x,
                eDizzyCam.y,
                eDizzyCam.z,
                "end_dizzy_eDizzyCam");
            Day2LogCameraMotionTsv(
                routineT,
                "handoff",
                -1f,
                -1f,
                -1f,
                holder,
                p,
                eSettledCam.x,
                eSettledCam.y,
                eSettledCam.z,
                "target_eSettledCam");
        }

        // --- Падение: капсула вертикальна. Камера — Quaternion. Кривая ease-out, часть опускания уже в конце фазы выше. ---
        Vector3 fallStartPos = p.transform.position;
        float yawDeg = p.transform.eulerAngles.y;
        Quaternion uprightYaw = Quaternion.Euler(0f, yawDeg, 0f);
        float fallRemainDrop = Mathf.Max(0.01f, Day2FallDropMeters);
        Vector3 fallEndPos = fallStartPos + Vector3.down * fallRemainDrop + flatBack * Day2FallBackSlideMeters;

        if (cc != null)
            cc.enabled = false;

        blurVol?.SetDefocusNormalized(0f);

        float fallT = 0f;
        float fallDur = Mathf.Max(0.1f, Day2FallDuration);
        float easePow = Mathf.Max(1.02f, Day2FallEaseOutPower);
        bool fadeOutDone = _fadeToBlackView == null;
        bool fadeOutStarted = false;
        float headEulerX = eDizzyCam.x;
        float headEulerY = eDizzyCam.y;
        float headEulerZ = eDizzyCam.z;
        float headVelX = 0f;
        float headVelY = 0f;
        float headVelZ = 0f;
        bool headMergeStarted = false;
        Quaternion qHeadMergeStart = Quaternion.identity;
        float posBlendSmoothed = 0f;
        float posBlendVel = 0f;

        while (fallT < fallDur)
        {
            fallT += Time.deltaTime;
            routineT += Time.deltaTime;
            float u = Mathf.Clamp01(fallT / fallDur);

            float uEaseEnd = Mathf.Clamp(Day2FallPosEaseUntilU, 0.05f, 0.98f);
            float posBlend;
            if (u <= uEaseEnd)
            {
                float uu = u / uEaseEnd;
                posBlend = (1f - Mathf.Pow(1f - uu, easePow)) * Day2FallPosEaseAnchor;
            }
            else
            {
                float linSpan = Mathf.Max(0.001f, 1f - uEaseEnd);
                posBlend = Mathf.Lerp(Day2FallPosEaseAnchor, 1f, (u - uEaseEnd) / linSpan);
            }

            float bodySt = Mathf.Max(0.04f, Day2FallBodyPosSmoothTime);
            posBlendSmoothed = Mathf.SmoothDamp(posBlendSmoothed, posBlend, ref posBlendVel, bodySt, Mathf.Infinity, Time.deltaTime);
            p.transform.SetPositionAndRotation(
                Vector3.Lerp(fallStartPos, fallEndPos, posBlendSmoothed),
                uprightYaw);

            float logMt = -1f;
            float logMerge = 0f;
            bool mergeStartedThisFrame = false;
            if (holder != null)
            {
                float mergeFrom = Mathf.Clamp(Day2FallHeadMergeFromU, 0.55f, 0.98f);
                if (u >= mergeFrom)
                {
                    if (!headMergeStarted)
                    {
                        headMergeStarted = true;
                        mergeStartedThisFrame = true;
                        qHeadMergeStart = Quaternion.Euler(headEulerX, headEulerY, headEulerZ);
                    }

                    float mtRaw = Mathf.InverseLerp(mergeFrom, 1f, u);
                    float mt = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, mtRaw));
                    logMt = mt;
                    logMerge = 1f;
                    holder.localRotation = Quaternion.Slerp(qHeadMergeStart, qSettled, mt);
                }
                else
                {
                    float delayU = Mathf.Clamp(Day2FallHeadTiltDelayU, 0f, 0.5f);
                    float uHead = Mathf.Clamp01((u - delayU) / Mathf.Max(0.001f, 1f - delayU));
                    float headProgress = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, uHead)));
                    float yzBlend = Mathf.SmoothStep(0f, 1f, Mathf.Pow(headProgress, 0.78f));
                    float dPitch = Mathf.DeltaAngle(eDizzyCam.x, eSettledCam.x);
                    float dYaw = Mathf.DeltaAngle(eDizzyCam.y, eSettledCam.y);
                    float dRoll = Mathf.DeltaAngle(eDizzyCam.z, eSettledCam.z);
                    float targetX = eDizzyCam.x + dPitch * headProgress;
                    float targetY = eDizzyCam.y + dYaw * yzBlend;
                    float targetZ = eDizzyCam.z + dRoll * yzBlend;
                    float pitchSt = Mathf.Max(0.05f, Day2FallHeadPitchSmoothTime);
                    float yzSt = Mathf.Max(0.05f, Day2FallHeadYawRollSmoothTime);
                    headEulerX = Mathf.SmoothDampAngle(headEulerX, targetX, ref headVelX, pitchSt, Mathf.Infinity, Time.deltaTime);
                    headEulerY = Mathf.SmoothDampAngle(headEulerY, targetY, ref headVelY, yzSt, Mathf.Infinity, Time.deltaTime);
                    headEulerZ = Mathf.SmoothDampAngle(headEulerZ, targetZ, ref headVelZ, yzSt, Mathf.Infinity, Time.deltaTime);
                    holder.localEulerAngles = new Vector3(headEulerX, headEulerY, headEulerZ);
                }
            }

            if (blurVol != null)
            {
                blurVol.SetWeight(RuntimeDizzyBlurVolume.MaxVolumeWeight);
                float defBegin = Mathf.Clamp01(RuntimeDizzyBlurVolume.DefocusFallBegin);
                float defT = u <= defBegin
                    ? 0f
                    : Mathf.Clamp01((u - defBegin) / (1f - defBegin));
                float defSmooth = 1f - Mathf.Pow(1f - defT, 1.25f);
                blurVol.SetDefocusNormalized(defSmooth);
            }

            villainSil?.SetAlphaNormalized(RuntimeDizzyVillainSilhouette.EvaluateAlphaFall(u));
            villainSil?.FaceCamera(GetDay2DizzyCameraWorldPosition(p));

            if (Day2CameraMotionLog && holder != null)
            {
                Day2LogCameraMotionTsv(
                    routineT,
                    "fall",
                    -1f,
                    u,
                    -1f,
                    holder,
                    p,
                    posBlend,
                    logMerge,
                    logMt,
                    mergeStartedThisFrame ? "merge_phase_start" : "");
            }

            if (!fadeOutStarted && _fadeToBlackView != null && u >= Day2FallFadeStartU)
            {
                fadeOutStarted = true;
                _fadeToBlackView.Play(Day2AfterFallFadeToBlackDuration, () => fadeOutDone = true);
            }

            yield return null;
        }

        p.transform.SetPositionAndRotation(fallEndPos, uprightYaw);
        if (holder != null)
            holder.localRotation = headMergeStarted ? qSettled : Quaternion.Euler(headEulerX, headEulerY, headEulerZ);

        if (Day2CameraMotionLog && holder != null)
        {
            Day2LogCameraMotionTsv(
                routineT,
                "handoff",
                -1f,
                -1f,
                -1f,
                holder,
                p,
                headMergeStarted ? 1f : 0f,
                0f,
                0f,
                "after_fall_loop_holder");
        }

        // --- Хвост: плавное дожимание вниз + полный расфокус перед затемнением ---
        Vector3 tailFrom = fallEndPos;
        Vector3 tailTo = tailFrom + Vector3.down * Day2FallTailExtraDrop + flatBack * 0.028f;
        float tailElapsed = 0f;
        float tailDur = Mathf.Max(0.05f, Day2FallTailDuration);
        Quaternion qAfterFall = holder != null ? holder.localRotation : Quaternion.identity;
        Vector3 eTailLean = new Vector3(
            eSettledCam.x - Day2FallTailExtraPitchDegrees,
            eSettledCam.y,
            eSettledCam.z);
        Quaternion qTailLean = holder != null ? Quaternion.Euler(eTailLean) : Quaternion.identity;

        float tailEasePow = Mathf.Max(1.02f, Day2TailEaseInPower);
        while (tailElapsed < tailDur)
        {
            if (!fadeOutStarted && _fadeToBlackView != null)
            {
                fadeOutStarted = true;
                _fadeToBlackView.Play(Day2AfterFallFadeToBlackDuration, () => fadeOutDone = true);
            }

            tailElapsed += Time.deltaTime;
            routineT += Time.deltaTime;
            float k = Mathf.Clamp01(tailElapsed / tailDur);
            float k2 = 1f - Mathf.Pow(1f - k, tailEasePow);
            float k2Soft = Mathf.SmoothStep(0f, 1f, k2);
            p.transform.SetPositionAndRotation(Vector3.Lerp(tailFrom, tailTo, k2Soft), uprightYaw);
            if (holder != null)
                holder.localRotation = Quaternion.Slerp(qAfterFall, qTailLean, k2Soft);
            if (blurVol != null)
            {
                blurVol.SetWeight(RuntimeDizzyBlurVolume.MaxVolumeWeight);
                blurVol.SetDefocusNormalized(1f);
            }

            villainSil?.FaceCamera(GetDay2DizzyCameraWorldPosition(p));

            if (Day2CameraMotionLog && holder != null)
            {
                Day2LogCameraMotionTsv(
                    routineT,
                    "tail",
                    -1f,
                    -1f,
                    k2,
                    holder,
                    p,
                    k,
                    tailElapsed,
                    0f,
                    "");
            }

            yield return null;
        }

        p.transform.SetPositionAndRotation(tailTo, uprightYaw);
        if (holder != null)
            holder.localRotation = qTailLean;

        if (Day2CameraMotionLog && holder != null)
        {
            Day2LogCameraMotionTsv(
                routineT,
                "handoff",
                -1f,
                -1f,
                1f,
                holder,
                p,
                0f,
                0f,
                0f,
                "after_tail_snap_qTailLean");
        }

        if (cc != null)
            cc.enabled = true;

        villainSil?.DestroySelf();

        if (!fadeOutStarted && _fadeToBlackView != null)
        {
            fadeOutStarted = true;
            _fadeToBlackView.Play(Day2AfterFallFadeToBlackDuration, () => fadeOutDone = true);
        }

        while (!fadeOutDone)
            yield return null;

        blurVol?.DestroySelf();
        blurVol = null;

        p.TeleportTo(savedPos, savedRot);
        if (holder != null)
            holder.localEulerAngles = savedHolderEuler;
        p.SyncRotationFromCamera();

        bool fadeInDone = false;
        if (_fadeToBlackView != null)
            _fadeToBlackView.PlayFadeFromBlack(Day2AfterBlackFadeInDuration, () => fadeInDone = true);
        else
            fadeInDone = true;

        while (!fadeInDone)
            yield return null;

        onComplete?.Invoke();
    }

    private static Vector3 GetDay2DizzyCameraWorldPosition(PlayerView p)
    {
        if (p != null && p.PlayerCamera != null)
            return p.PlayerCamera.transform.position;
        if (p != null)
            return p.transform.position + Vector3.up * 1.65f;
        return Vector3.zero;
    }

    private static string Day2CameraMotionLogPath =>
        Path.Combine(Application.persistentDataPath, "Day2_CameraHolder_motion.tsv");

    private void Day2LogCameraMotionTsv(
        float routineT,
        string phase,
        float linear,
        float u,
        float k,
        Transform holder,
        PlayerView pv,
        float aux1,
        float aux2,
        float aux3,
        string note)
    {
        if (!Day2CameraMotionLog || holder == null)
            return;

        Vector3 h = holder.localEulerAngles;
        Transform camTr = pv != null && pv.PlayerCamera != null ? pv.PlayerCamera.transform : null;
        Vector3 w = camTr != null ? camTr.eulerAngles : Vector3.zero;
        Vector3 camP = camTr != null ? camTr.position : Vector3.zero;
        float playerY = pv != null ? pv.transform.position.y : 0f;

        float dt = Mathf.Max(Time.deltaTime, 0.00001f);
        float dPitchDegS = 0f;
        if (_day2CamLogPitchPrevValid)
            dPitchDegS = Mathf.DeltaAngle(_day2CamLogPrevHolderPitchX, h.x) / dt;
        _day2CamLogPrevHolderPitchX = h.x;
        _day2CamLogPitchPrevValid = true;

        string safeNote = string.IsNullOrEmpty(note) ? "" : note.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F4}\t{1}\t{2:F4}\t{3:F4}\t{4:F4}\t{5:F3}\t{6:F3}\t{7:F3}\t{8:F3}\t{9:F3}\t{10:F3}\t{11:F4}\t{12:F4}\t{13:F4}\t{14:F4}\t{15:F2}\t{16:F5}\t{17:F5}\t{18:F5}\t{19}\n",
            routineT,
            phase,
            linear,
            u,
            k,
            h.x,
            h.y,
            h.z,
            w.x,
            w.y,
            w.z,
            camP.x,
            camP.y,
            camP.z,
            playerY,
            dPitchDegS,
            aux1,
            aux2,
            aux3,
            safeNote);

        try
        {
            File.AppendAllText(Day2CameraMotionLogPath, line);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Day2 camera TSV append failed: {ex.Message}");
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
        if (_gameSoundController == null)
            _gameSoundController = GameSoundController.Instance;

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

        if (_showMainMenuOnStart)
        {
            StartCoroutine(ShowMainMenuThenStart());
            return;
        }

        if (GameSaveSystem.LoadFromSaveAtStart && GameSaveSystem.LoadDay1() != null)
        {
            StartCoroutine(StartFromSavedGameDelayed());
            return;
        }

        EnterIntro();
    }

    private System.Collections.IEnumerator ShowMainMenuThenStart()
    {
        // Важно: GameFlowController.Init уже вызван, но сюжет должен ждать выбор пользователя.
        if (_introView != null)
        {
            _introView.Stop();
            _introView.gameObject.SetActive(false);
        }

        _tutorialHint?.Hide();
        TutorialHintView.Instance?.Hide();

        _controller?.SetBlock(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        InputRebindMenu rebindMenu = _inputRebindMenu;
        if (rebindMenu != null)
            rebindMenu.gameObject.SetActive(false);

        bool optionsOpen = false;
        bool canContinue = GameSaveSystem.LoadDay1() != null;

        MainMenuChoice choice = MainMenuChoice.None;
        bool exitRequested = false;

        MainMenuUI menu = MainMenuUI.Create(
            backgroundSprite: _mainMenuBackground,
            font: _mainMenuFont != null ? _mainMenuFont : TMPro.TMP_Settings.defaultFontAsset,
            canContinue: canContinue,
            onContinue: () => choice = MainMenuChoice.Continue,
            onNewGame: () => choice = MainMenuChoice.NewGame,
            onOptions: () =>
            {
                optionsOpen = !optionsOpen;
                if (rebindMenu != null)
                    rebindMenu.gameObject.SetActive(optionsOpen);

                // Чтобы не было конфликтов с кликами по "слою" меню.
                _mainMenuUI?.SetButtonsInteractable(
                    canContinue: optionsOpen ? false : canContinue,
                    newGameEnabled: optionsOpen ? false : true,
                    exitEnabled: optionsOpen ? false : true,
                    optionsEnabled: true);

                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            },
            onExit: () =>
            {
                exitRequested = true;
                choice = MainMenuChoice.None;
            });

        _mainMenuUI = menu;
        menu.SetButtonsInteractable(canContinue, newGameEnabled: true, exitEnabled: true, optionsEnabled: true);

        // Ждём выбора пользователя.
        while (choice == MainMenuChoice.None && !exitRequested)
        {
            // ESC закрывает Options, если оно открыто.
            if (optionsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                optionsOpen = false;
                if (rebindMenu != null)
                    rebindMenu.gameObject.SetActive(false);

                menu.SetButtonsInteractable(
                    canContinue: canContinue,
                    newGameEnabled: true,
                    exitEnabled: true,
                    optionsEnabled: true);
            }

            yield return null;
        }

        if (rebindMenu != null)
            rebindMenu.gameObject.SetActive(false);

        if (_mainMenuUI != null)
            Destroy(_mainMenuUI.gameObject);
        _mainMenuUI = null;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (exitRequested)
        {
            // В редакторе корректнее остановить Play Mode.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
            yield break;
        }

        // Continue: загрузим сейв и сразу стартуем второй день (без интро).
        if (choice == MainMenuChoice.Continue)
        {
            yield return StartCoroutine(StartFromSavedGameDelayed());
            yield break;
        }

        // New Game: обычное вступление/интро как в текущей логике.
        GameSaveSystem.SetLoadFromSaveAtStartOverride(false);
        EnterIntro();
    }

    private MainMenuUI _mainMenuUI;
    private InputRebindMenu _inputRebindMenu;

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

    /// <summary>
    /// Передаём ссылку на меню переназначения клавиш, чтобы не искать его по сцене.
    /// </summary>
    public void SetInputRebindMenu(InputRebindMenu menu)
    {
        _inputRebindMenu = menu;
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

        bool hasOverride = GameSaveSystem.HasLoadFromSaveAtStartOverride;

        if (GameSaveSystem.LoadFromSaveAtStart)
        {
            Day1SaveData loaded = GameSaveSystem.LoadDay1();
            if (loaded != null)
            {
                _storyDirector.ApplyDay1Save(loaded);
                MarkDay1TutorialCompleted();
                _storyDirector.StartStoryFromStepId("fade_to_black_day1_end");
                if (hasOverride)
                    GameSaveSystem.ClearLoadFromSaveAtStartOverride();
                yield break;
            }
        }

        if (hasOverride)
            GameSaveSystem.ClearLoadFromSaveAtStartOverride();

        _storyDirector.StartStory();
    }

    private void Update()
    {
        if (_input == null) return;

        if (!_isTravelFading && _travelTarget != TravelTarget.None && _input.ConfirmPressed)
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
                TravelTarget target = _travelTarget;
                bool freeTeleport = _freeTeleportTargetActive;
                bool ignoreClientReq = freeTeleport && _travelTarget == TravelTarget.Client;
                PerformTravelWithFade(target, ignoreClientReq, freeTeleport, forceIgnoreSameDestination: false, () =>
                {
                    string doorKey = target == TravelTarget.Warehouse ? GameConfig.Tutorial.doorWarehouseKey : GameConfig.Tutorial.returnPressFKey;
                    MarkTutorialStepCompleted(doorKey);
                    if (target == TravelTarget.Client)
                        MarkTutorialStepCompleted(GameConfig.Tutorial.returnToClientKey);
                    _freeTeleportTargetActive = false;
                    _allowWarehouseConfirmFromClientArea = false;
                });
                return;
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
            if (_storyDirector != null && _storyDirector.IsDay2After4455LitMoveDialogueActive)
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
            if (_storyDirector != null && _storyDirector.IsDay2After4455LitWarehouseSequenceRunning)
                return false;
            bool inZoneToClient = IsPlayerInZoneTo(TravelTarget.Client);
            bool nearExitDoor = _warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance;
            if (!inZoneToClient && !nearExitDoor)
                return false;
            if (!nearExitDoor)
                return false;
            if (!IsPlayerLookingAt(_warehouseExitDoor))
                return false;
            if (DialogueManager.isConversationActive && string.Equals(DialogueManager.lastConversationStarted, "Radio_Day1_2", StringComparison.OrdinalIgnoreCase))
                return false;
            if (BlockReturnUntilPlayerDay1_2ReplicaDone)
                return false;

            if (_storyDirector != null && _storyDirector.IsRunning && !_storyDirector.IsStepAllowingTravelToClient)
            {
                // Разрешаем перемещения во время просмотра записи (после Client_Day1.5.3),
                // но не разрешаем запуск радио-события до завершения видео (см. IsRadioEventAvailable).
                bool duringWatchComputerVideo = _storyDirector.IsWaitingComputerVideo || _storyDirector.IsCurrentStepWatchComputerVideo;
                if (!duringWatchComputerVideo)
                    return false;
            }
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
                && (_storyDirector == null || !_storyDirector.IsDay2After4455LitMoveDialogueActive)
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

        // Важно:
        // Если блокировка снимается (`isLocked == false`), GameState мог уже смениться.
        // Тогда ранний `return` мешает разблокировать управление и игрок "залипает".
        if (!clientDialogue && isLocked)
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
        // Первый раз после завершения диалога: подсказка «нажми F чтобы перейти на склад».
        ShowHintOnceByKey(GameConfig.Tutorial.pressFToWarehouseAfterDialogueKey);
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

        // Гарантированно убираем портреты клиентов при завершении диалога (на случай, если conversationEnded не сработал или порядок событий глючит).
        _clientInteraction?.CloseUI();

        RemovePackageFromHands();

        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        GameStateService.SetWrongPackageDialogue(false);

        // Туториал «нажми F чтобы перейти на склад» должен появляться при первом завершении диалога.
        // Событие ClientDialogueFinished приходит гарантированно (в отличие от OnClientConversationEnded, который не подписан).
        if (_tutorialPendingAction == TutorialPendingAction.None)
            ShowHintOnceByKey(GameConfig.Tutorial.pressFToWarehouseAfterDialogueKey);
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
        // Шаг go_to_phone без завершённого звонка: сюжет не продвигаем, радио/склад не подсвечиваем (см. OnDropped — provider_call только после звонка).
        bool onPhoneTutorialStep = _storyDirector != null
            && string.Equals(_storyDirector.CurrentStepId, "go_to_phone", StringComparison.OrdinalIgnoreCase);
        if (onPhoneTutorialStep && !_providerCallDone)
        {
            ShowHintRaw(GameConfig.Tutorial.phoneHintKey);
            return;
        }

        MarkTutorialStepCompleted(GameConfig.Tutorial.phonePutKey);
        if (!_providerCallDone)
            return;
        _preferEmptyOverMeetClient = false;
        if (IsTutorialStepAlreadyShown(GameConfig.Tutorial.radioUseKey))
            return;
        ShowHintOnceByKey(GameConfig.Tutorial.radioUseKey);
    }

    public void ShowWarehousePickHint()
    {
        if (_tutorialHint == null) return;
        bool allowed = IsPackagePickAllowedByStory;
        if (!allowed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[GameFlowController] ShowWarehousePickHint blocked. " +
                $"requiredPackage={GameStateService.RequiredPackageNumber} acceptAny={_acceptAnyPackageForReturn} " +
                $"waitingClientVideo={_storyDirector != null && _storyDirector.IsWaitingComputerVideo} " +
                $"currentState={GameStateService.CurrentState} stepId='{_storyDirector?.CurrentStepId}'");
#endif
            return;
        }
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
        PlayClientArriveBell();
        ShowHintOnceByKey(GameConfig.Tutorial.meetClientKey);
        GameStateService.SetState(GameState.ClientDialog);
    }

    private void PlayClientArriveBell()
    {
        if (_clientArriveBellClip == null)
            return;
        Vector3 pos = _player != null ? _player.transform.position : (Camera.main != null ? Camera.main.transform.position : transform.position);
        AudioSource.PlayClipAtPoint(_clientArriveBellClip, pos, Mathf.Clamp01(_clientArriveBellVolume));
    }

    /// <summary> Показать подсказку по ключу (для туториала показывается спрайт по ключу в TutorialHintView). </summary>
    public void SetFlyMode(bool active)
    {
        _flyModeActive = active;
        if (active)
        {
            _tutorialHint?.Hide();
            if (_storyDirector != null) _storyDirector.enabled = false;
        }
        else
        {
            if (_storyDirector != null) _storyDirector.enabled = true;
        }
    }

    public void ShowHintRaw(string key)
    {
        if (_flyModeActive) return;
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
        if (_flyModeActive) return;
        if (_tutorialHint == null) return;
        if (_tutorialPendingAction != TutorialPendingAction.None) return;
        if (string.IsNullOrEmpty(key))
        {
            if (!HasActiveTutorial())
                _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[GameFlowController] ShowHintOnceByKey('{key}') alreadyShown={IsTutorialStepAlreadyShown(key)} displayed={_hintKeysDisplayed.Contains(key)} hasActiveTutorial={HasActiveTutorial()} pendingAction={_tutorialPendingAction} stepId='{_storyDirector?.CurrentStepId}'");
#endif

        // Шаг уже выполнен игроком — не показываем снова (кроме подсказок текущего шага сюжета).
        if (IsTutorialStepAlreadyShown(key))
        {
            if (IsCurrentStoryStepHint(key))
            {
                _hintKeysDisplayed.Add(key);
                _tutorialHint?.Show(key);
            }
            else if (!HasActiveTutorial())
            {
                _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
            }
            else
            {
                // Чтобы не оставлять "залипший" старый спрайт туториала при переходе на новый шаг.
                _tutorialHint?.Hide();
            }
            return;
        }
        // Уже показали этот шаг, ждём выполнения — не спамим (кроме подсказок текущего шага сюжета).
        if (_hintKeysDisplayed.Contains(key))
        {
            if (IsCurrentStoryStepHint(key))
                _tutorialHint?.Show(key);
            else if (!HasActiveTutorial())
                _tutorialHint?.Show(GameConfig.Tutorial.emptyKey);
            else
            {
                _tutorialHint?.Hide();
            }
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

    public void ForceTravel(TravelTarget target, bool forceIgnoreSameDestination = false)
    {
        PerformTravelWithFade(target, ignoreClientRequirements: true, freeTeleport: false, forceIgnoreSameDestination, () => { });
    }

    public void SetAllowReturnToClientWithoutExitZone(bool allow) { }

    public void SetPendingDialogueReturnPackage(int packageNumber)
    {
        _pendingDialogueReturnPackage = packageNumber;
    }

    public void SetPendingStoryCarryItemId(string itemId)
    {
        _pendingStoryCarryItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
    }

    public void SetAcceptAnyPackageForReturn(bool acceptAny)
    {
        _acceptAnyPackageForReturn = acceptAny;
    }

    public bool AcceptAnyPackageForReturn => _acceptAnyPackageForReturn;

    public bool IsPackagePickAllowedByStory
    {
        get
        {
            // Пока игрок находится на шаге просмотра видео (Client_Day1.5.3), не даём ломать сценарий
            // через pickup посылок на складе и не показываем tutorial.warehouse_pick.
            if (_storyDirector != null && (_storyDirector.IsWaitingComputerVideo || _storyDirector.IsCurrentStepWatchComputerVideo))
                return false;
            return GameStateService.RequiredPackageNumber > 0 || _acceptAnyPackageForReturn;
        }
    }

    public bool TryPerformPendingReturnToClient()
    {
        if (_pendingDialogueReturnPackage <= 0) return false;
        if (!CanLeaveWarehouseWithPendingPackage()) return false;
        PerformTravelWithFade(TravelTarget.Client, ignoreClientRequirements: true, freeTeleport: false, forceIgnoreSameDestination: false, () =>
        {
            _pendingDialogueReturnPackage = 0;
            MarkTutorialStepCompleted(GameConfig.Tutorial.returnToClientKey);
        });
        return true;
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
        // Обучение: пока не досмотрели компьютер-видео (WatchComputerVideo / Radio video intro),
        // радио-события не должны стартовать, даже если игрок уже может свободно перемещаться.
        if (_storyDirector != null && (_storyDirector.IsWaitingComputerVideo || _storyDirector.IsCurrentStepWatchComputerVideo))
            return false;
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

    /// <summary> Сначала полное затемнение, затем вызов onFadeCompleteBeforeTravel (закрытие диалога), затем телепорт на склад и fade from black. Используется при F из диалога (Client_Day1.4 / ChoseToGivePackage5577). </summary>
    public void PlayFadeToBlackThenWarehouseFromDialogue(Action onFadeCompleteBeforeTravel)
    {
        if (_travelFadeDuration <= 0f || _fadeToBlackView == null)
        {
            onFadeCompleteBeforeTravel?.Invoke();
            if (PerformTravel(TravelTarget.Warehouse, true, false, false))
                StartCoroutine(FadeFromBlackNextFrame(_travelFadeDuration, () => { }));
            return;
        }
        _warehouseTravelFromDialogueAfterFade = true;
        Vector3 soundPos = _player != null ? _player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        _gameSoundController?.PlayTravelTransition(soundPos);
        _isTravelFading = true;
        PlayFadeToBlack(_travelFadeDuration, () =>
        {
            onFadeCompleteBeforeTravel?.Invoke();
            _warehouseTravelFromDialogueAfterFade = false;
            bool ok = PerformTravel(TravelTarget.Warehouse, true, false, false);
            if (ok)
                StartCoroutine(FadeFromBlackNextFrame(_travelFadeDuration, () => { _isTravelFading = false; }));
            else
            {
                _fadeToBlackView?.Hide();
                _isTravelFading = false;
            }
        });
    }

    /// <summary> Экран затемняется → в момент полного показа (чёрный) делаем телепорт → когда спрайт снова прозрачный, мы уже на складе/в зоне. </summary>
    /// <param name="forceIgnoreSameDestination">Только для шага go_warehouse_day2_auto (после Client_Day1.1), иначе не трогать радио.</param>
    private void PerformTravelWithFade(TravelTarget target, bool ignoreClientRequirements, bool freeTeleport, bool forceIgnoreSameDestination, Action onSuccess)
    {
        if (onSuccess == null) onSuccess = () => { };
        bool useFade = _travelFadeDuration > 0f && _fadeToBlackView != null
            && (target == TravelTarget.Warehouse || target == TravelTarget.Client);
        Vector3 soundPos = _player != null ? _player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        _gameSoundController?.PlayTravelTransition(soundPos);
        if (!useFade)
        {
            if (PerformTravel(target, ignoreClientRequirements, freeTeleport, forceIgnoreSameDestination))
                onSuccess();
            return;
        }
        _isTravelFading = true;
        PlayFadeToBlack(_travelFadeDuration, () =>
        {
            // Когда экран полностью чёрный — переносим
            bool ok = PerformTravel(target, ignoreClientRequirements, freeTeleport, forceIgnoreSameDestination);
            if (ok)
            {
                StartCoroutine(FadeFromBlackNextFrame(_travelFadeDuration, onSuccess));
            }
            else
            {
                _fadeToBlackView.Hide();
                _isTravelFading = false;
            }
        });
    }

    private IEnumerator FadeFromBlackNextFrame(float duration, Action onSuccess)
    {
        yield return null; // один кадр уже отрендерены в новой локации
        _fadeToBlackView.PlayFadeFromBlack(duration, () =>
        {
            _isTravelFading = false;
            onSuccess();
        });
    }

    private bool PerformTravel(TravelTarget target, bool ignoreClientRequirements, bool freeTeleport = false, bool forceIgnoreSameDestination = false)
    {
        // Не телепортировать в ту же зону (forceIgnoreSameDestination только для go_warehouse_day2_auto, чтобы не ломать радио)
        if (!forceIgnoreSameDestination && _lastTeleportDestination != TravelTarget.None && _lastTeleportDestination == target)
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

            bool blockDeliveryDuringClientVideo = _storyDirector != null && (_storyDirector.IsWaitingComputerVideo || _storyDirector.IsCurrentStepWatchComputerVideo);
            if (blockDeliveryDuringClientVideo)
            {
                // Сбрасываем активную задачу посылок, чтобы pickup и туториал не происходили раньше сценария.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[GameFlowController] Enter Warehouse during client video. Clearing delivery. state='{GameStateService.CurrentState}' required={GameStateService.RequiredPackageNumber} stepId='{_storyDirector?.CurrentStepId}'");
#endif
                if (_delivery != null)
                {
                    _delivery.ClearTask();
                }
                GameStateService.SetRequiredPackage(0, enforceOnly: false);
            }
            else if (!freeTeleport && !_tutorialWarehouseVisit && _delivery != null && !_delivery.HasActiveTask)
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
            // Закрываем UI клиента при прилёте на склад — иначе портреты могут остаться на экране (например при delayCloseForWarehouseFade).
            _clientInteraction?.CloseUI();
            // Всегда до подписчиков OnTeleportedToWarehouse: иначе StoryDirector может выйти по раннему return
            EnterClientDialogueState(false, movePlayerToClient: false);
            _controller?.SetBlock(false);
            OnTeleportedToWarehouse?.Invoke();
            _clientInteraction?.ResetClientDialogFlagsForWarehouse();
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
            _pendingStoryCarryItemId = null;
            Transform point = _clientPoint;
            if (_useFreeTeleportPointForNextClientTravel && _freeTeleportToClientPoint != null)
            {
                point = _freeTeleportToClientPoint;
                _useFreeTeleportPointForNextClientTravel = false;
            }
            else if (freeTeleport && _freeTeleportToClientPoint != null)
                point = _freeTeleportToClientPoint;
            // Сюжет вызывает SetTravelTarget(Client) без useFreeTeleportPointForClient → _freeTeleportTargetActive=false.
            // Игрок жмёт F у двери: freeTeleport остаётся false → раньше попадали в TeleportFromSklad вместо FreeTeleportToPVZ.
            // Сюжетные телепорты (ForceTravel / pending return) идут с ignoreClientRequirements=true — сюжетная точка не трогается.
            else if (!ignoreClientRequirements
                     && GameStateService.CurrentState == GameState.Warehouse
                     && _freeTeleportToClientPoint != null)
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
        if (BlockReturnUntilPlayerDay1_2ReplicaDone)
            return "ждётся реплика Player_Day1_2_Replica (до конца диалога на радио)";
        if (_storyDirector != null && _storyDirector.IsDay2After4455LitWarehouseSequenceRunning)
            return "нужно дождаться окончания сюжетной сцены на складе";
        if (_storyDirector != null && _storyDirector.IsRunning && !_storyDirector.IsStepAllowingTravelToClient)
            return "сценарий не разрешает возврат к клиенту (текущий шаг: " + (_storyDirector.CurrentStepId ?? "?") + ")";
        if (!string.IsNullOrEmpty(_pendingStoryCarryItemId) && !CanLeaveWarehouseWithPendingStoryCarryItem())
            return "нужно принести специальный предмет: " + _pendingStoryCarryItemId;
        if (GameStateService.RequiredPackageNumber > 0 && !CanLeaveWarehouseToClient())
            return "нужна посылка в руках (требуется №" + GameStateService.RequiredPackageNumber + ")";
        return null;
    }

    private bool CanLeaveWarehouseToClient()
    {
        bool inExitZone = IsPlayerInZoneTo(TravelTarget.Client) || (_warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance);
        if (!inExitZone)
            return false;

        if (!string.IsNullOrEmpty(_pendingStoryCarryItemId))
            return CanLeaveWarehouseWithPendingStoryCarryItem();

        // После Client_Day1.5.2/1.5.3 на шаге просмотра видео можно уйти на склад и вернуться обратно без посылки.
        // Иначе игрок застревает на складе, если до этого остался RequiredPackageNumber > 0.
        if (_storyDirector != null && string.Equals(_storyDirector.CurrentStepId, "watch_computer_indoor_day1_5", StringComparison.OrdinalIgnoreCase))
            return true;

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

    private bool CanLeaveWarehouseWithPendingStoryCarryItem()
    {
        if (string.IsNullOrEmpty(_pendingStoryCarryItemId))
            return true;

        bool exitInside = IsPlayerInZoneTo(TravelTarget.Client) || (_warehouseExitDoor != null && _player != null && Vector3.Distance(_player.transform.position, _warehouseExitDoor.position) <= _doorTeleportMaxDistance);
        if (!exitInside)
            return false;

        PlayerHands hands = HandsRegistry.Hands;
        if (hands == null || hands.Current is not MonoBehaviour mb)
            return false;

        StoryCarryItem marker = mb.GetComponent<StoryCarryItem>();
        return marker != null && string.Equals(marker.ItemId, _pendingStoryCarryItemId, StringComparison.OrdinalIgnoreCase);
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