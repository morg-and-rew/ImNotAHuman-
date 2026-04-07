using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using static IGameFlowController;
using TutorialPendingAction = IGameFlowController.TutorialPendingAction;

public sealed class StoryDirector : MonoBehaviour
{
    private const float KnockAfterFreeRoamDelaySeconds = 10f;
    /// <summary> Первый диалог дня 2 у стойки; после старта — свечи на складе не зажигаются и без подсказки/обводки. </summary>
    public const string Day21ClientConversationTitle = "Client_day2.1";
    private const string Day2CandlesLitConversation = "Client_day2.1.3_candles_lit";
    private const string Day2CandlesUnlitConversation = "Client_day2.1.3_candles_unlit";
    private const string Day2CandlesUnlitAfterVideoConversation = "Client_day2.1.4_after_video_unlit";
    private const string Day2CandlesLitReturnConversation = "Client_day2.1.4_after_warehouse_lit";
    private const string Day2After4455LitFollowupConversation = "Client_day2.2_after_4455_lit_followup";
    private const string Day2After4455LitWarehouseConversation = "Client_day2.2_warehouse_lit_auto";
    private const string Day2After4455LitWarehouseConversationGiveExtra = "Client_day2.2_warehouse_lit_auto_if_gave_5577";
    private const string Day2After4455LitReturnDialogueTools = "Client_day2.2_after_tools_if_gave_5577";
    private const string Day2After4455LitReturnDialogue5577 = "Client_day2.2_after_5577_if_not_gave";
    private const string Day2After4455LitMoveDialogueTools = "Client_day2.2_move_dialogue_after_tools";
    private const string Day2After4455LitMoveDialogue5577 = "Client_day2.2_move_dialogue_after_5577";
    private const string Day2After4455LitDelayedClientConversation = "Client_day2.2_after_60s_meet";
    private const string Day2CandlesLitAfter4455Conversation = "Client_day2.2_candles_lit_after_4455";
    private const string Day2After60sMeetWarehouseAutoConversation = "Client_day2.2_warehouse_after_60s_auto";
    private const string Day2After60sMeetAfterVideoConversation = "Client_day2.2_after_60s_after_video";
    /// <summary> Lua-флаг в Client_day2.2_after_60s_after_video: true на ветке «Позвонить в скорую» (см. userScript у узлов в DialogueDatabase). </summary>
    private const string Day2After60sAfterVideoLuaCallAmbulance = "Day2After60s_CallAmbulance";
    private const string Day2After60sMeetAfterAmbulanceConversation = "Client_day2.2_after_60s_after_ambulance";
    private const string Day2After60sMeetEmergencyCallConversation = "Phone_CallEmergency_911";
    /// <summary> После after_video / after_ambulance — первый финальный диалог, затем радио (только помехи), затем <see cref="Day2EndConversationAfterRadioStatic"/>. </summary>
    private const string Day2EndConversationBeforeRadio = "Client_day2.2_end";
    /// <summary> После взаимодействия с радио — второй финальный диалог, затем затемнение и конец игры. </summary>
    private const string Day2EndConversationAfterRadioStatic = "Client_day2.2_end2";
    private const string Day2ToolsCarryItemId = "day2_tools_box";
    private const float Day2After4455LitNextClientDelaySeconds = 60f;
    private const float Day2AfterDay212CandlesApproachDelaySeconds = 30f;
    private const string Day2FreeRoamBeforeOrder8877StepId = "day2_free_roam_before_order_8877";
    private const float Day1AfterClient14WarehouseImpactDelaySeconds = 10f;

    [SerializeField] private AttitudeChoiceRecorder _attitudeRecorder;
    [SerializeField] private PhoneUnlockDirector _phoneUnlock;
    [SerializeField] private ClientInteraction _client;
    [SerializeField] private AudioSource _knockAudioSource;
    [SerializeField] private Computer _computer;
    [SerializeField] private CustomDialogueUI _customDialogueUIRef;
    [SerializeField] private AudioClip _day2After60sMeetWarehouseImpactClip;
    [SerializeField, Range(0f, 1f)] private float _day2After60sMeetWarehouseImpactVolume = 0.85f;
    [SerializeField] private PhoneItemView _phoneItemView;
    [SerializeField] private AudioClip _day2After60sMeetSlapClip;
    [SerializeField, Range(0f, 1f)] private float _day2After60sMeetSlapVolume = 0.9f;
    [SerializeField] private AudioClip _day2After60sMeetEnergyClip;
    [SerializeField, Range(0f, 1f)] private float _day2After60sMeetEnergyVolume = 0.9f;
    [SerializeField] private AudioClip _day1AfterClient14WarehouseImpactClip;
    [SerializeField, Range(0f, 1f)] private float _day1AfterClient14WarehouseImpactVolume = 0.85f;
    [SerializeField] private RadioInteractable _radioInteractable;

    private List<Step> _steps = new List<Step>();
    private int _index = -1;
    private DeliveryNoteView _deliveryNoteView;
    private IGameFlowController _flow;
    private IPlayerInput _input;
    private IPlayerBlocker _controller;
    private bool _pendingRemovePackageAfterDialogue;
    private Step _currentStep;
    private bool _clientDay14HandledByConversationEnded;
    private bool _clientDay21Started;

    private enum WaitMode { Idle, WaitingDialogueEnd, WaitingWarehouseConfirm, WaitingReturnToClientArea, WaitingClientConfirm, WaitingClientReturnForDialogue, WaitingRadioComplete, WaitingTrigger, WaitingFreeRoamClientConfirm, WaitingKnockThenWarehouse, WaitingClientPortraitOnlySpace, WaitingComputerVideo, WaitingFadeToBlack, WaitingTeleportToWarehouse, WaitingTeleportToClient }
    private WaitMode _wait = WaitMode.Idle;
    private string _pendingDialogueAfterReturn;
    private Coroutine _knockDelayCoroutine;
    private string _waitingRadioStyleConversation;
    private bool _warehousePickHintShown;
    private CustomDialogueUI _customDialogueUI;
    private string _pendingClientApproachConversation;
    private bool _day2LitWarehouseDetourActive;
    private string _pendingDialogueAfterComputerVideo;
    private bool _day2After4455LitGoToWarehousePending;
    private Coroutine _day2After4455LitWarehouseSequence;
    private bool _day2After4455LitAwaitingNextClientDelay;
    private Coroutine _day2After4455LitNextClientDelayCoroutine;
    private bool _day2After60sMeetGoToWarehousePending;
    private bool _day2After60sMeetPlayVideoOnClientReturn;
    private int _day2After60sMeetLastEntryId = -1;
    private bool _day212AfterClient212ApproachReady;
    private Coroutine _day212AfterClient212Routine;
    private bool _day214AfterCandlesApproachReady = true;
    private Coroutine _day214AfterCandlesRoutine;
    private bool _day22AfterCandlesLit4455ApproachReady = true;
    private Coroutine _day22AfterCandlesLit4455Routine;
    private bool _day2After60sMeetSlapSoundPlayed;
    private bool _day2After60sMeetEnergySoundPlayed;
    private bool _day2After60sMeetEmergencyCallRunning;
    private bool _day2After60sMeetRestorePoseValid;
    private Vector3 _day2After60sMeetRestorePos;
    private Quaternion _day2After60sMeetRestoreRot = Quaternion.identity;
    private Quaternion _day2After60sMeetRestoreCamRot = Quaternion.identity;
    private bool _isSubscribedToSubtitleShown;
    private bool _day2EndFlowStarted;
    private bool _day2EndWaitingForRadioInteract;
    private bool _day2EndRadioInteracted;
    private Coroutine _day2EndFlowCoroutine;
    private Coroutine _day1AfterClient14ImpactSoundCoroutine;
    public string CurrentStepId => (_index >= 0 && _index < _steps.Count) ? _steps[_index].stepId : "";
    public bool HasStoryStarted => _index >= 0;
    /// <summary> True после того как был запущен диалог <see cref="Day21ClientConversationTitle"/> в этой сессии сюжета. </summary>
    public bool IsClientDay21Started => _clientDay21Started;
    public bool IsRunning => _index >= 0 && _index < _steps.Count && _wait != WaitMode.Idle;
    public bool IsWaitingForRadioComplete => _wait == WaitMode.WaitingRadioComplete;
    public bool IsWaitingComputerVideo => _wait == WaitMode.WaitingComputerVideo;
    public bool IsCurrentStepWatchComputerVideo => _currentStep != null && _currentStep.type == StepType.WatchComputerVideo;
    public bool IsCurrentStepGoToRadio => _currentStep != null && _currentStep.type == StepType.GoToRadio;
    /// <summary> True, если текущий шаг — интро «go_to_radio»: пришли на склад после того как положили телефон (go_to_phone → go_to_warehouse_for_radio → go_to_radio). Иначе не показывать tutorial.radio_use. </summary>
    public bool IsIntroGoToRadioStep => IsCurrentStepGoToRadio
        && _index >= 2
        && string.Equals(_steps[_index - 1].stepId, "go_to_warehouse_for_radio", StringComparison.OrdinalIgnoreCase)
        && string.Equals(_steps[_index - 2].stepId, "go_to_phone", StringComparison.OrdinalIgnoreCase);
    public bool IsWaitingForWarehouseStoryZoneExit => false;
    /// <summary> True, если сюжет ждёт подтверждения перехода на склад (например после Client_Day1.4 с ChoseToGivePackage5577). </summary>
    public bool IsWaitingForWarehouseConfirm => _wait == WaitMode.WaitingWarehouseConfirm;
    /// <summary> Автодиалоги и настройка ветки «свечи / 4455 / инструменты или 5577» на складе — до появления подсказки «вернись к клиенту». </summary>
    public bool IsDay2After4455LitWarehouseSequenceRunning => _day2After4455LitWarehouseSequence != null;
    public bool IsDay2After4455LitMoveDialogueActive =>
        DialogueManager.isConversationActive
        && (string.Equals(DialogueManager.lastConversationStarted, Day2After4455LitMoveDialogueTools, StringComparison.OrdinalIgnoreCase)
            || string.Equals(DialogueManager.lastConversationStarted, Day2After4455LitMoveDialogue5577, StringComparison.OrdinalIgnoreCase));
    /// <summary> После Client_day2.1.2: первые 30 с без диалога у стойки; затем звонок и можно подойти к клиенту (свечи). </summary>
    public bool IsDay212CandlesClientApproachCooldownActive => IsDay212CandlesApproachStillCoolingDown();
    /// <summary> После Client_day2.1.4_* (свечи): 30 с, звонок, затем Client_day2.2_order_8877 по E. </summary>
    public bool IsDay214AfterCandlesOrder8877CooldownActive => IsDay214Order8877ApproachStillCoolingDown();
    /// <summary> После Client_day2.2_candles_lit_after_4455: 30 с, звонок, затем followup / after_60s по E. </summary>
    public bool IsDay22AfterCandlesLit4455PendingCooldownActive => IsDay22AfterCandlesLit4455PendingApproachStillCoolingDown();
    public bool IsWaitingForClientInteraction =>
        _wait == WaitMode.WaitingFreeRoamClientConfirm
        || _wait == WaitMode.WaitingClientConfirm
        || (_currentStep != null && _currentStep.optional);

    public bool IsStepAllowingTravelToWarehouse =>
        IsAtOrPastStep("free_roam_before_clients")
        || (_currentStep != null && (_currentStep.type == StepType.GoToDoorWarehouse || _currentStep.type == StepType.GoWarehouse || _currentStep.type == StepType.GoWarehouseWaitReturn || _currentStep.type == StepType.GoToRadio));

    public bool IsStepAllowingTravelToClient =>
        IsAtOrPastStep("free_roam_before_clients")
        || (_currentStep != null && (_currentStep.type == StepType.ReturnFromWarehouse || _currentStep.type == StepType.ReturnToClient))
        || _wait == WaitMode.WaitingRadioComplete
        || (_currentStep != null && _currentStep.type == StepType.GoWarehouse && _wait == WaitMode.WaitingClientConfirm)
        || (_currentStep != null && _currentStep.type == StepType.GoToRadio);

    public bool DoesCurrentStepRequirePackageForReturn =>
        _currentStep != null && (_currentStep.type == StepType.ReturnToClient || _currentStep.type == StepType.GoWarehouseWaitReturn);

    public bool IsExpectingTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return false;
        return _currentStep != null && _wait == WaitMode.WaitingTrigger
            && string.Equals(_currentStep.triggerId, triggerId, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool IsAtOrPastStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId)) return true;
        if (_steps == null || _steps.Count == 0 || _index < 0) return true;
        int targetIndex = -1;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (string.Equals(_steps[i].stepId, stepId, StringComparison.OrdinalIgnoreCase))
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0) return false;
        return _index >= targetIndex;
    }

    public void Initialize(IGameFlowController flow, IPlayerInput input, IPlayerBlocker controller, DeliveryNoteView deliveryNoteView)
    {
        _flow = flow;
        _input = input;
        _controller = controller;
        _deliveryNoteView = deliveryNoteView;

        _customDialogueUI = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;
        EnsureCustomDialogueUISubscription();
        RadioInteractable.OnAnyRadioInteracted += OnAnyRadioInteracted;

        _steps = BuildStepsFromConfig();
        if (_steps.Count == 0)
            return;

        if (_client != null)
            _client.ClientDialogueStepCompleted += OnDialogueCompleted;
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded += OnDialogueSystemConversationEnded;
        _flow.OnTeleportedToWarehouse += OnTeleportedToWarehouse;
        _flow.OnTeleportedToClient += OnTeleportedToClient;
        if (_flow is GameFlowController gfc)
        {
            gfc.OnRadioStoryCompleted += OnRadioStoryCompleted;
            gfc.OnTriggerFired += OnTriggerFired;
            gfc.OnComputerVideoEnded += OnComputerVideoEnded;
        }
    }

    private static List<Step> BuildStepsFromConfig()
    {
        List<Step> list = new List<Step>();
        IReadOnlyList<StoryStepData> data = GameConfig.StorySteps;
        for (int i = 0; i < data.Count; i++)
        {
            StoryStepData d = data[i];
            if (d == null) continue;
            if (!Enum.TryParse(d.stepType, true, out StepType t))
            {
                continue;
            }
            List<string> ids = (d.activateRadioEventIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            list.Add(new Step
            {
                stepId = d.stepId ?? "",
                type = t,
                conversationTitle = d.conversationTitle ?? "",
                hintText = d.hintText ?? "",
                triggerId = d.triggerId ?? "",
                optional = d.optional,
                autoTravel = d.autoTravel,
                removePackageAfterDialogue = d.removePackageAfterDialogue,
                showDeliveryNote = d.showDeliveryNote,
                deliveryNoteNumber = d.deliveryNoteNumber,
                deliveryNoteLuaCondition = d.deliveryNoteLuaCondition ?? "",
                skipIfLuaConditionFalse = d.skipIfLuaConditionFalse ?? "",
                hideDeliveryNote = d.hideDeliveryNote,
                expireRadioOnEnter = d.expireRadioOnEnter,
                activateRadioEventIds = ids,
                radioStaticVolume = d.radioStaticVolume,
                radioStaticVolumeWhenEnter = d.radioStaticVolumeWhenEnter,
                showRadioHintOnEnter = d.showRadioHintOnEnter,
                computerVideoKind = d.computerVideoKind ?? "",
                fadeToBlackDuration = d.fadeToBlackDuration
            });
        }
        return list;
    }

    private void OnDestroy()
    {
        if (_knockDelayCoroutine != null)
            StopCoroutine(_knockDelayCoroutine);
        if (_client != null) _client.ClientDialogueStepCompleted -= OnDialogueCompleted;
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnDialogueSystemConversationEnded;
        if (_day2After4455LitWarehouseSequence != null)
            StopCoroutine(_day2After4455LitWarehouseSequence);
        if (_day2After4455LitNextClientDelayCoroutine != null)
            StopCoroutine(_day2After4455LitNextClientDelayCoroutine);
        if (_day2EndFlowCoroutine != null)
            StopCoroutine(_day2EndFlowCoroutine);
        _day2EndFlowCoroutine = null;
        if (_day1AfterClient14ImpactSoundCoroutine != null)
            StopCoroutine(_day1AfterClient14ImpactSoundCoroutine);
        if (_day212AfterClient212Routine != null)
            StopCoroutine(_day212AfterClient212Routine);
        if (_day214AfterCandlesRoutine != null)
            StopCoroutine(_day214AfterCandlesRoutine);
        if (_day22AfterCandlesLit4455Routine != null)
            StopCoroutine(_day22AfterCandlesLit4455Routine);
        RadioInteractable.OnAnyRadioInteracted -= OnAnyRadioInteracted;
        UnsubscribeCustomDialogueUI();
        _flow.OnTeleportedToWarehouse -= OnTeleportedToWarehouse;
        _flow.OnTeleportedToClient -= OnTeleportedToClient;
        if (_flow is GameFlowController gfc)
        {
            gfc.OnRadioStoryCompleted -= OnRadioStoryCompleted;
            gfc.OnTriggerFired -= OnTriggerFired;
        }
    }

    private void StartKnockThenWarehouseFlow()
    {
        // В течение 10 сек посылка не нужна (в т.ч. если зайдём на склад).
        if (_flow is GameFlowController gfc)
            gfc.SetRequiredPackageForReturn(0);

        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        GameStateService.SetState(GameState.None);
        _wait = WaitMode.WaitingKnockThenWarehouse;
        _knockDelayCoroutine = StartCoroutine(KnockAfterDelayThenAdvance());
    }

    private System.Collections.IEnumerator KnockAfterDelayThenAdvance()
    {
        yield return new WaitForSeconds(KnockAfterFreeRoamDelaySeconds);
        if (_knockAudioSource != null && _knockAudioSource.clip != null)
            _knockAudioSource.PlayOneShot(_knockAudioSource.clip);
        _knockDelayCoroutine = null;
        _wait = WaitMode.Idle;
        Advance();
    }

    public void StartStory()
    {
        _index = -1;
        _clientDay21Started = false;
        _wait = WaitMode.Idle;
        Advance();
    }

    public void NotifyClientDay21StartedIfNeeded(string conversationTitle)
    {
        if (string.IsNullOrEmpty(conversationTitle)) return;
        if (!string.Equals(conversationTitle, Day21ClientConversationTitle, StringComparison.OrdinalIgnoreCase)) return;
        _clientDay21Started = true;
    }

    /// <summary> True, если сюжет ещё не запущен и можно стартовать с шага после go_to_radio (игрок уже прослушал Radio_Tutorial). </summary>
    public bool CanStartFromAfterRadio()
    {
        if (_steps == null || _steps.Count == 0) return false;
        if (_index >= 0) return false;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (string.Equals(_steps[i].stepId, "go_to_radio", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary> Запуск сюжета с шага после go_to_radio (free_roam_before_clients). Вызывать только если игрок уже прослушал Radio_Tutorial. </summary>
    public void StartStoryFromAfterRadio()
    {
        int goToRadioIndex = -1;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (string.Equals(_steps[i].stepId, "go_to_radio", StringComparison.OrdinalIgnoreCase))
            {
                goToRadioIndex = i;
                break;
            }
        }
        if (goToRadioIndex < 0)
        {
            StartStory();
            return;
        }
        _index = goToRadioIndex;
        _wait = WaitMode.Idle;
        Advance();
    }

    /// <summary> Запуск сюжета сразу с первого диалога с клиентом (intro / Client_Day1). Radio_Tutorial и Radio_Day1_2 необязательны. </summary>
    public void StartStoryFromClientDialogue()
    {
        if (_steps == null || _steps.Count == 0) return;
        int introIndex = -1;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (string.Equals(_steps[i].stepId, "intro", StringComparison.OrdinalIgnoreCase))
            {
                introIndex = i;
                break;
            }
        }
        if (introIndex < 0)
        {
            StartStoryFromAfterRadio();
            return;
        }
        _index = introIndex - 1;
        _wait = WaitMode.Idle;
        Advance();
    }

    /// <summary> День 2: игрок выбрал «сразу к клиентам» — переходим к day2_after_radio и сразу запускаем Client_day2.1 (игрок уже у клиента). </summary>
    public void AdvanceFromDay2StartToClient()
    {
        if (!string.Equals(CurrentStepId, "day2_start", StringComparison.OrdinalIgnoreCase)) return;
        _wait = WaitMode.Idle;
        Advance();
        // Игрок уже подошёл к клиенту и нажал E — сразу запускаем диалог, без второго нажатия
        if (_currentStep != null && string.Equals(_currentStep.stepId, "day2_after_radio", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_currentStep.conversationTitle))
        {
            _pendingRemovePackageAfterDialogue = _currentStep.removePackageAfterDialogue;
            _controller?.SetBlock(true);
            GameStateService.SetState(GameState.ClientDialog);
            ((GameFlowController)_flow).EnterClientDialogueState(true);
            _wait = WaitMode.WaitingDialogueEnd;
            _client?.StartClientDialogWithSpecificStep("", _currentStep.conversationTitle);
        }
    }

    public void Tick()
    {
        EnsureCustomDialogueUISubscription();
        if (_input == null) return;

        if (_wait == WaitMode.WaitingTrigger && _currentStep != null && _input.NextPressed)
        {
            if (_currentStep.type == StepType.PressSpace)
            {
                _flow?.NotifyTutorialActionCompleted(TutorialPendingAction.PressSpace);
                _wait = WaitMode.Idle;
                Advance();
                return;
            }
            if (_currentStep.type == StepType.GoToDoorWarehouse || _currentStep.type == StepType.ReturnFromWarehouse)
            {
                _wait = WaitMode.Idle;
                Advance();
                return;
            }
        }

        // Если в руках предмет (телефон) — E сначала должен убрать его, а не запускать диалог с клиентом
        if (_currentStep != null && _currentStep.optional && _client != null && _client.IsPlayerLookingAtClient(_flow.Player) && _input.InteractPressed
            && (HandsRegistry.Hands == null || !HandsRegistry.Hands.HasItem))
        {
            if (GameFlowController.IsRadioDay21StoryConversationPlaying())
                return;
            _wait = WaitMode.Idle;
            Advance();
            Advance();
            return;
        }

        if (_wait == WaitMode.WaitingFreeRoamClientConfirm && _client != null && _client.IsPlayerLookingAtClient(_flow.Player) && _input.InteractPressed
            && (HandsRegistry.Hands == null || !HandsRegistry.Hands.HasItem))
        {
            if (GameFlowController.IsRadioDay21StoryConversationPlaying())
                return;
            if (!string.IsNullOrEmpty(_pendingClientApproachConversation))
            {
                if (IsDay212CandlesApproachStillCoolingDown())
                    return;
                if (IsDay22AfterCandlesLit4455PendingApproachStillCoolingDown())
                    return;
                string nextConversation = _pendingClientApproachConversation;
                _pendingClientApproachConversation = null;
                _pendingRemovePackageAfterDialogue = false;
                _controller?.SetBlock(true);
                GameStateService.SetState(GameState.ClientDialog);
                ((GameFlowController)_flow).EnterClientDialogueState(true);
                _wait = WaitMode.WaitingDialogueEnd;
                _client.StartClientDialogWithSpecificStep("", nextConversation);
                return;
            }

            // День 2: после радио ждём, пока игрок сам подойдёт к клиенту и нажмёт E — тогда запускаем Client_day2.1
            if (_currentStep != null && !string.IsNullOrEmpty(_currentStep.conversationTitle))
            {
                if (IsDay214Order8877ApproachStillCoolingDown())
                    return;
                _pendingRemovePackageAfterDialogue = _currentStep.removePackageAfterDialogue;
                _controller?.SetBlock(true);
                GameStateService.SetState(GameState.ClientDialog);
                ((GameFlowController)_flow).EnterClientDialogueState(true);
                _wait = WaitMode.WaitingDialogueEnd;
                _client.StartClientDialogWithSpecificStep("", _currentStep.conversationTitle);
                return;
            }
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (!_input.ConfirmPressed) return;

        if (_wait == WaitMode.WaitingClientReturnForDialogue && _flow != null && _flow.TryPerformPendingReturnToClient())
            return;

        if (_wait == WaitMode.WaitingReturnToClientArea && _client != null && _client.IsPlayerInside)
        {
            _wait = WaitMode.Idle;
            Advance();
            return;
        }
    }

    private void OnRadioStoryCompleted()
    {
        if (_wait == WaitMode.WaitingRadioComplete)
        {
            _wait = WaitMode.Idle;
            Advance();
            GameStateService.SetState(GameState.Warehouse);
            return;
        }
        if (_currentStep != null && _currentStep.type == StepType.ActivateRadioEvent)
        {
            Advance();
        }
    }

    private bool IsDay212CandlesApproachStillCoolingDown()
    {
        if (_wait != WaitMode.WaitingFreeRoamClientConfirm || string.IsNullOrEmpty(_pendingClientApproachConversation))
            return false;
        if (!string.Equals(_pendingClientApproachConversation, Day2CandlesLitConversation, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_pendingClientApproachConversation, Day2CandlesUnlitConversation, StringComparison.OrdinalIgnoreCase))
            return false;
        return !_day212AfterClient212ApproachReady;
    }

    private IEnumerator CoDay212AfterClient212DelayThenBell()
    {
        yield return new WaitForSeconds(Day2AfterDay212CandlesApproachDelaySeconds);
        if (_flow is GameFlowController gfc)
            gfc.PlayClientCounterBellTwoDimensional();
        _day212AfterClient212ApproachReady = true;
        _day212AfterClient212Routine = null;
    }

    private bool IsDay214Order8877ApproachStillCoolingDown()
    {
        if (_wait != WaitMode.WaitingFreeRoamClientConfirm || _currentStep == null)
            return false;
        if (!string.Equals(_currentStep.stepId, Day2FreeRoamBeforeOrder8877StepId, StringComparison.OrdinalIgnoreCase))
            return false;
        return !_day214AfterCandlesApproachReady;
    }

    private void StartDay214AfterCandlesDelayThenBell()
    {
        _day214AfterCandlesApproachReady = false;
        if (_day214AfterCandlesRoutine != null)
            StopCoroutine(_day214AfterCandlesRoutine);
        _day214AfterCandlesRoutine = StartCoroutine(CoDay214AfterCandlesDelayThenBell());
    }

    private IEnumerator CoDay214AfterCandlesDelayThenBell()
    {
        yield return new WaitForSeconds(Day2AfterDay212CandlesApproachDelaySeconds);
        if (_flow is GameFlowController gfc)
            gfc.PlayClientCounterBellTwoDimensional();
        _day214AfterCandlesApproachReady = true;
        _day214AfterCandlesRoutine = null;
    }

    private bool IsDay22AfterCandlesLit4455PendingApproachStillCoolingDown()
    {
        if (_wait != WaitMode.WaitingFreeRoamClientConfirm || string.IsNullOrEmpty(_pendingClientApproachConversation))
            return false;
        if (!string.Equals(_pendingClientApproachConversation, Day2After4455LitFollowupConversation, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_pendingClientApproachConversation, Day2After4455LitDelayedClientConversation, StringComparison.OrdinalIgnoreCase))
            return false;
        return !_day22AfterCandlesLit4455ApproachReady;
    }

    private void StartDay22AfterCandlesLit4455DelayThenBell()
    {
        _day22AfterCandlesLit4455ApproachReady = false;
        if (_day22AfterCandlesLit4455Routine != null)
            StopCoroutine(_day22AfterCandlesLit4455Routine);
        _day22AfterCandlesLit4455Routine = StartCoroutine(CoDay22AfterCandlesLit4455DelayThenBell());
    }

    private IEnumerator CoDay22AfterCandlesLit4455DelayThenBell()
    {
        yield return new WaitForSeconds(Day2AfterDay212CandlesApproachDelaySeconds);
        if (_flow is GameFlowController gfc)
            gfc.PlayClientCounterBellTwoDimensional();
        _day22AfterCandlesLit4455ApproachReady = true;
        _day22AfterCandlesLit4455Routine = null;
    }

    private void OnTriggerFired(string triggerId)
    {
        if (_wait != WaitMode.WaitingTrigger) return;
        if (_currentStep == null || string.IsNullOrEmpty(_currentStep.triggerId)) return;
        if (!string.Equals(_currentStep.triggerId, triggerId, System.StringComparison.OrdinalIgnoreCase)) return;
        _wait = WaitMode.Idle;
        Advance();
    }

    private void Advance()
    {
        _index++;
        if (_index >= _steps.Count)
        {
            _wait = WaitMode.Idle;
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        _currentStep = _steps[_index];

        // Обновляем Lua-флаги для шагов с skipIfLuaConditionFalse.
        // Используется для разветвления по состоянию свечей.
        bool candlesLit = CandleInteractable.IsAnyCandleLit;
        DialogueLua.SetVariable("CandlesLit", candlesLit);
        DialogueLua.SetVariable("CandlesUnlit", !candlesLit);

        if (!string.IsNullOrEmpty(_currentStep.skipIfLuaConditionFalse))
        {
            bool condition = DialogueLua.GetVariable(_currentStep.skipIfLuaConditionFalse).AsBool;
            if (!condition)
            {
                Advance();
                return;
            }
        }

        ShowHintForStep(_currentStep);
        ApplyDirectives(_currentStep);

        switch (_currentStep.type)
        {
            case StepType.None:
                if (string.Equals(_currentStep.stepId, "free_roam_after_day1_4", StringComparison.OrdinalIgnoreCase))
                    StartKnockThenWarehouseFlow();
                else
                    FreeRoamNone(_currentStep);
                break;
            case StepType.PressSpace: PressSpace(_currentStep); break;
            case StepType.GoToDoorWarehouse: GoToDoorWarehouse(_currentStep); break;
            case StepType.ReturnFromWarehouse: ReturnFromWarehouse(_currentStep); break;
            case StepType.GoToRouter: GoToRouter(_currentStep); break;
            case StepType.GoToPhone: GoToPhone(_currentStep); break;
            case StepType.Dialogue: StartDialogue(_currentStep); break;
            case StepType.GoToRadio: GoToRadio(_currentStep); break;
            case StepType.GoWarehouse: GoWarehouse(_currentStep); break;
            case StepType.GoWarehouseWaitReturn: GoWarehouseWaitReturn(_currentStep); break;
            case StepType.ReturnToClient: ReturnToClient(_currentStep); break;
            case StepType.WatchComputerVideo: WatchComputerVideo(_currentStep); break;
            case StepType.DialogueRadioStyle: StartDialogueRadioStyle(_currentStep); break;
            case StepType.ActivateRadioEvent: ActivateRadioEventAndGoIdle(_currentStep); break;
            case StepType.FadeToBlack: StartFadeToBlack(_currentStep); break;
        }
    }

    private void ShowHintForStep(Step step)
    {
        if (step.type == StepType.None)
        {
            if (string.Equals(step.stepId, "day2_after_radio", StringComparison.OrdinalIgnoreCase))
            {
                _flow?.ShowHintOnceByKey(GameConfig.Tutorial.emptyKey);
                return;
            }
            _flow?.ShowMeetClientHintOnce();
            return;
        }

        string key = null;
        switch (step.type)
        {
            case StepType.PressSpace:
                key = GameConfig.Tutorial.pressSpaceKey;
                break;
            case StepType.GoToDoorWarehouse:
                if (!string.Equals(step.stepId, "go_to_warehouse_for_radio", StringComparison.OrdinalIgnoreCase))
                    key = GameConfig.Tutorial.doorWarehouseKey;
                break;
            case StepType.ReturnFromWarehouse:
                key = GameConfig.Tutorial.returnPressFKey;
                break;
            case StepType.GoToRouter:
                key = GameConfig.Tutorial.routerHintKey;
                break;
            case StepType.GoToPhone:
                key = GameConfig.Tutorial.phoneHintKey;
                break;
            case StepType.GoToRadio:
                // tutorial.radio_use только для интро-шага "go_to_radio" (после того как положили телефон); не показывать при переходе с go_to_warehouse_for_radio (Radio_Day1_2).
                if (string.Equals(step.stepId, "go_to_radio", StringComparison.OrdinalIgnoreCase) && IsIntroGoToRadioStep)
                    key = GameConfig.Tutorial.radioUseKey;
                break;
            case StepType.GoWarehouseWaitReturn:
                key = GameConfig.Tutorial.warehouseReturnKey;
                break;
        }
        if (!string.IsNullOrEmpty(key))
            _flow?.ShowHintOnceByKey(key);
    }

    private void ApplyDirectives(Step step)
    {
        if (step.expireRadioOnEnter) _flow?.ExpireAllRadioAvailable();
        if (step?.activateRadioEventIds != null)
        {
            float? vol = step.radioStaticVolume > 0f ? (float?)step.radioStaticVolume : null;
            foreach (string id in step.activateRadioEventIds)
            {
                if (!string.IsNullOrEmpty(id))
                    _flow?.ActivateRadioEvent(id, vol);
            }
        }
        if (step.radioStaticVolumeWhenEnter > 0f)
            _flow?.SetRadioStaticVolume(step.radioStaticVolumeWhenEnter);
        // Показывать «подойдите к радио» только после того как положил телефон (IsIntroGoToRadioStep). Для Radio_Day1_2 не показывать.
        bool skipRadioHint = string.Equals(step.stepId, "go_to_radio", StringComparison.OrdinalIgnoreCase) && !IsIntroGoToRadioStep;
        if (step.showRadioHintOnEnter && !skipRadioHint) _flow?.ShowRadioHintOnce();
    }

    private void PressSpace(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingTrigger;
    }

    private void FreeRoamNone(Step step)
    {
        if (string.Equals(step.stepId, "free_roam_after_day1_4", StringComparison.OrdinalIgnoreCase) && _flow is GameFlowController gfc)
            gfc.SetRequiredPackageForReturn(0);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        // day2_after_radio: игрок пришёл с радио (остаётся на складе) — не сбрасывать состояние в None, иначе зоны решат, что цель «склад», и F телепортирует на склад повторно
        if (!string.Equals(step.stepId, "day2_after_radio", StringComparison.OrdinalIgnoreCase))
            GameStateService.SetState(GameState.None);
        _wait = WaitMode.WaitingFreeRoamClientConfirm;
    }

    private void GoToDoorWarehouse(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingTeleportToWarehouse;
        _flow?.SetTutorialWarehouseVisit(true);
        bool silent = string.Equals(step.stepId, "go_to_warehouse_for_radio", StringComparison.OrdinalIgnoreCase);
        string hintKey = silent ? "" : (GameConfig.Tutorial.doorWarehouseKey ?? "");
        _flow?.SetTravelTarget(TravelTarget.Warehouse, hintKey);
    }

    private void ReturnFromWarehouse(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingTeleportToClient;
        string hintKey = GameConfig.Tutorial.returnPressFKey ?? "";
        _flow?.SetTravelTarget(TravelTarget.Client, hintKey, useFreeTeleportPointForClient: true);
    }

    private void GoToRouter(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingTrigger;
    }

    private void GoToPhone(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingTrigger;
    }

    private void GoToRadio(Step step)
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingRadioComplete;
        if (step.optional && _flow is GameFlowController gfc)
            gfc.SetRequiredPackageForReturn(0);
    }

    private void StartDialogue(Step step)
    {
        _pendingRemovePackageAfterDialogue = step.removePackageAfterDialogue;
        _controller?.SetBlock(true);
        GameStateService.SetState(GameState.ClientDialog);
        ((GameFlowController)_flow).EnterClientDialogueState(true);
        _wait = WaitMode.WaitingDialogueEnd;
        if (_client != null)
            _client.StartClientDialogWithSpecificStep("", step.conversationTitle);
    }

    private void GoWarehouseWaitReturn(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingReturnToClientArea;
    }

    private void GoWarehouse(Step step)
    {
        if (string.Equals(step.stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            if (_flow is GameFlowController gfcDay15)
                gfcDay15.SetRequiredPackageForReturn(0);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _pendingDialogueAfterReturn = "Client_Day1.5";
            if (GameStateService.CurrentState == GameState.Warehouse)
            {
                _wait = WaitMode.WaitingClientConfirm;
                string returnHintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
                    ? (GameConfig.Tutorial.emptyKey ?? "")
                    : (GameConfig.Tutorial.returnToClientKey ?? "");
                _flow?.SetTravelTarget(TravelTarget.Client, returnHintKey);
            }
            else
            {
                _wait = WaitMode.WaitingWarehouseConfirm;
            }
            return;
        }

        if (step.deliveryNoteNumber > 0 && _flow is GameFlowController gfc)
            gfc.SetFixedPackageForNextWarehouse(step.deliveryNoteNumber);

        if (step.autoTravel)
        {
            if (string.Equals(step.stepId, "go_warehouse_after_day1_5", StringComparison.OrdinalIgnoreCase) && _flow is GameFlowController gfcAuto)
            {
                gfcAuto.SetRequiredPackageForReturn(0);
                gfcAuto.SetTutorialWarehouseVisit(true);
            }
            _wait = WaitMode.WaitingWarehouseConfirm;
            // Только для шага после Client_Day1.1 — принудительный телепорт даже если считаем что уже на складе; остальные шаги (радио и т.д.) не трогаем.
            bool forceIgnoreSameDestination = string.Equals(step.stepId, "go_warehouse_day2_auto", StringComparison.OrdinalIgnoreCase);
            _flow?.ForceTravel(TravelTarget.Warehouse, forceIgnoreSameDestination);
            return;
        }

        _wait = WaitMode.WaitingWarehouseConfirm;
        string warehouseHintKey = GameConfig.Tutorial.goWarehouseKey ?? "";
        _flow?.SetTravelTarget(TravelTarget.Warehouse, warehouseHintKey);
    }

    private void ReturnToClient(Step step)
    {
        if (_flow is GameFlowController gfc)
        {
            if (step.deliveryNoteNumber > 0)
                gfc.SetRequiredPackageForReturn(step.deliveryNoteNumber);
            else if (string.Equals(step.stepId, "return_to_client_day1_5", StringComparison.OrdinalIgnoreCase))
            {
                if (GameStateService.RequiredPackageNumber <= 0)
                    gfc.SetAcceptAnyPackageForReturn(true);
            }
        }

        if (step.autoTravel)
        {
            _wait = WaitMode.WaitingClientConfirm;
            _flow?.ForceTravel(TravelTarget.Client);
            return;
        }

        _wait = WaitMode.WaitingClientConfirm;
        string hintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
            ? (GameConfig.Tutorial.emptyKey ?? "")
            : (GameConfig.Tutorial.returnToClientKey ?? "");
        _flow?.SetTravelTarget(TravelTarget.Client, hintKey);
    }

    private void WatchComputerVideo(Step step)
    {
        _wait = WaitMode.WaitingComputerVideo;
        string kind = string.IsNullOrEmpty(step.computerVideoKind) ? "indoor" : step.computerVideoKind;
        _computer?.SetAllowedVideoKind(kind);
    }

    private void OnComputerVideoEnded()
    {
        if (_wait != WaitMode.WaitingComputerVideo) return;
        _wait = WaitMode.Idle;
        _computer?.SetAllowedVideoKind(null);
        if (!string.IsNullOrEmpty(_pendingDialogueAfterComputerVideo))
        {
            string nextConversation = _pendingDialogueAfterComputerVideo;
            _pendingDialogueAfterComputerVideo = null;
            _wait = WaitMode.WaitingDialogueEnd;
            _controller?.SetBlock(true);
            GameStateService.SetState(GameState.ClientDialog);
            StartCoroutine(ShowClientDialogueNextFrame(nextConversation, lockMovement: true));
            return;
        }
        Advance();
    }

    private void StartDialogueRadioStyle(Step step)
    {
        if (string.IsNullOrEmpty(step.conversationTitle))
        {
            Advance();
            return;
        }
        _wait = WaitMode.WaitingDialogueEnd;
        _waitingRadioStyleConversation = step.conversationTitle;
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded += OnDialogueRadioStyleEnded;
        DialogueManager.StartConversation(step.conversationTitle);
    }

    private void OnDialogueRadioStyleEnded(Transform _)
    {
        if (string.IsNullOrEmpty(_waitingRadioStyleConversation)) return;
        string endedConversation = _waitingRadioStyleConversation;
        _waitingRadioStyleConversation = null;
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnDialogueRadioStyleEnded;
        // После Client_Day1.6 посылка дня уже отыграна: снимаем задачу склада, иначе остаётся номер с go_warehouse_after_day1_5
        // и игрок снова может подбирать коробки до конца дня.
        if (string.Equals(endedConversation, "Client_Day1.6", System.StringComparison.OrdinalIgnoreCase)
            && _flow is GameFlowController gfcAfterDay16)
        {
            gfcAfterDay16.SetFixedPackageForNextWarehouse(0);
            gfcAfterDay16.SetRequiredPackageForReturn(0);
        }
        _wait = WaitMode.Idle;
        Advance();
    }

    private void ActivateRadioEventAndGoIdle(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        GameStateService.SetState(GameState.None);
        _wait = WaitMode.Idle;
    }

    private void StartFadeToBlack(Step step)
    {
        _controller?.SetBlock(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _flow?.HideHint();
        _wait = WaitMode.WaitingFadeToBlack;
        float duration = step.fadeToBlackDuration > 0f ? step.fadeToBlackDuration : 3f;
        _flow?.PlayFadeToBlack(duration, OnFadeToBlackComplete);
    }

    private void OnFadeToBlackComplete()
    {
        if (_currentStep != null && string.Equals(_currentStep.stepId, "fade_to_black_day1_end", StringComparison.OrdinalIgnoreCase))
        {
            TrySaveDay1Progress();
            if (_flow is GameFlowController gfc)
            {
                gfc.PlayDay2Intro(OnDay2IntroComplete);
                return;
            }
        }
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnDay2IntroComplete()
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.Idle;
        if (_flow is GameFlowController gfc)
        {
            gfc.MarkDay1TutorialCompleted();
            gfc.RunAfterDay2IntroUnlockRoutine(Advance);
        }
        else
            Advance();
    }

    private void TrySaveDay1Progress()
    {
        bool choseToGive = DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool;
        bool gotPhoneNumber = _phoneUnlock != null && _phoneUnlock.HasSpawnedNote;
        string savedPhoneNumber = _phoneUnlock != null ? _phoneUnlock.GetSavedPhoneNumber() : "";

        int neutral = 0, mystical = 0, skeptical = 0;
        if (_attitudeRecorder != null && _attitudeRecorder.Stats != null)
        {
            neutral = _attitudeRecorder.Stats.NeutralCount;
            mystical = _attitudeRecorder.Stats.MysticalCount;
            skeptical = _attitudeRecorder.Stats.SkepticalCount;
        }

        var data = new Day1SaveData
        {
            ChoseToGivePackage5577 = choseToGive,
            GotPhoneNumberFromGuy = gotPhoneNumber,
            SavedPhoneNumber = savedPhoneNumber ?? "",
            NeutralChoicesCount = neutral,
            MysticalChoicesCount = mystical,
            SkepticalChoicesCount = skeptical,
            Packages = PackageRegistry.Instance != null ? PackageRegistry.Instance.CaptureSaveEntries() : null
        };
        GameSaveSystem.SaveDay1(data);
    }

    /// <summary>Восстановить состояние после загрузки сохранения (Lua, аттитюды, телефон). Вызывать до старта сюжета.</summary>
    public void ApplyDay1Save(Day1SaveData data)
    {
        if (data == null) return;

        DialogueLua.SetVariable("ChoseToGivePackage5577", data.ChoseToGivePackage5577);
        DialogueLua.SetVariable("RunWarehouse5577Steps", false);

        if (_attitudeRecorder != null && _attitudeRecorder.Stats != null)
            _attitudeRecorder.Stats.SetCounts(data.NeutralChoicesCount, data.MysticalChoicesCount, data.SkepticalChoicesCount);

        if (data.GotPhoneNumberFromGuy)
        {
            GameStateService.UnlockPhone();
            _phoneUnlock?.SpawnNoteFromSave(data.SavedPhoneNumber ?? "");
        }

        if (data.Packages != null && data.Packages.Count > 0 && PackageRegistry.Instance != null)
            PackageRegistry.Instance.RestoreFromSaveEntries(data.Packages);
    }

    /// <summary>Запустить сюжет с указанного шага — выполнить этот шаг (для загрузки: отыграть fade_to_black_day1_end и интро 2-го дня).</summary>
    public void StartStoryFromStepId(string stepId)
    {
        if (_steps == null || _steps.Count == 0 || string.IsNullOrEmpty(stepId)) return;
        int idx = -1;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (string.Equals(_steps[i].stepId, stepId, StringComparison.OrdinalIgnoreCase))
            {
                idx = i;
                break;
            }
        }
        if (idx < 0) return;
        _index = idx - 1;
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnDialogueCompleted(ClientDialogueStepCompletionData data)
    {
        string conv = data.ConversationTitle ?? "";
        bool isClientDay14 = string.Equals(conv, "Client_Day1.4", StringComparison.OrdinalIgnoreCase);
        bool isClientDay152 = string.Equals(conv, "Client_Day1.5.2", StringComparison.OrdinalIgnoreCase);
        bool isClientDay153 = string.Equals(conv, "Client_Day1.5.3", StringComparison.OrdinalIgnoreCase);
        bool isDay2After60sAfterVideo = string.Equals(conv, Day2After60sMeetAfterVideoConversation, StringComparison.OrdinalIgnoreCase);
        bool isDay2After60sAfterAmbulance = string.Equals(conv, Day2After60sMeetAfterAmbulanceConversation, StringComparison.OrdinalIgnoreCase);
        if (_wait != WaitMode.WaitingDialogueEnd
            && !isClientDay14
            && !isClientDay152
            && !isClientDay153
            && !isDay2After60sAfterVideo
            && !isDay2After60sAfterAmbulance)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[StoryDirector] OnDialogueCompleted conv='{conv}' wait={_wait} stepId='{_currentStep?.stepId}'");
#endif

        _attitudeRecorder?.RecordFromLua();
        _phoneUnlock?.TryUnlockFromDialogue();
        if (_pendingRemovePackageAfterDialogue) { _pendingRemovePackageAfterDialogue = false; _flow?.RemovePackageFromHands(); }
        if (_currentStep?.hideDeliveryNote == true && _deliveryNoteView != null) _deliveryNoteView.Hide();

        if (string.Equals(conv, "Client_Day1.4.1", StringComparison.OrdinalIgnoreCase))
        {
            _flow?.RemovePackageFromHands();
            if (DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool)
                ScheduleDay1AfterClient14WarehouseImpactSound();
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (string.Equals(conv, "Client_Day1.5", StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (string.Equals(conv, "Client_Day1.5.2", StringComparison.OrdinalIgnoreCase))
        {
            _flow?.RemovePackageFromHands();
            // После Client_Day1.5.2 автоматически запускаем Client_Day1.5.3.
            // В режиме обучения нельзя “срезать” скрипт: сначала должен быть просмотр записи, затем радио.
            _wait = WaitMode.WaitingDialogueEnd;
            _controller?.SetBlock(true);
            GameStateService.SetState(GameState.ClientDialog);
            StartCoroutine(ShowClientDialogueNextFrame("Client_Day1.5.3", lockMovement: true));
            return;
        }

        if (string.Equals(conv, "Client_Day1.5.3", StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            // После окончания Client_Day1.5.3 показываем туториал tutorial.watch_video
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[StoryDirector] Client_Day1.5.3 completed -> show watch_video. " +
                $"currentStep='{_currentStep?.stepId}', wait='{_wait}', " +
                $"state='{GameStateService.CurrentState}', requiredPackage={GameStateService.RequiredPackageNumber}, " +
                $"acceptAny={_flow != null && _flow.AcceptAnyPackageForReturn}");
#endif
            _flow?.ShowHintOnceByKey(GameConfig.Tutorial?.watchVideoKey ?? "tutorial.watch_video");
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (string.Equals(conv, "Client_day2.1.2", StringComparison.OrdinalIgnoreCase))
        {
            GameSoundController.Instance?.StartWindLoop();

            // После 5574-ветки на шаге return_to_client_day2_1 в GameState остаётся RequiredPackageNumber;
            // в свободном роуме до свечей склад не должен требовать посылку и должен отпускать к клиенту без коробки.
            if (_flow is GameFlowController gfcDay212)
            {
                gfcDay212.SetFixedPackageForNextWarehouse(0);
                gfcDay212.SetPendingDialogueReturnPackage(0);
                gfcDay212.SetRequiredPackageForReturn(0);
            }

            string nextConversation = CandleInteractable.IsAnyCandleLit
                ? Day2CandlesLitConversation
                : Day2CandlesUnlitConversation;

            // Ветка дня 2: 30 с и звонок — затем можно подойти к клиенту и начать диалог про свечи.
            _pendingClientApproachConversation = nextConversation;
            _day212AfterClient212ApproachReady = false;
            if (_day212AfterClient212Routine != null)
                StopCoroutine(_day212AfterClient212Routine);
            _day212AfterClient212Routine = StartCoroutine(CoDay212AfterClient212DelayThenBell());
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.WaitingFreeRoamClientConfirm;
            return;
        }

        if (string.Equals(conv, Day2CandlesLitConversation, StringComparison.OrdinalIgnoreCase))
        {
            // Ветка "свечи зажжены": после диалога едем на склад без задания, затем возвращаемся к клиенту без посылки.
            _day2LitWarehouseDetourActive = true;
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.WaitingWarehouseConfirm;
            _flow?.ForceTravel(TravelTarget.Warehouse);
            return;
        }

        if (string.Equals(conv, Day2CandlesUnlitConversation, StringComparison.OrdinalIgnoreCase))
        {
            // Ветка "свечи не зажжены": второй диалог запускается сразу после видео, без ручных действий игрока.
            Vector3 savedPlayerPosition = Vector3.zero;
            Quaternion savedPlayerRotation = Quaternion.identity;
            Quaternion savedCameraRotation = Quaternion.identity;
            PlayerView savedPlayer = null;
            bool hasSavedPose = false;
            if (_flow is GameFlowController gfcPose && gfcPose.Player != null && gfcPose.Player.PlayerCamera != null)
            {
                hasSavedPose = true;
                savedPlayer = gfcPose.Player;
                savedPlayerPosition = savedPlayer.transform.position;
                savedPlayerRotation = savedPlayer.transform.rotation;
                savedCameraRotation = savedPlayer.PlayerCamera.transform.rotation;
            }
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            if (hasSavedPose && savedPlayer != null && savedPlayer.PlayerCamera != null)
            {
                savedPlayer.TeleportTo(savedPlayerPosition, savedPlayerRotation);
                savedPlayer.PlayerCamera.transform.rotation = savedCameraRotation;
                savedPlayer.SyncRotationFromCamera();
                StartCoroutine(RestorePlayerPoseNextFrame(savedPlayerPosition, savedPlayerRotation, savedCameraRotation));
            }
            _pendingDialogueAfterComputerVideo = Day2CandlesUnlitAfterVideoConversation;
            _wait = WaitMode.WaitingComputerVideo;
            _computer?.SetAllowedVideoKind(Computer.KindIndoor);
            bool started = _computer != null && _computer.TryPlayAllowedVideoImmediately();
            if (!started)
            {
                _pendingDialogueAfterComputerVideo = null;
                _wait = WaitMode.WaitingDialogueEnd;
                _controller?.SetBlock(true);
                GameStateService.SetState(GameState.ClientDialog);
                StartCoroutine(ShowClientDialogueNextFrame(Day2CandlesUnlitAfterVideoConversation, lockMovement: true));
            }
            return;
        }

        if (string.Equals(conv, Day2CandlesUnlitAfterVideoConversation, StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.Idle;
            Advance();
            StartDay214AfterCandlesDelayThenBell();
            return;
        }

        if (string.Equals(conv, Day2CandlesLitReturnConversation, StringComparison.OrdinalIgnoreCase))
        {
            // После дополнительного диалога в lit-ветке возвращаем игрока в свободный режим.
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.Idle;
            Advance();
            StartDay214AfterCandlesDelayThenBell();
            return;
        }

        if (string.Equals(conv, Day2After4455LitReturnDialogueTools, StringComparison.OrdinalIgnoreCase)
            || string.Equals(conv, Day2After4455LitReturnDialogue5577, StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.Idle;
            if (string.Equals(conv, Day2After4455LitReturnDialogueTools, StringComparison.OrdinalIgnoreCase))
                TryDestroyHeldStoryToolsBox();
            else
            {
                // Override-диалог: ClientDialogueFinished не вызывается — убираем посылку и сбрасываем задачу 5577,
                // иначе на складе остаётся RequiredPackage и блокируется выход к клиенту.
                _flow?.RemovePackageFromHands();
                if (_flow is GameFlowController gfcAfter5577)
                {
                    gfcAfter5577.SetPendingDialogueReturnPackage(0);
                    gfcAfter5577.SetRequiredPackageForReturn(0);
                }
            }
            _day2After4455LitAwaitingNextClientDelay = true;
            // После инструментов — move_dialogue_after_5577; после посылки 5577 — move_dialogue_after_tools (на ходу).
            string moveDialogue = string.Equals(conv, Day2After4455LitReturnDialogueTools, StringComparison.OrdinalIgnoreCase)
                ? Day2After4455LitMoveDialogue5577
                : Day2After4455LitMoveDialogueTools;
            StartCoroutine(StartDialogueNextFrame(moveDialogue)); // Диалог идет на фоне свободного передвижения.
            return;
        }

        if (string.Equals(conv, Day2CandlesLitAfter4455Conversation, StringComparison.OrdinalIgnoreCase))
        {
            // После return_to_client_day2_order_4455 в состоянии может остаться RequiredPackageNumber — сбрасываем перед свободным роумом.
            if (_flow is GameFlowController gfcCandlesLit4455)
            {
                gfcCandlesLit4455.SetFixedPackageForNextWarehouse(0);
                gfcCandlesLit4455.SetPendingDialogueReturnPackage(0);
                gfcCandlesLit4455.SetRequiredPackageForReturn(0);
            }
            // Развилка после 4455:
            // - свечи зажжены -> при следующем E — Client_day2.2_after_4455_lit_followup, затем игрок сам идёт на склад (без авто-телепорта);
            // - свечи не зажжены -> после 30 с и звонка — E у стойки → Client_day2.2_after_60s_meet.
            _pendingClientApproachConversation = CandleInteractable.IsAnyCandleLit
                ? Day2After4455LitFollowupConversation
                : Day2After4455LitDelayedClientConversation;
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.WaitingFreeRoamClientConfirm;
            StartDay22AfterCandlesLit4455DelayThenBell();
            return;
        }

        if (string.Equals(conv, Day2After4455LitFollowupConversation, StringComparison.OrdinalIgnoreCase))
        {
            _day2After4455LitGoToWarehousePending = true;
            if (_flow is GameFlowController gfcPrep)
            {
                // Жестко очищаем возможные хвосты прошлых шагов (в т.ч. 5577), чтобы на складе не появлялась записка.
                gfcPrep.SetFixedPackageForNextWarehouse(0);
                gfcPrep.SetPendingDialogueReturnPackage(0);
                gfcPrep.SetPendingStoryCarryItemId(null); // toolbox: только после складских автодиалогов, если отдавали 5577 в день 1.
                gfcPrep.SetRequiredPackageForReturn(0);
            }
            _flow?.SetTutorialWarehouseVisit(true); // Отключаем генерацию складской записки/задачи на этом переходе.
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.WaitingWarehouseConfirm;
            string whKey = GameConfig.Tutorial.goWarehouseKey ?? "";
            _flow?.SetTravelTarget(TravelTarget.Warehouse, whKey);
            return;
        }

        if (string.Equals(conv, Day2After4455LitDelayedClientConversation, StringComparison.OrdinalIgnoreCase))
        {
            _day2After60sMeetGoToWarehousePending = true;
            if (_flow is GameFlowController gfcPrep60s)
            {
                // Переход на склад должен открыть обычную рандомную задачу без хвостов прошлых шагов.
                gfcPrep60s.SetFixedPackageForNextWarehouse(0);
                gfcPrep60s.SetPendingDialogueReturnPackage(0);
                gfcPrep60s.SetPendingStoryCarryItemId(null);
                gfcPrep60s.SetRequiredPackageForReturn(0);
            }
            _wait = WaitMode.WaitingWarehouseConfirm;
            _flow?.ForceTravel(TravelTarget.Warehouse);
            return;
        }
        if (string.Equals(conv, Day2After60sMeetAfterVideoConversation, StringComparison.OrdinalIgnoreCase))
        {
            // Ветку «Позвонить в скорую» нельзя надёжно определить по последнему entry id: субтитры игрока
            // (showPCSubtitlesDuringLine=0) и длинный граф не обновляют _day2After60sMeetLastEntryId до id=3.
            // Флаг выставляется Lua userScript в DialogueDatabase на узлах выбора (см. Day2After60s_CallAmbulance).
            bool isCallAmbulanceBranch = DialogueLua.GetVariable(Day2After60sAfterVideoLuaCallAmbulance).AsBool;
            DialogueLua.SetVariable(Day2After60sAfterVideoLuaCallAmbulance, false);
            if (isCallAmbulanceBranch)
            {
                StartDay2After60sMeetEmergencyCall();
                return;
            }
            ClearHandsAndDeliveryStateAfterDay2After60sAfterVideoNonEmergency();
            StartDay2EndFlow();
            return;
        }
        if (string.Equals(conv, Day2After60sMeetAfterAmbulanceConversation, StringComparison.OrdinalIgnoreCase))
        {
            // Override-диалог: ClientDialogueFinished не вызывается — убираем посылку и сбрасываем задачу доставки.
            ClearHandsAndDeliveryStateAfterDay2After60sAfterVideoNonEmergency();
            StartDay2EndFlow();
            return;
        }

        if (string.Equals(conv, "Client_Day1.2", StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (string.Equals(conv, "Client_Day1.4", StringComparison.OrdinalIgnoreCase))
        {
            if (_clientDay14HandledByConversationEnded)
            {
                _clientDay14HandledByConversationEnded = false;
                return;
            }
            HandleClientDay14Completed();
            return;
        }

        GameStateService.SetState(GameState.None);
        ((GameFlowController)_flow).EnterClientDialogueState(false);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnDialogueSystemConversationEnded(Transform actor)
    {
        if (DialogueManager.instance == null) return;
        string lastConv = DialogueManager.lastConversationStarted ?? "";
        bool isDay2MoveDialogue =
            string.Equals(lastConv, Day2After4455LitMoveDialogueTools, StringComparison.OrdinalIgnoreCase)
            || string.Equals(lastConv, Day2After4455LitMoveDialogue5577, StringComparison.OrdinalIgnoreCase);
        if (isDay2MoveDialogue)
        {
            TryDestroyHeldStoryToolsBox();
            if (_day2After4455LitAwaitingNextClientDelay && _day2After4455LitNextClientDelayCoroutine == null)
                _day2After4455LitNextClientDelayCoroutine = StartCoroutine(Day2After4455LitDelayThenAdvance());
            return;
        }
        if (_day2After60sMeetEmergencyCallRunning
            && string.Equals(lastConv, Day2After60sMeetEmergencyCallConversation, StringComparison.OrdinalIgnoreCase))
        {
            _day2After60sMeetEmergencyCallRunning = false;
            RestoreAfterDay2After60sMeetEmergencyCall();
            return;
        }
        if (!string.Equals(lastConv, "Client_Day1.4", StringComparison.OrdinalIgnoreCase)) return;
        _clientDay14HandledByConversationEnded = true;
        HandleClientDay14Completed();
    }

    private IEnumerator Day2After4455LitDelayThenAdvance()
    {
        yield return new WaitForSeconds(Day2After4455LitNextClientDelaySeconds);
        _day2After4455LitNextClientDelayCoroutine = null;
        if (!_day2After4455LitAwaitingNextClientDelay)
            yield break;
        _day2After4455LitAwaitingNextClientDelay = false;
        // Как и после Client_day2.2_candles_lit_after_4455 (свечи не зажжены): 30 с + звонок, затем E у стойки → Client_day2.2_after_60s_meet.
        _pendingClientApproachConversation = Day2After4455LitDelayedClientConversation;
        GameStateService.SetState(GameState.None);
        ((GameFlowController)_flow).EnterClientDialogueState(false);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingFreeRoamClientConfirm;
        StartDay22AfterCandlesLit4455DelayThenBell();
    }

    private static void TryDestroyHeldStoryToolsBox()
    {
        PlayerHands hands = HandsRegistry.Hands;
        if (hands?.Current is not StoryCarryItem carry) return;
        if (!string.Equals(carry.ItemId, Day2ToolsCarryItemId, StringComparison.OrdinalIgnoreCase)) return;
        hands.DestroyCurrentItem();
    }

    private void HandleClientDay14Completed()
    {
        DialogueLua.SetVariable("RunWarehouse5577Steps", false);
        bool choseToGive = DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool;
        // Звук падения: через 10 с после Client_Day1.4 (без отдачи) или после Client_Day1.4.1 (с отдачей).
        if (!choseToGive)
        {
            ScheduleDay1AfterClient14WarehouseImpactSound();
            _client?.CloseUI();
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            bool wasInStoryFlow = _wait == WaitMode.WaitingDialogueEnd;
            _wait = WaitMode.Idle;
            if (wasInStoryFlow) Advance();
            return;
        }
        // ChoseToGivePackage5577 == true: экран сначала полностью гаснет (fade), только потом телепорт на склад. UI и разблокировка — после телепорта в OnTeleportedToWarehouse.
        _pendingDialogueAfterReturn = "Client_Day1.4.1";
        _wait = WaitMode.WaitingWarehouseConfirm;
        if (_flow is GameFlowController gfc)
        {
            gfc.SetFixedPackageForNextWarehouse(5577);
            // Телепорт выполнит GameFlowController после полного затемнения (PlayFadeToBlackThenWarehouseFromDialogue), не вызываем ForceTravel здесь.
            if (!gfc.IsWarehouseTravelFromDialogueAfterFade)
                _flow.ForceTravel(TravelTarget.Warehouse);
        }
    }

    private void OnTeleportedToWarehouse()
    {
        void EnsureControlsAfterWarehouseTeleport()
        {
            if (_flow is GameFlowController gfc)
                gfc.EnterClientDialogueState(false, movePlayerToClient: false);
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (_wait == WaitMode.WaitingTeleportToWarehouse)
        {
            EnsureControlsAfterWarehouseTeleport();
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (_day2LitWarehouseDetourActive)
        {
            EnsureControlsAfterWarehouseTeleport();
            if (_flow is GameFlowController gfcDetour)
                gfcDetour.SetRequiredPackageForReturn(0); // Без записки и без активной задачи на складе.
            _wait = WaitMode.WaitingClientConfirm;
            string hintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
                ? (GameConfig.Tutorial.emptyKey ?? "")
                : (GameConfig.Tutorial.returnToClientKey ?? "");
            _flow?.SetTravelTarget(TravelTarget.Client, hintKey);
            return;
        }
        if (_day2After4455LitGoToWarehousePending)
        {
            _day2After4455LitGoToWarehousePending = false;
            EnsureControlsAfterWarehouseTeleport();
            _controller?.SetBlock(false);
            GameStateService.SetState(GameState.None);
            if (_flow is GameFlowController gfcWarehouseDialog)
                gfcWarehouseDialog.EnterClientDialogueState(false, movePlayerToClient: false);
            if (_day2After4455LitWarehouseSequence != null)
                StopCoroutine(_day2After4455LitWarehouseSequence);
            bool gave5577InDay1 = DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool;
            _day2After4455LitWarehouseSequence = StartCoroutine(RunDay2After4455LitWarehouseSequence(gave5577InDay1));
            return;
        }
        if (_day2After60sMeetGoToWarehousePending)
        {
            _day2After60sMeetGoToWarehousePending = false;
            EnsureControlsAfterWarehouseTeleport();
            _controller?.SetBlock(false);
            GameStateService.SetState(GameState.None);
            PlayDay2After60sMeetWarehouseImpactSound();
            if (_flow is GameFlowController gfcWarehouseDialog60s)
                gfcWarehouseDialog60s.EnterClientDialogueState(false, movePlayerToClient: false);
            if (_day2After4455LitWarehouseSequence != null)
                StopCoroutine(_day2After4455LitWarehouseSequence);
            _day2After4455LitWarehouseSequence = StartCoroutine(RunDay2After60sMeetWarehouseSequence());
            return;
        }
        if (_wait == WaitMode.WaitingRadioComplete)
        {
            EnsureControlsAfterWarehouseTeleport();
            // Радио приоритетно: подсказка остаётся «иди к радио / нажми E», пока игрок не нажал E у радио
            string hintKey = GameConfig.Tutorial.radioUseKey ?? "";
            _flow?.SetTravelTarget(TravelTarget.Client, hintKey, useFreeTeleportPointForClient: true);
            return;
        }
        if (_wait != WaitMode.WaitingWarehouseConfirm)
        {
            // Рассинхрон (другой шаг успел сменить _wait до колбэка fade) — не оставляем игрока без управления.
            EnsureControlsAfterWarehouseTeleport();
            return;
        }

        // После телепорта на склад (в т.ч. из диалога Client_Day1.4) — разблокируем и сбрасываем состояние диалога.
        if (_flow is GameFlowController gfcUnlock)
            gfcUnlock.EnterClientDialogueState(false, movePlayerToClient: false);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_currentStep != null && string.Equals(_currentStep.stepId, "free_roam_after_day1_4", StringComparison.OrdinalIgnoreCase) && _flow is GameFlowController gfcFree)
        {
            gfcFree.SetRequiredPackageForReturn(0);
        }

        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            if (_flow is GameFlowController gfcDay15)
                gfcDay15.SetRequiredPackageForReturn(0);
            _pendingDialogueAfterReturn = "Client_Day1.5";
            _wait = WaitMode.WaitingClientConfirm;
            string hintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
                ? (GameConfig.Tutorial.emptyKey ?? "")
                : (GameConfig.Tutorial.returnToClientKey ?? "");
            _flow?.SetTravelTarget(TravelTarget.Client, hintKey);
            return;
        }

        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_after_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            _wait = WaitMode.Idle;
            GameStateService.SetState(GameState.Warehouse);
            Advance();
            var gfc = _flow as GameFlowController;
            gfc?.StartRandomDeliveryTaskAndSetRequiredForReturn();
            gfc?.RefreshWarehouseDeliveryNote();
            if (!_warehousePickHintShown)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[StoryDirector] TeleportedToWarehouse -> step go_warehouse_after_day1_5. " +
                    $"showWarehousePickHint now. currentStep='{_currentStep?.stepId}', " +
                    $"state='{GameStateService.CurrentState}', requiredPackage={GameStateService.RequiredPackageNumber}, " +
                    $"acceptAny={(gfc != null ? gfc.AcceptAnyPackageForReturn : false)}, " +
                    $"waitModeAfterTeleport='{_wait}'");
#endif
                _warehousePickHintShown = true;
                gfc?.ShowWarehousePickHint();
            }
            return;
        }

        // После Client_Day1.1 → авто-переход на склад: разблокировка, запись о посылке и подсказка.
        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_day2_auto", StringComparison.OrdinalIgnoreCase))
        {
            _wait = WaitMode.Idle;
            GameStateService.SetState(GameState.Warehouse);
            Advance();
            var gfc = _flow as GameFlowController;
            gfc?.StartRandomDeliveryTaskAndSetRequiredForReturn();
            gfc?.RefreshWarehouseDeliveryNote();
            if (!_warehousePickHintShown)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[StoryDirector] TeleportedToWarehouse -> step go_warehouse_day2_auto. " +
                    $"showWarehousePickHint now. state='{GameStateService.CurrentState}', " +
                    $"requiredPackage={GameStateService.RequiredPackageNumber}, acceptAny={(gfc != null && gfc.AcceptAnyPackageForReturn)}");
#endif
                _warehousePickHintShown = true;
                gfc?.ShowWarehousePickHint();
            }
            return;
        }

        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_day2_1", StringComparison.OrdinalIgnoreCase))
        {
            _wait = WaitMode.Idle;
            Advance();
            var gfc = _flow as GameFlowController;
            gfc?.RefreshWarehouseDeliveryNote();
            return;
        }

        if (!string.IsNullOrEmpty(_pendingDialogueAfterReturn))
        {
            _wait = WaitMode.WaitingClientReturnForDialogue;
            if (_flow is GameFlowController gfc)
            {
                gfc.SetRequiredPackageForReturn(5577);
                gfc.SetPendingDialogueReturnPackage(5577);
            }
            string hintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
                ? (GameConfig.Tutorial.emptyKey ?? "")
                : (GameConfig.Tutorial.returnToClientKey ?? "");
            _flow?.SetTravelTarget(TravelTarget.Client, hintKey);
            return;
        }
        _wait = WaitMode.Idle;
        if (_flow is GameFlowController gfcDefault)
            gfcDefault.RefreshWarehouseDeliveryNote();
        Advance();
    }


    private void OnTeleportedToClient()
    {
        if (_wait == WaitMode.WaitingRadioComplete)
        {
            GameStateService.SetState(GameState.None);
            return;
        }
        if (_wait == WaitMode.WaitingTeleportToClient)
        {
            _wait = WaitMode.Idle;
            Advance();
            return;
        }
        bool pendingReturnDialogue = _wait == WaitMode.WaitingClientReturnForDialogue || _wait == WaitMode.WaitingClientConfirm;
        if (pendingReturnDialogue && !string.IsNullOrEmpty(_pendingDialogueAfterReturn))
        {
            if (_flow is GameFlowController gfc)
                gfc.SetPendingDialogueReturnPackage(0);
            string convToStart = _pendingDialogueAfterReturn;
            _pendingDialogueAfterReturn = null;
            _wait = WaitMode.WaitingDialogueEnd;
            _controller?.SetBlock(true);
            GameStateService.SetState(GameState.ClientDialog);
            StartCoroutine(ShowClientDialogueNextFrame(convToStart));
            return;
        }
        if (_wait == WaitMode.WaitingClientConfirm)
        {
            if (_day2LitWarehouseDetourActive)
            {
                _day2LitWarehouseDetourActive = false;
                _wait = WaitMode.WaitingDialogueEnd;
                _controller?.SetBlock(true);
                GameStateService.SetState(GameState.ClientDialog);
                StartCoroutine(ShowClientDialogueNextFrame(Day2CandlesLitReturnConversation));
                return;
            }
            if (_day2After60sMeetPlayVideoOnClientReturn)
            {
                _day2After60sMeetPlayVideoOnClientReturn = false;
                _day2After60sMeetLastEntryId = -1;
                _day2After60sMeetSlapSoundPlayed = false;
                _day2After60sMeetEnergySoundPlayed = false;
                _wait = WaitMode.WaitingDialogueEnd;
                _controller?.SetBlock(true);
                GameStateService.SetState(GameState.ClientDialog);
                StartCoroutine(ShowClientDialogueNextFrame(Day2After60sMeetAfterVideoConversation, lockMovement: true));
                return;
            }

            if (_currentStep != null && string.Equals(_currentStep.stepId, "return_to_client_day1_5", StringComparison.OrdinalIgnoreCase))
            {
                // Запускаем Client_Day1.5.2 как обычный диалог (без портрета и F)
                string convToStart = "Client_Day1.5.2";
                _wait = WaitMode.WaitingDialogueEnd;
                _controller?.SetBlock(true);
                GameStateService.SetState(GameState.ClientDialog);
                StartCoroutine(ShowClientDialogueNextFrame(convToStart));
                return;
            }
            _wait = WaitMode.Idle;
            Advance();
        }
    }

    private IEnumerator RunDay2After4455LitWarehouseSequence(bool gave5577InDay1)
    {
        _wait = WaitMode.WaitingDialogueEnd;
        if (_customDialogueUI != null)
            _customDialogueUI.SetForcedAutoAdvance(true, 6f);

        DialogueManager.StartConversation(Day2After4455LitWarehouseConversation);
        while (DialogueManager.isConversationActive)
            yield return null;

        if (gave5577InDay1)
        {
            if (_customDialogueUI != null)
                _customDialogueUI.SetForcedAutoAdvance(true, 6f);
            DialogueManager.StartConversation(Day2After4455LitWarehouseConversationGiveExtra);
            while (DialogueManager.isConversationActive)
                yield return null;
        }

        if (_customDialogueUI != null)
            _customDialogueUI.SetForcedAutoAdvance(false);

        _controller?.SetBlock(false);
        GameStateService.SetState(GameState.Warehouse);
        if (_flow is GameFlowController gfcCommon)
        {
            gfcCommon.EnterClientDialogueState(false, movePlayerToClient: false);
            gfcCommon.SetPendingDialogueReturnPackage(0);
            gfcCommon.SetRequiredPackageForReturn(0);
        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _day2After4455LitWarehouseSequence = null;

        string litReturnHintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
            ? (GameConfig.Tutorial.emptyKey ?? "")
            : (GameConfig.Tutorial.returnToClientKey ?? "");

        if (gave5577InDay1)
        {
            if (_flow is GameFlowController gfcGive)
                gfcGive.SetPendingStoryCarryItemId(Day2ToolsCarryItemId);
            _pendingDialogueAfterReturn = Day2After4455LitReturnDialogueTools;
            _wait = WaitMode.WaitingClientConfirm;
            _flow?.SetTravelTarget(TravelTarget.Client, litReturnHintKey);
            yield break;
        }

        // День 1 без отдачи 5577 — с посылкой 5577; диалоги у стойки: after_5577_if_not_gave, затем на ходу move_after_tools.
        _pendingDialogueAfterReturn = Day2After4455LitReturnDialogue5577;
        _wait = WaitMode.WaitingClientReturnForDialogue;
        if (_flow is GameFlowController gfcNoGive)
        {
            gfcNoGive.SetPendingStoryCarryItemId(null);
            GameStateService.SetRequiredPackage(5577, enforceOnly: false);
            GameStateService.SetPackageDropLocked(false);
            _deliveryNoteView?.Hide();
            gfcNoGive.SetPendingDialogueReturnPackage(5577);
        }
        _flow?.SetTravelTarget(TravelTarget.Client, litReturnHintKey);
    }

    private IEnumerator RunDay2After60sMeetWarehouseSequence()
    {
        _wait = WaitMode.WaitingDialogueEnd;
        if (_customDialogueUI != null)
            _customDialogueUI.SetForcedAutoAdvance(true, 6f);

        DialogueManager.StartConversation(Day2After60sMeetWarehouseAutoConversation);
        while (DialogueManager.isConversationActive)
            yield return null;

        if (_customDialogueUI != null)
            _customDialogueUI.SetForcedAutoAdvance(false);

        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        GameStateService.SetState(GameState.Warehouse);

        if (_flow is GameFlowController gfc)
        {
            gfc.EnterClientDialogueState(false, movePlayerToClient: false);
            gfc.SetPendingDialogueReturnPackage(0);
            gfc.SetPendingStoryCarryItemId(null);
            gfc.StartRandomDeliveryTaskAndSetRequiredForReturn();
            gfc.RefreshWarehouseDeliveryNote();
            gfc.ShowWarehousePickHint();
        }

        _day2After4455LitWarehouseSequence = null;
        _day2After60sMeetPlayVideoOnClientReturn = true;
        _wait = WaitMode.WaitingClientConfirm;
        string hintKey = (_flow != null && _flow.PreferEmptyOverMeetClient && !IsRunning)
            ? (GameConfig.Tutorial.emptyKey ?? "")
            : (GameConfig.Tutorial.returnToClientKey ?? "");
        _flow?.SetTravelTarget(TravelTarget.Client, hintKey);
    }

    /// <summary> Показать диалог с клиентом со следующего кадра после телепорта, чтобы UI успел отрисоваться. </summary>
    /// <param name="lockMovement">Если false, игрок может передвигаться во время диалога (например Client_Day1.5.3).</param>
    private IEnumerator ShowClientDialogueNextFrame(string conversationTitle, bool lockMovement = true)
    {
        yield return null;
        if (string.IsNullOrEmpty(conversationTitle)) yield break;
        ((GameFlowController)_flow).EnterClientDialogueState(lockMovement);
        _client?.StartClientDialogWithSpecificStep("", conversationTitle);
    }

    private IEnumerator StartDialogueNextFrame(string conversationTitle)
    {
        yield return null;
        if (string.IsNullOrEmpty(conversationTitle)) yield break;
        DialogueManager.StartConversation(conversationTitle);
    }

    private IEnumerator RestorePlayerPoseNextFrame(Vector3 position, Quaternion rotation, Quaternion cameraRotation)
    {
        yield return null;
        if (_flow is not GameFlowController gfc || gfc.Player == null || gfc.Player.PlayerCamera == null)
            yield break;
        gfc.Player.TeleportTo(position, rotation);
        gfc.Player.PlayerCamera.transform.rotation = cameraRotation;
        gfc.Player.SyncRotationFromCamera();
    }

    private void PlayDay2After60sMeetWarehouseImpactSound()
    {
        if (_day2After60sMeetWarehouseImpactClip == null)
            return;
        Vector3 position = Vector3.zero;
        if (_flow is GameFlowController gfc && gfc.Player != null)
            position = gfc.Player.transform.position;
        else if (Camera.main != null)
            position = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(_day2After60sMeetWarehouseImpactClip, position, _day2After60sMeetWarehouseImpactVolume);
    }

    private void ScheduleDay1AfterClient14WarehouseImpactSound()
    {
        if (_day1AfterClient14WarehouseImpactClip == null)
            return;
        if (_day1AfterClient14ImpactSoundCoroutine != null)
            StopCoroutine(_day1AfterClient14ImpactSoundCoroutine);
        _day1AfterClient14ImpactSoundCoroutine = StartCoroutine(CoDay1AfterClient14WarehouseImpactDelayed());
    }

    private IEnumerator CoDay1AfterClient14WarehouseImpactDelayed()
    {
        yield return new WaitForSeconds(Day1AfterClient14WarehouseImpactDelaySeconds);
        _day1AfterClient14ImpactSoundCoroutine = null;
        PlayDay1AfterClient14WarehouseImpactSound();
    }

    private void PlayDay1AfterClient14WarehouseImpactSound()
    {
        if (_day1AfterClient14WarehouseImpactClip == null)
            return;
        Vector3 position = Vector3.zero;
        if (_flow is GameFlowController gfc && gfc.Player != null)
            position = gfc.Player.transform.position;
        else if (Camera.main != null)
            position = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(_day1AfterClient14WarehouseImpactClip, position, _day1AfterClient14WarehouseImpactVolume);
    }

    private void OnSubtitleShown(Subtitle subtitle)
    {
        if (subtitle?.dialogueEntry == null || DialogueManager.masterDatabase == null)
            return;
        Conversation conv = DialogueManager.masterDatabase.GetConversation(subtitle.dialogueEntry.conversationID);
        if (conv == null || !string.Equals(conv.Title, Day2After60sMeetAfterVideoConversation, StringComparison.OrdinalIgnoreCase))
            return;

        _day2After60sMeetLastEntryId = subtitle.dialogueEntry.id;
        // В этом диалоге id=4/5 — выбор игрока, часто не приходит в OnSubtitleShown
        // (showPCSubtitlesDuringLine=0). Поэтому привязываем к первым ответным репликам:
        // id=6 после "Ударить по лицу", id=10 после "Вылить энергетик".
        if (_day2After60sMeetLastEntryId == 6 && !_day2After60sMeetSlapSoundPlayed)
        {
            _day2After60sMeetSlapSoundPlayed = true;
            PlayClipAtPlayer(_day2After60sMeetSlapClip, _day2After60sMeetSlapVolume);
        }
        else if (_day2After60sMeetLastEntryId == 10 && !_day2After60sMeetEnergySoundPlayed)
        {
            _day2After60sMeetEnergySoundPlayed = true;
            PlayClipAtPlayer(_day2After60sMeetEnergyClip, _day2After60sMeetEnergyVolume);
        }
    }

    private void ClearHandsAndDeliveryStateAfterDay2After60sAfterVideoNonEmergency()
    {
        _flow?.RemovePackageFromHands();
        if (_flow is not GameFlowController gfc || gfc.Player == null)
            return;
        PlayerHands hands = HandsRegistry.Hands;
        if (hands != null && hands.Current is PhoneItemView)
            hands.DropCurrentItem(gfc.Player.DropPoint.position, Quaternion.identity);
        gfc.SetPendingDialogueReturnPackage(0);
        gfc.SetRequiredPackageForReturn(0);
    }

    private void StartDay2After60sMeetEmergencyCall()
    {
        if (_flow is not GameFlowController gfc || gfc.Player == null)
            return;

        _day2After60sMeetEmergencyCallRunning = true;
        _day2After60sMeetRestorePoseValid = gfc.Player.PlayerCamera != null;
        _day2After60sMeetRestorePos = gfc.Player.transform.position;
        _day2After60sMeetRestoreRot = gfc.Player.transform.rotation;
        if (gfc.Player.PlayerCamera != null)
            _day2After60sMeetRestoreCamRot = gfc.Player.PlayerCamera.transform.rotation;

        ClearHandsAndDeliveryStateAfterDay2After60sAfterVideoNonEmergency();

        GameStateService.UnlockPhone();
        _client?.CloseUI();
        GameStateService.SetState(GameState.None);
        gfc.EnterClientDialogueState(false);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        PlayerHands hands = HandsRegistry.Hands;
        if (hands != null && _phoneItemView != null)
            hands.TryTake(_phoneItemView, gfc.Player.PhoneHandPoint);

        _wait = WaitMode.Idle;
        // Важно: разговор 911 НЕ запускаем тут автоматически.
        // Игрок должен сам набрать номер в телефоне, после чего PhoneStoryWiring запустит Phone_CallEmergency_911.
        GameStateService.SetState(GameState.Phone);
    }

    private void StartDay2EndFlow()
    {
        if (_day2EndFlowStarted)
            return;
        _day2EndFlowStarted = true;
        if (_day2EndFlowCoroutine != null)
            StopCoroutine(_day2EndFlowCoroutine);
        _day2EndFlowCoroutine = StartCoroutine(RunDay2EndFlowSequence());
    }

    private IEnumerator RunDay2EndFlowSequence()
    {
        _wait = WaitMode.WaitingDialogueEnd;
        yield return StartCoroutine(PlayAutoDialogueFreeRoam(Day2EndConversationBeforeRadio));

        _wait = WaitMode.Idle;
        GameStateService.SetState(GameState.None);
        if (_flow is GameFlowController gfc)
            gfc.EnterClientDialogueState(false);
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _day2EndWaitingForRadioInteract = true;
        _day2EndRadioInteracted = false;
        _flow?.ShowRadioHintOnce();
        if (_radioInteractable != null)
            _radioInteractable.SetForcedStaticOnlyMode(true);

        while (!_day2EndRadioInteracted)
            yield return null;

        _day2EndWaitingForRadioInteract = false;
        if (_radioInteractable != null)
            _radioInteractable.SetForcedStaticOnlyMode(false);

        _wait = WaitMode.WaitingDialogueEnd;
        yield return StartCoroutine(PlayAutoDialogueFreeRoam(Day2EndConversationAfterRadioStatic));

        _controller?.SetBlock(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _flow?.HideHint();
        _wait = WaitMode.WaitingFadeToBlack;
        _flow?.PlayFadeToBlack(3f, () =>
        {
            _wait = WaitMode.Idle;
            if (_flow is GameFlowController gfcEnd)
                gfcEnd.QuitApplicationAfterStoryEnding();
        });

        _day2EndFlowCoroutine = null;
    }

    private IEnumerator PlayAutoDialogueFreeRoam(string conversationTitle)
    {
        if (string.IsNullOrEmpty(conversationTitle))
            yield break;
        if (_customDialogueUI != null)
            _customDialogueUI.SetForcedAutoAdvance(true, 6f);
        DialogueManager.StartConversation(conversationTitle);
        while (DialogueManager.isConversationActive)
            yield return null;
        if (_customDialogueUI != null)
            _customDialogueUI.SetForcedAutoAdvance(false);
    }

    private void OnAnyRadioInteracted()
    {
        if (!_day2EndWaitingForRadioInteract)
            return;
        _day2EndRadioInteracted = true;
    }

    private void RestoreAfterDay2After60sMeetEmergencyCall()
    {
        if (_flow is not GameFlowController gfc || gfc.Player == null)
            return;

        PlayerHands hands = HandsRegistry.Hands;
        if (hands != null && hands.Current is PhoneItemView)
            hands.DropCurrentItem(gfc.Player.DropPoint.position, Quaternion.identity);

        if (_day2After60sMeetRestorePoseValid)
        {
            gfc.Player.TeleportTo(_day2After60sMeetRestorePos, _day2After60sMeetRestoreRot);
            if (gfc.Player.PlayerCamera != null)
            {
                gfc.Player.PlayerCamera.transform.rotation = _day2After60sMeetRestoreCamRot;
                gfc.Player.SyncRotationFromCamera();
            }
        }

        _wait = WaitMode.WaitingDialogueEnd;
        _controller?.SetBlock(true);
        GameStateService.SetState(GameState.ClientDialog);
        StartCoroutine(ShowClientDialogueNextFrame(Day2After60sMeetAfterAmbulanceConversation, lockMovement: true));
    }

    private void PlayClipAtPlayer(AudioClip clip, float volume)
    {
        if (clip == null)
            return;
        Vector3 position = Vector3.zero;
        if (_flow is GameFlowController gfc && gfc.Player != null)
            position = gfc.Player.transform.position;
        else if (Camera.main != null)
            position = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
    }

    private void EnsureCustomDialogueUISubscription()
    {
        CustomDialogueUI runtimeUi = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;
        if (runtimeUi == _customDialogueUI && _isSubscribedToSubtitleShown)
            return;
        if (runtimeUi == _customDialogueUI && runtimeUi == null)
            return;

        UnsubscribeCustomDialogueUI();
        _customDialogueUI = runtimeUi;
        if (_customDialogueUI != null)
        {
            _customDialogueUI.OnSubtitleShown += OnSubtitleShown;
            _isSubscribedToSubtitleShown = true;
        }
    }

    private void UnsubscribeCustomDialogueUI()
    {
        if (_customDialogueUI != null && _isSubscribedToSubtitleShown)
            _customDialogueUI.OnSubtitleShown -= OnSubtitleShown;
        _isSubscribedToSubtitleShown = false;
    }

    /// <summary> Показать только портрет клиента со следующего кадра после телепорта, чтобы спрайт и плашка не были пустыми. </summary>
    private IEnumerator ShowPortraitOnlyNextFrame()
    {
        yield return null;
        _client?.ShowPortraitOnly("Client_Day1.5");
        ((GameFlowController)_flow).EnterClientDialogueState(true);
    }
}

[Serializable]
public class Step
{
    public string stepId;
    public StepType type;
    public string conversationTitle;
    public string hintText;
    public string triggerId;
    public bool optional;
    public bool autoTravel;
    public bool removePackageAfterDialogue, showDeliveryNote, hideDeliveryNote, expireRadioOnEnter, showRadioHintOnEnter;
    public int deliveryNoteNumber;
    public string deliveryNoteLuaCondition;
    public string skipIfLuaConditionFalse;
    public List<string> activateRadioEventIds;
    public float radioStaticVolume = -1f;
    public float radioStaticVolumeWhenEnter = -1f;
    public string computerVideoKind;
    public float fadeToBlackDuration;
}

public enum StepType { None, PressSpace, GoToDoorWarehouse, ReturnFromWarehouse, GoToRouter, GoToPhone, Dialogue, GoToRadio, GoWarehouse, GoWarehouseWaitReturn, ReturnToClient, WatchComputerVideo, DialogueRadioStyle, ActivateRadioEvent, FadeToBlack }
