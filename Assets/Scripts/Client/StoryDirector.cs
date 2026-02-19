using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using static IGameFlowController;

public sealed class StoryDirector : MonoBehaviour
{
    private const float KnockAfterFreeRoamDelaySeconds = 10f;

    [SerializeField] private AttitudeChoiceRecorder _attitudeRecorder;
    [SerializeField] private PhoneUnlockDirector _phoneUnlock;
    [SerializeField] private ClientInteraction _client;
    [SerializeField] private AudioSource _knockAudioSource;
    [SerializeField] private Computer _computer;

    private List<Step> _steps = new List<Step>();
    private int _index = -1;
    private DeliveryNoteView _deliveryNoteView;
    private IGameFlowController _flow;
    private IPlayerInput _input;
    private IPlayerBlocker _controller;
    private bool _pendingRemovePackageAfterDialogue;
    private Step _currentStep;

    private enum WaitMode { Idle, WaitingDialogueEnd, WaitingWarehouseConfirm, WaitingReturnToClientArea, WaitingClientConfirm, WaitingClientReturnForDialogue, WaitingRadioComplete, WaitingTrigger, WaitingFreeRoamClientConfirm, WaitingKnockThenWarehouse, WaitingWarehouseStoryZoneExit, WaitingClientPortraitOnlySpace, WaitingComputerVideo, WaitingFadeToBlack, WaitingTeleportToWarehouse, WaitingTeleportToClient }
    private WaitMode _wait = WaitMode.Idle;
    private string _pendingDialogueAfterReturn;
    private Coroutine _knockDelayCoroutine;
    private bool _waitingRadioStyleDay151;
    private string _waitingRadioStyleConversation;
    private const float RadioStyleAutoAdvanceSeconds = 8f;

    public string CurrentStepId => (_index >= 0 && _index < _steps.Count) ? _steps[_index].stepId : "";
    public bool IsRunning => _index >= 0 && _index < _steps.Count && _wait != WaitMode.Idle;

    /// <summary> True, если сценарий ждёт триггер с указанным id (игрок может выполнить действие только на нужном шаге). </summary>
    public bool IsExpectingTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return false;
        return _currentStep != null && _wait == WaitMode.WaitingTrigger
            && string.Equals(_currentStep.triggerId, triggerId, System.StringComparison.OrdinalIgnoreCase);
    }

    public void Initialize(IGameFlowController flow, IPlayerInput input, IPlayerBlocker controller, DeliveryNoteView deliveryNoteView)
    {
        _flow = flow;
        _input = input;
        _controller = controller;
        _deliveryNoteView = deliveryNoteView;

        _steps = BuildStepsFromConfig();
        if (_steps.Count == 0)
        {
            Debug.LogError("[Story] No steps in GameConfig.json. Check story.steps.");
            return;
        }

        if (_client != null)
            _client.ClientDialogueStepCompleted += OnDialogueCompleted;
        _flow.OnTeleportedToWarehouse += OnTeleportedToWarehouse;
        _flow.OnTeleportedToClient += OnTeleportedToClient;
        if (_flow is GameFlowController gfc)
        {
            gfc.OnRadioStoryCompleted += OnRadioStoryCompleted;
            gfc.OnTriggerFired += OnTriggerFired;
            gfc.OnExitZonePassed += OnExitZonePassed;
            gfc.OnComputerVideoEnded += OnComputerVideoEnded;
        }

        Debug.Log($"[Story] Loaded {_steps.Count} steps from GameConfig.");
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
                Debug.LogWarning($"[Story] Unknown stepType '{d.stepType}' at index {i}");
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
        _flow.OnTeleportedToWarehouse -= OnTeleportedToWarehouse;
        _flow.OnTeleportedToClient -= OnTeleportedToClient;
        if (_flow is GameFlowController gfc)
        {
            gfc.OnRadioStoryCompleted -= OnRadioStoryCompleted;
            gfc.OnTriggerFired -= OnTriggerFired;
            gfc.OnExitZonePassed -= OnExitZonePassed;
        }
    }

    private void OnExitZonePassed(string zoneId)
    {
        // #region agent log
        AgentDebugLog.Log("StoryDirector.cs:OnExitZonePassed", "entry", "{\"zoneId\":\"" + (zoneId ?? "") + "\",\"wait\":" + (int)_wait + ",\"expectedWait\":10}", "H3");
        // #endregion
        if (_wait != WaitMode.WaitingWarehouseStoryZoneExit) return;
        if (!string.Equals(zoneId, WarehouseStoryTriggerZone.ZoneIdExited, StringComparison.OrdinalIgnoreCase)) return;

        _flow?.ForceTravel(TravelTarget.Client);
    }

    private void StartKnockThenWarehouseFlow()
    {
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

    public void Tick()
    {
        if (_input == null) return;

        if (_wait == WaitMode.WaitingTrigger && _currentStep != null && _input.NextPressed)
        {
            if (_currentStep.type == StepType.PressSpace || _currentStep.type == StepType.GoToDoorWarehouse || _currentStep.type == StepType.ReturnFromWarehouse)
            {
                Debug.Log($"[Tutorial] Действие выполнено: нажат пробел (Next) → шаг \"{_currentStep.stepId}\" завершён, переход к следующему");
                _wait = WaitMode.Idle;
                Advance();
                return;
            }
        }

        if (_wait == WaitMode.WaitingFreeRoamClientConfirm && _client != null && _client.IsPlayerInside && _input.InteractPressed)
        {
            Debug.Log($"[Tutorial] Действие выполнено: E у стойки клиента → шаг \"{_currentStep?.stepId}\" завершён, переход к следующему");
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (_wait == WaitMode.WaitingClientPortraitOnlySpace && (_input.ConfirmPressed || _input.NextPressed))
        {
            Debug.Log($"[Tutorial] Действие выполнено: Space/F у портрета клиента → шаг \"{_currentStep?.stepId}\" завершён, переход к следующему");
            _client?.HidePortraitOnly();
            _flow?.RemovePackageFromHands();
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (!_input.ConfirmPressed) return;

        if (_wait == WaitMode.WaitingClientReturnForDialogue && _flow != null && _flow.TryPerformPendingReturnToClient())
            return;

        if (_wait == WaitMode.WaitingReturnToClientArea && _client != null && _client.IsPlayerInside)
        {
            Debug.Log($"[Tutorial] Действие выполнено: игрок в зоне стойки клиента → шаг \"{_currentStep?.stepId}\" завершён, переход к следующему");
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
        Debug.Log($"[Tutorial] Действие выполнено: триггер \"{triggerId}\" → шаг \"{_currentStep.stepId}\" завершён, переход к следующему");
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
            Debug.Log("[Tutorial] Обучение завершено: все шаги пройдены.");
            return;
        }

        _currentStep = _steps[_index];

        if (!string.IsNullOrEmpty(_currentStep.skipIfLuaConditionFalse))
        {
            bool condition = DialogueLua.GetVariable(_currentStep.skipIfLuaConditionFalse).AsBool;
            if (!condition)
            {
                Debug.Log($"[Tutorial] Шаг \"{_currentStep.stepId}\" пропущен (Lua {_currentStep.skipIfLuaConditionFalse}=false), переход к следующему");
                Advance();
                return;
            }
        }

        ShowHintForStep(_currentStep);
        ApplyDirectives(_currentStep);
        Debug.Log($"[Tutorial] Шаг запущен: {_index + 1}/{_steps.Count} stepId=\"{_currentStep.stepId}\" type={_currentStep.type} wait={_wait}");

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

    /// <summary> Показываем подсказку только из таблицы (ключи tutorial.* / intro.*). step.hintText и прочие тексты не используем. </summary>
    private void ShowHintForStep(Step step)
    {
        string hint = "";
        switch (step.type)
        {
            case StepType.None:
                hint = (_flow != null && (_flow.PreferEmptyOverMeetClient || _flow.MeetClientHintAlreadyShown))
                    ? (_flow.ResolveHintText(null, GameConfig.Tutorial.emptyKey) ?? "")
                    : (_flow?.ResolveHintText(null, GameConfig.Tutorial.meetClientKey) ?? "");
                break;
            case StepType.PressSpace:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.pressSpaceKey) ?? "";
                break;
            case StepType.GoToDoorWarehouse:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.doorWarehouseKey) ?? "";
                break;
            case StepType.ReturnFromWarehouse:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.returnPressFKey) ?? "";
                break;
            case StepType.GoToRouter:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.routerHintKey) ?? "";
                break;
            case StepType.GoToPhone:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.phoneHintKey) ?? "";
                break;
            case StepType.GoToRadio:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.radioUseKey) ?? "";
                break;
            case StepType.GoWarehouseWaitReturn:
                hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.warehouseReturnKey) ?? "";
                break;
            case StepType.WatchComputerVideo:
            case StepType.DialogueRadioStyle:
            case StepType.ActivateRadioEvent:
                break;
            case StepType.FadeToBlack:
                break;
        }
        if (!string.IsNullOrEmpty(hint))
        {
            Debug.Log($"[Tutorial] Показана подсказка для шага \"{step.stepId}\" (type={step.type}), текст: \"{(hint.Length > 60 ? hint.Substring(0, 60) + "..." : hint)}\"");
            _flow?.ShowHintRaw(hint);
        }
    }

    private void ApplyDirectives(Step step)
    {
        if (step.expireRadioOnEnter) _flow?.ExpireAllRadioAvailable();
        if (step?.activateRadioEventIds != null)
        {
            foreach (string id in step.activateRadioEventIds)
            {
                if (!string.IsNullOrEmpty(id))
                    _flow?.ActivateRadioEvent(id);
            }
        }
        if (step.showRadioHintOnEnter) _flow?.ShowRadioHintOnce();
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
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
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
        string hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.doorWarehouseKey) ?? "";
        _flow?.SetTravelTarget(TravelTarget.Warehouse, hint);
    }

    private void ReturnFromWarehouse(Step step)
    {
        _controller?.SetBlock(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _wait = WaitMode.WaitingTeleportToClient;
        string hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.returnPressFKey) ?? "";
        _flow?.SetTravelTarget(TravelTarget.Client, hint, useFreeTeleportPointForClient: true);
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
        if (step.optional)
        {
            _wait = WaitMode.Idle;
            Advance();
        }
        else
        {
            _wait = WaitMode.WaitingRadioComplete;
        }
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
        // go_warehouse_day1_5: игрок сам заходит в зону склада и выходит (без F, без подсказок). После выхода из зоны — телепорт к клиенту и Client_Day1.5.
        if (string.Equals(step.stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            _controller?.SetBlock(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            GameStateService.SetState(GameState.None);
            _pendingDialogueAfterReturn = "Client_Day1.5";
            _wait = WaitMode.WaitingWarehouseStoryZoneExit;
            return;
        }

        if (step.deliveryNoteNumber > 0 && _flow is GameFlowController gfc)
            gfc.SetFixedPackageForNextWarehouse(step.deliveryNoteNumber);

        if (step.autoTravel)
        {
            _wait = WaitMode.WaitingWarehouseConfirm;
            _flow?.ForceTravel(TravelTarget.Warehouse);
            return;
        }

        _wait = WaitMode.WaitingWarehouseConfirm;
        string hint = _flow?.ResolveHintText(null, GameConfig.Tutorial.goWarehouseKey) ?? "";
        _flow?.SetTravelTarget(TravelTarget.Warehouse, hint);
    }

    private void ReturnToClient(Step step)
    {
        if (_flow is GameFlowController gfc)
        {
            if (step.deliveryNoteNumber > 0)
                gfc.SetRequiredPackageForReturn(step.deliveryNoteNumber);
            else if (string.Equals(step.stepId, "return_to_client_day1_5", StringComparison.OrdinalIgnoreCase))
                gfc.SetAcceptAnyPackageForReturn(true);
        }

        if (step.autoTravel)
        {
            _wait = WaitMode.WaitingClientConfirm;
            _flow?.ForceTravel(TravelTarget.Client);
            return;
        }

        _wait = WaitMode.WaitingClientConfirm;
        string hint = _flow != null && _flow.PreferEmptyOverMeetClient
            ? (_flow.ResolveHintText(null, GameConfig.Tutorial.emptyKey) ?? "")
            : (_flow?.ResolveHintText(null, GameConfig.Tutorial.returnToClientKey) ?? "");
        _flow?.SetTravelTarget(TravelTarget.Client, hint);
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
            Debug.LogWarning("[Story] DialogueRadioStyle step has no conversationTitle.");
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
        Debug.Log("[Tutorial] StartFadeToBlack → обучение скрыто");
        _flow?.HideHint();
        _wait = WaitMode.WaitingFadeToBlack;
        float duration = step.fadeToBlackDuration > 0f ? step.fadeToBlackDuration : 3f;
        _flow?.PlayFadeToBlack(duration, OnFadeToBlackComplete);
    }

    private void OnFadeToBlackComplete()
    {
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnDialogueCompleted(ClientDialogueStepCompletionData data)
    {
        if (_wait != WaitMode.WaitingDialogueEnd) return;

        _attitudeRecorder?.RecordFromLua();
        _phoneUnlock?.TryUnlockFromDialogue();
        if (_pendingRemovePackageAfterDialogue) { _pendingRemovePackageAfterDialogue = false; _flow?.RemovePackageFromHands(); }
        if (_currentStep?.hideDeliveryNote == true && _deliveryNoteView != null) _deliveryNoteView.Hide();

        string conv = data.ConversationTitle ?? "";

        // Client_Day1.4.1 закончился → посылка отдана, убрать из рук, свободное хождение (Advance)
        if (string.Equals(conv, "Client_Day1.4.1", StringComparison.OrdinalIgnoreCase))
        {
            _flow?.RemovePackageFromHands();
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        // Client_Day1.5 закончился → перебрасываем на склад (следующий шаг go_warehouse_after_day1_5), потом Client_Day1.5.1, потом посылка, возврат к клиенту
        if (string.Equals(conv, "Client_Day1.5", StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        // Client_Day1.5.1 закончился (реплика героя на складе) → остаёмся на складе, показываем подсказку по посылке, берём посылку, F к клиенту
        if (string.Equals(conv, "Client_Day1.5.1", StringComparison.OrdinalIgnoreCase))
        {
            // #region agent log
            AgentDebugLog.Log("StoryDirector.cs:Client_Day1.5.1_complete", "before state set", "{\"stateBefore\":\"" + GameStateService.CurrentState + "\"}", "H_state");
            // #endregion
            GameStateService.SetState(GameState.Warehouse);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            ((GameFlowController)_flow).RefreshWarehouseDeliveryNote();
            // #region agent log
            AgentDebugLog.Log("StoryDirector.cs:Client_Day1.5.1_complete", "after refresh", "{\"stateAfter\":\"" + GameStateService.CurrentState + "\"}", "H_state");
            // #endregion
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        // Client_Day1.4 закончился: если не отдали посылку — свободное хождение; иначе — на склад за посылкой 5577, по возвращении Client_Day1.4.1
        if (string.Equals(conv, "Client_Day1.4", StringComparison.OrdinalIgnoreCase))
        {
            DialogueLua.SetVariable("RunWarehouse5577Steps", false);
            bool choseToGive = DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool;
            if (!choseToGive)
            {
                GameStateService.SetState(GameState.None);
                ((GameFlowController)_flow).EnterClientDialogueState(false);
                _wait = WaitMode.Idle;
                Advance();
                return;
            }
            // Решил отдать посылку — перенос на склад, взять 5577, по возвращении запустить Client_Day1.4.1
            _pendingDialogueAfterReturn = "Client_Day1.4.1";
            _wait = WaitMode.WaitingWarehouseConfirm;
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
            if (_flow is GameFlowController gfc)
            {
                gfc.SetFixedPackageForNextWarehouse(5577);
                gfc.ForceTravel(TravelTarget.Warehouse);
            }
            return;
        }

        // Остальные диалоги — как раньше
        GameStateService.SetState(GameState.None);
        ((GameFlowController)_flow).EnterClientDialogueState(false);
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnTeleportedToWarehouse()
    {
        // #region agent log
        AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToWarehouse", "entry", "{\"wait\":" + (int)_wait + ",\"pendingDialogueAfterReturn\":\"" + (_pendingDialogueAfterReturn ?? "") + "\"}", "H4");
        // #endregion
        if (_wait == WaitMode.WaitingTeleportToWarehouse)
        {
            Debug.Log($"[Tutorial] Действие выполнено: телепорт на склад → шаг \"{_currentStep?.stepId}\" завершён, переход к следующему");
            _wait = WaitMode.Idle;
            Advance();
            return;
        }
        if (_wait != WaitMode.WaitingWarehouseConfirm) return;

        // Зашли на склад в рамках шага go_warehouse_day1_5: ждём выхода из триггерной зоны склада → телепорт к клиенту и диалог Client_Day1.5
        string stepId = _currentStep != null ? _currentStep.stepId : "";
        // #region agent log
        AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToWarehouse", "check go_warehouse_day1_5", "{\"stepId\":\"" + stepId + "\",\"match\":" + (string.Equals(stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase) ? "true" : "false") + "}", "H1");
        // #endregion
        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            _pendingDialogueAfterReturn = "Client_Day1.5";
            _wait = WaitMode.WaitingWarehouseStoryZoneExit;
            // #region agent log
            AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToWarehouse", "set WaitingWarehouseStoryZoneExit", "{}", "H1");
            // #endregion
            return;
        }

        // go_warehouse_after_day1_5: реплика героя на складе — показ слов как у радио (субтитры, авто-пролистывание, без блокировки движения)
        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_after_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            _wait = WaitMode.WaitingDialogueEnd;
            _waitingRadioStyleDay151 = true;
            if (DialogueManager.instance != null)
                DialogueManager.instance.conversationEnded += OnRadioStyleDay151Ended;
            var gfc = _flow as GameFlowController;
            gfc?.CustomDialogueUI?.SetForcedAutoAdvance(true, RadioStyleAutoAdvanceSeconds);
            DialogueManager.StartConversation("Client_Day1.5.1");
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
            string hint = _flow != null && _flow.PreferEmptyOverMeetClient
                ? (_flow.ResolveHintText(null, GameConfig.Tutorial.emptyKey) ?? "")
                : (_flow?.ResolveHintText(null, GameConfig.Tutorial.returnToClientKey) ?? "");
            _flow?.SetTravelTarget(TravelTarget.Client, hint);
            // #region agent log
            AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToWarehouse", "set pending return and travel target Client", "{\"pendingConv\":\"" + _pendingDialogueAfterReturn + "\"}", "H4");
            // #endregion
            return;
        }
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnRadioStyleDay151Ended(Transform _)
    {
        if (!_waitingRadioStyleDay151) return;
        _waitingRadioStyleDay151 = false;
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnRadioStyleDay151Ended;
        var gfc = _flow as GameFlowController;
        gfc?.CustomDialogueUI?.SetForcedAutoAdvance(false);
        GameStateService.SetState(GameState.Warehouse);
        gfc?.EnterClientDialogueState(false);
        gfc?.RefreshWarehouseDeliveryNote();
        gfc?.ShowWarehousePickHint();
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnTeleportedToClient()
    {
        if (_wait == WaitMode.WaitingTeleportToClient)
        {
            Debug.Log($"[Tutorial] Действие выполнено: телепорт к клиенту → шаг \"{_currentStep?.stepId}\" завершён, переход к следующему");
            _wait = WaitMode.Idle;
            Advance();
            return;
        }
        bool pendingReturnDialogue = _wait == WaitMode.WaitingClientReturnForDialogue || _wait == WaitMode.WaitingWarehouseStoryZoneExit;
        // #region agent log
        AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToClient", "entry", "{\"wait\":" + (int)_wait + ",\"pendingReturnDialogue\":" + pendingReturnDialogue + ",\"pendingConv\":\"" + (_pendingDialogueAfterReturn ?? "") + "\"}", "H5");
        // #endregion
        if (pendingReturnDialogue && !string.IsNullOrEmpty(_pendingDialogueAfterReturn))
        {
            // #region agent log
            AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToClient", "starting dialogue", "{\"conv\":\"" + _pendingDialogueAfterReturn + "\"}", "H5");
            // #endregion
            if (_flow is GameFlowController gfc)
                gfc.SetPendingDialogueReturnPackage(0);
            string convToStart = _pendingDialogueAfterReturn;
            _pendingDialogueAfterReturn = null;
            _wait = WaitMode.WaitingDialogueEnd;
            _controller?.SetBlock(true);
            GameStateService.SetState(GameState.ClientDialog);
            ((GameFlowController)_flow).EnterClientDialogueState(true);
            if (_client != null)
                _client.StartClientDialogWithSpecificStep("", convToStart);
            return;
        }
        if (_wait == WaitMode.WaitingClientConfirm)
        {
            // return_to_client_day1_5: вернулись к клиенту с посылкой — показываем только спрайт клиента, по Space выходим в свободное хождение
            if (_currentStep != null && string.Equals(_currentStep.stepId, "return_to_client_day1_5", StringComparison.OrdinalIgnoreCase))
            {
                _wait = WaitMode.WaitingClientPortraitOnlySpace;
                GameStateService.SetState(GameState.ClientDialog);
                _controller?.SetBlock(true);
                ((GameFlowController)_flow).EnterClientDialogueState(true);
                _client?.ShowPortraitOnly("Client_Day1.5");
                return;
            }
            _wait = WaitMode.Idle;
            Advance();
        }
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
    public string computerVideoKind;
    public float fadeToBlackDuration;
}

public enum StepType { None, PressSpace, GoToDoorWarehouse, ReturnFromWarehouse, GoToRouter, GoToPhone, Dialogue, GoToRadio, GoWarehouse, GoWarehouseWaitReturn, ReturnToClient, WatchComputerVideo, DialogueRadioStyle, ActivateRadioEvent, FadeToBlack }
