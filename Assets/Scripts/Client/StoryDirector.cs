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
    [SerializeField] private CustomDialogueUI _customDialogueUIRef;

    private List<Step> _steps = new List<Step>();
    private int _index = -1;
    private DeliveryNoteView _deliveryNoteView;
    private IGameFlowController _flow;
    private IPlayerInput _input;
    private IPlayerBlocker _controller;
    private bool _pendingRemovePackageAfterDialogue;
    private Step _currentStep;



    private enum WaitMode { Idle, WaitingDialogueEnd, WaitingWarehouseConfirm, WaitingReturnToClientArea, WaitingClientConfirm, WaitingClientReturnForDialogue, WaitingRadioComplete, WaitingTrigger, WaitingFreeRoamClientConfirm, WaitingKnockThenWarehouse, WaitingClientPortraitOnlySpace, WaitingComputerVideo, WaitingFadeToBlack, WaitingTeleportToWarehouse, WaitingTeleportToClient }
    private WaitMode _wait = WaitMode.Idle;
    private string _pendingDialogueAfterReturn;
    private Coroutine _knockDelayCoroutine;
    private string _waitingRadioStyleConversation;
    private CustomDialogueUI _customDialogueUI;
    public string CurrentStepId => (_index >= 0 && _index < _steps.Count) ? _steps[_index].stepId : "";
    /// <summary> True, если сюжет уже запущен (хотя бы один шаг был активирован). </summary>
    public bool HasStoryStarted => _index >= 0;
    public bool IsRunning => _index >= 0 && _index < _steps.Count && _wait != WaitMode.Idle;
    public bool IsWaitingForRadioComplete => _wait == WaitMode.WaitingRadioComplete;
    /// <summary> Всегда false: логика «выйти из зоны склада» заменена на «телепорт на склад → телепорт к клиенту». </summary>
    public bool IsWaitingForWarehouseStoryZoneExit => false;

    /// <summary> Телепорт на склад разрешён: после туториала (free_roam_before_clients) — всегда; во время туториала — только на шагах перехода на склад/радио. Исключение: во время диалога с клиентом не проверяется здесь (игрок в диалоге). </summary>
    public bool IsStepAllowingTravelToWarehouse =>
        IsAtOrPastStep("free_roam_before_clients")
        || (_currentStep != null && (_currentStep.type == StepType.GoToDoorWarehouse || _currentStep.type == StepType.GoWarehouse || _currentStep.type == StepType.GoWarehouseWaitReturn || _currentStep.type == StepType.GoToRadio));

    /// <summary> Телепорт к клиенту (со склада) разрешён: после туториала — всегда; во время туториала — только на шагах возврата/радио/ожидания. Исключение: когда с клиентом говорят и посылают за посылкой — проверка посылки в руках в GameFlowController. </summary>
    public bool IsStepAllowingTravelToClient =>
        IsAtOrPastStep("free_roam_before_clients")
        || (_currentStep != null && (_currentStep.type == StepType.ReturnFromWarehouse || _currentStep.type == StepType.ReturnToClient))
        || _wait == WaitMode.WaitingRadioComplete
        || (_currentStep != null && _currentStep.type == StepType.GoWarehouse && _wait == WaitMode.WaitingClientConfirm)
        || (_currentStep != null && _currentStep.type == StepType.GoToRadio);

    /// <summary> True, если текущий шаг сценария — «вернуться к клиенту с посылкой»; только в этом случае при возврате со склада проверяется наличие нужной посылки. На шагах радио/свободного перемещения посылка не требуется. </summary>
    public bool DoesCurrentStepRequirePackageForReturn =>
        _currentStep != null && (_currentStep.type == StepType.ReturnToClient || _currentStep.type == StepType.GoWarehouseWaitReturn);

    /// <summary> True, если сценарий ждёт триггер с указанным id (игрок может выполнить действие только на нужном шаге). </summary>
    public bool IsExpectingTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return false;
        return _currentStep != null && _wait == WaitMode.WaitingTrigger
            && string.Equals(_currentStep.triggerId, triggerId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary> True, если обучение не идёт или текущий шаг — указанный или любой после него (по порядку в списке шагов). </summary>
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

        // Необязательный шаг (например Radio_Day1_2): игрок может не идти на склад, а подойти к клиенту и нажать E — пропускаем шаг и сразу запускаем следующий диалог (Client_Day1.3).
        if (_currentStep != null && _currentStep.optional && _client != null && _client.IsPlayerInside && _input.InteractPressed)
        {
            Debug.Log($"[Tutorial] Опциональный шаг \"{_currentStep.stepId}\" пропущен (E у клиента) → переход к Client_Day1.3.");
            _wait = WaitMode.Idle;
            Advance(); // optional_radio → free_roam_before_client_day1_3
            Advance(); // free_roam → client_day1_3 (StartDialogue)
            return;
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
            // Радио на складе: игрок физически на складе, но Advance() запустил free_roam_before_clients и FreeRoamNone выставил state=None. Восстанавливаем Warehouse, чтобы можно было вернуться к клиенту по F.
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

    /// <summary> Показываем подсказку только из таблицы (ключи tutorial.*), каждый ключ — один раз за сессию. </summary>
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
                key = GameConfig.Tutorial.radioUseKey;
                break;
            case StepType.GoWarehouseWaitReturn:
                key = GameConfig.Tutorial.warehouseReturnKey;
                break;
        }
        if (!string.IsNullOrEmpty(key))
        {
            Debug.Log($"[Tutorial] Подсказка для шага \"{step.stepId}\" (type={step.type}), ключ: \"{key}\" (один раз за сессию)");
            _flow?.ShowHintOnceByKey(key);
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
        if (string.Equals(step.stepId, "free_roam_after_day1_4", StringComparison.OrdinalIgnoreCase) && _flow is GameFlowController gfc)
            gfc.SetRequiredPackageForReturn(0);
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
        // go_to_warehouse_for_radio: цель «склад» ставим без подсказки — игрок уже умеет перемещаться
        bool silent = string.Equals(step.stepId, "go_to_warehouse_for_radio", StringComparison.OrdinalIgnoreCase);
        string hint = silent ? "" : (_flow?.ResolveHintText(null, GameConfig.Tutorial.doorWarehouseKey) ?? "");
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
        _wait = WaitMode.WaitingRadioComplete;
        // Опциональное радио: посылка не нужна, можно в любой момент вернуться к клиенту (F) и поговорить (E) вместо радио.
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
        // go_warehouse_day1_5: спустя 10 сек после free_roam_after_day1_4. Игрок может уже быть на складе или пойти туда; по возврате к клиенту — Client_Day1.5.
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
                string returnHint = _flow != null && _flow.PreferEmptyOverMeetClient
                    ? (_flow.ResolveHintText(null, GameConfig.Tutorial.emptyKey) ?? "")
                    : (_flow?.ResolveHintText(null, GameConfig.Tutorial.returnToClientKey) ?? "");
                _flow?.SetTravelTarget(TravelTarget.Client, returnHint);
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
        string warehouseHint = _flow?.ResolveHintText(null, GameConfig.Tutorial.goWarehouseKey) ?? "";
        _flow?.SetTravelTarget(TravelTarget.Warehouse, warehouseHint);
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

        // Client_Day1.5 закончился → перебрасываем на склад (go_warehouse_after_day1_5), запись, взять посылку, возврат к клиенту
        if (string.Equals(conv, "Client_Day1.5", StringComparison.OrdinalIgnoreCase))
        {
            GameStateService.SetState(GameState.None);
            ((GameFlowController)_flow).EnterClientDialogueState(false);
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
        // Опциональное радио: игрок пришёл на склад — даём цель «к клиенту» (телепорт у двери, не к стойке)
        if (_wait == WaitMode.WaitingRadioComplete)
        {
            string hint = _flow != null && _flow.PreferEmptyOverMeetClient
                ? (_flow.ResolveHintText(null, GameConfig.Tutorial.emptyKey) ?? "")
                : (_flow?.ResolveHintText(null, GameConfig.Tutorial.returnToClientKey) ?? "");
            _flow?.SetTravelTarget(TravelTarget.Client, hint, useFreeTeleportPointForClient: true);
            return;
        }
        if (_wait != WaitMode.WaitingWarehouseConfirm) return;

        // Визит на склад до истечения 10 сек (шаг free_roam_after_day1_4) — посылку брать не нужно.
        if (_currentStep != null && string.Equals(_currentStep.stepId, "free_roam_after_day1_4", StringComparison.OrdinalIgnoreCase) && _flow is GameFlowController gfcFree)
        {
            gfcFree.SetRequiredPackageForReturn(0);
        }

        // Зашли на склад в рамках шага go_warehouse_day1_5: ждём выхода из триггерной зоны склада → телепорт к клиенту и диалог Client_Day1.5
        string stepId = _currentStep != null ? _currentStep.stepId : "";
        // #region agent log
        AgentDebugLog.Log("StoryDirector.cs:OnTeleportedToWarehouse", "check go_warehouse_day1_5", "{\"stepId\":\"" + stepId + "\",\"match\":" + (string.Equals(stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase) ? "true" : "false") + "}", "H1");
        // #endregion
        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            // Визит «в течение 10 сек» после 1.4/1.4.1 — посылку брать не нужно, только потом вернуться к клиенту на Client_Day1.5.
            if (_flow is GameFlowController gfcDay15)
                gfcDay15.SetRequiredPackageForReturn(0);
            _pendingDialogueAfterReturn = "Client_Day1.5";
            _wait = WaitMode.WaitingClientConfirm;
            string hint = _flow != null && _flow.PreferEmptyOverMeetClient
                ? (_flow.ResolveHintText(null, GameConfig.Tutorial.emptyKey) ?? "")
                : (_flow?.ResolveHintText(null, GameConfig.Tutorial.returnToClientKey) ?? "");
            _flow?.SetTravelTarget(TravelTarget.Client, hint);
            return;
        }

        // go_warehouse_after_day1_5: телепорт на склад → сразу записка в руках, нужно взять посылку (диалог Client_Day1.5.1 временно отключён).
        if (_currentStep != null && string.Equals(_currentStep.stepId, "go_warehouse_after_day1_5", StringComparison.OrdinalIgnoreCase))
        {
            _wait = WaitMode.Idle;
            GameStateService.SetState(GameState.Warehouse);
            Advance();
            var gfc = _flow as GameFlowController;
            gfc?.StartRandomDeliveryTaskAndSetRequiredForReturn();
            gfc?.RefreshWarehouseDeliveryNote();
            gfc?.ShowWarehousePickHint();
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

    private void OnTeleportedToClient()
    {
        // Опциональное радио: вернулись к клиенту без прослушивания — не продвигаем шаг, даём возможность нажать E и поговорить с клиентом (пропуск радио). Не вызываем EnterClientDialogueState(false): камера не поднималась, ClearDialogueCameraOffset испортит позицию.
        if (_wait == WaitMode.WaitingRadioComplete)
        {
            GameStateService.SetState(GameState.None);
            return;
        }
        if (_wait == WaitMode.WaitingTeleportToClient)
        {
            Debug.Log($"[Tutorial] Действие выполнено: телепорт к клиенту → шаг \"{_currentStep?.stepId}\" завершён, переход к следующему");
            _wait = WaitMode.Idle;
            Advance();
            return;
        }
        bool pendingReturnDialogue = _wait == WaitMode.WaitingClientReturnForDialogue || _wait == WaitMode.WaitingClientConfirm;
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
            // return_to_client_day1_5: вернулись к клиенту с посылкой — только спрайт клиентки (без диалога), по Space выходим
            if (_currentStep != null && string.Equals(_currentStep.stepId, "return_to_client_day1_5", StringComparison.OrdinalIgnoreCase))
            {
                _wait = WaitMode.WaitingClientPortraitOnlySpace;
                GameStateService.SetState(GameState.ClientDialog);
                _controller?.SetBlock(true);
                ((GameFlowController)_flow).EnterClientDialogueState(true);
                _client?.ShowPortraitOnly("Client_Day1.5");
                Debug.Log("[Tutorial] return_to_client_day1_5: показан портрет клиентки (без диалога). Нажмите Space для выхода.");
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
