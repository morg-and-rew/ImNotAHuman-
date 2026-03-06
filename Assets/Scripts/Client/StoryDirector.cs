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

    [SerializeField] private AttitudeChoiceRecorder _attitudeRecorder;
    [SerializeField] private PhoneUnlockDirector _phoneUnlock;
    [SerializeField] private ClientInteraction _client;
    [SerializeField] private AudioSource _knockAudioSource;
    [SerializeField] private Computer _computer;
    [SerializeField] private CustomDialogueUI _customDialogueUIRef;

    private List<Step> _steps = new List<Step>();
    private int _index = -1;
    private DeliveryNoteView _deliveryNoteView;
    private IGameFlowController _flow;
    private IPlayerInput _input;
    private IPlayerBlocker _controller;
    private bool _pendingRemovePackageAfterDialogue;
    private Step _currentStep;
    private bool _clientDay14HandledByConversationEnded;

    private enum WaitMode { Idle, WaitingDialogueEnd, WaitingWarehouseConfirm, WaitingReturnToClientArea, WaitingClientConfirm, WaitingClientReturnForDialogue, WaitingRadioComplete, WaitingTrigger, WaitingFreeRoamClientConfirm, WaitingKnockThenWarehouse, WaitingClientPortraitOnlySpace, WaitingComputerVideo, WaitingFadeToBlack, WaitingTeleportToWarehouse, WaitingTeleportToClient }
    private WaitMode _wait = WaitMode.Idle;
    private string _pendingDialogueAfterReturn;
    private Coroutine _knockDelayCoroutine;
    private string _waitingRadioStyleConversation;
    private bool _warehousePickHintShown;
    private CustomDialogueUI _customDialogueUI;
    public string CurrentStepId => (_index >= 0 && _index < _steps.Count) ? _steps[_index].stepId : "";
    public bool HasStoryStarted => _index >= 0;
    public bool IsRunning => _index >= 0 && _index < _steps.Count && _wait != WaitMode.Idle;
    public bool IsWaitingForRadioComplete => _wait == WaitMode.WaitingRadioComplete;
    public bool IsCurrentStepGoToRadio => _currentStep != null && _currentStep.type == StepType.GoToRadio;
    /// <summary> True, если текущий шаг — интро «go_to_radio»: пришли на склад после того как положили телефон (go_to_phone → go_to_warehouse_for_radio → go_to_radio). Иначе не показывать tutorial.radio_use. </summary>
    public bool IsIntroGoToRadioStep => IsCurrentStepGoToRadio
        && _index >= 2
        && string.Equals(_steps[_index - 1].stepId, "go_to_warehouse_for_radio", StringComparison.OrdinalIgnoreCase)
        && string.Equals(_steps[_index - 2].stepId, "go_to_phone", StringComparison.OrdinalIgnoreCase);
    public bool IsWaitingForWarehouseStoryZoneExit => false;
    /// <summary> True, если сюжет ждёт подтверждения перехода на склад (например после Client_Day1.4 с ChoseToGivePackage5577). </summary>
    public bool IsWaitingForWarehouseConfirm => _wait == WaitMode.WaitingWarehouseConfirm;
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
        _wait = WaitMode.Idle;
        Advance();
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
            _wait = WaitMode.Idle;
            Advance();
            Advance();
            return;
        }

        if (_wait == WaitMode.WaitingFreeRoamClientConfirm && _client != null && _client.IsPlayerLookingAtClient(_flow.Player) && _input.InteractPressed
            && (HandsRegistry.Hands == null || !HandsRegistry.Hands.HasItem))
        {
            // День 2: после радио ждём, пока игрок сам подойдёт к клиенту и нажмёт E — тогда запускаем Client_day2.1
            if (_currentStep != null && !string.IsNullOrEmpty(_currentStep.conversationTitle)
                && string.Equals(_currentStep.stepId, "day2_after_radio", StringComparison.OrdinalIgnoreCase))
            {
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
            _flow?.ForceTravel(TravelTarget.Warehouse);
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
        _waitingRadioStyleConversation = null;
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnDialogueRadioStyleEnded;
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
        if (_flow is GameFlowController gfc)
            gfc.MarkDay1TutorialCompleted();
        _controller?.SetBlock(false);
        _wait = WaitMode.Idle;
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
            SkepticalChoicesCount = skeptical
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
        if (_wait != WaitMode.WaitingDialogueEnd && !isClientDay14 && !isClientDay152 && !isClientDay153)
            return;

        _attitudeRecorder?.RecordFromLua();
        _phoneUnlock?.TryUnlockFromDialogue();
        if (_pendingRemovePackageAfterDialogue) { _pendingRemovePackageAfterDialogue = false; _flow?.RemovePackageFromHands(); }
        if (_currentStep?.hideDeliveryNote == true && _deliveryNoteView != null) _deliveryNoteView.Hide();

        if (string.Equals(conv, "Client_Day1.4.1", StringComparison.OrdinalIgnoreCase))
        {
            _flow?.RemovePackageFromHands();
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
            // После Client_Day1.5.2 автоматически запускаем Client_Day1.5.3; во время 1.5.3 игрок может передвигаться
            _wait = WaitMode.WaitingDialogueEnd;
            _controller?.SetBlock(false);
            GameStateService.SetState(GameState.ClientDialog);
            StartCoroutine(ShowClientDialogueNextFrame("Client_Day1.5.3", lockMovement: false));
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
            _flow?.ShowHintOnceByKey(GameConfig.Tutorial?.watchVideoKey ?? "tutorial.watch_video");
            _wait = WaitMode.Idle;
            Advance();
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
        if (!string.Equals(lastConv, "Client_Day1.4", StringComparison.OrdinalIgnoreCase)) return;
        _clientDay14HandledByConversationEnded = true;
        HandleClientDay14Completed();
    }

    private void HandleClientDay14Completed()
    {
        DialogueLua.SetVariable("RunWarehouse5577Steps", false);
        bool choseToGive = DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool;
        if (!choseToGive)
        {
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
            _flow.ForceTravel(TravelTarget.Warehouse);
        }
    }

    private void OnTeleportedToWarehouse()
    {
        if (_wait == WaitMode.WaitingTeleportToWarehouse)
        {
            _wait = WaitMode.Idle;
            Advance();
            return;
        }
        if (_wait == WaitMode.WaitingRadioComplete)
        {
            // Радио приоритетно: подсказка остаётся «иди к радио / нажми E», пока игрок не нажал E у радио
            string hintKey = GameConfig.Tutorial.radioUseKey ?? "";
            _flow?.SetTravelTarget(TravelTarget.Client, hintKey, useFreeTeleportPointForClient: true);
            return;
        }
        if (_wait != WaitMode.WaitingWarehouseConfirm) return;

        // После телепорта на склад (в т.ч. из диалога Client_Day1.4) — разблокируем и сбрасываем состояние диалога.
        if (_flow is GameFlowController gfcUnlock)
            gfcUnlock.EnterClientDialogueState(false);
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

    /// <summary> Показать диалог с клиентом со следующего кадра после телепорта, чтобы UI успел отрисоваться. </summary>
    /// <param name="lockMovement">Если false, игрок может передвигаться во время диалога (например Client_Day1.5.3).</param>
    private IEnumerator ShowClientDialogueNextFrame(string conversationTitle, bool lockMovement = true)
    {
        yield return null;
        if (string.IsNullOrEmpty(conversationTitle)) yield break;
        ((GameFlowController)_flow).EnterClientDialogueState(lockMovement);
        _client?.StartClientDialogWithSpecificStep("", conversationTitle);
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
