using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using static IGameFlowController;

public sealed class StoryDirector : MonoBehaviour
{
    [SerializeField] private AttitudeChoiceRecorder _attitudeRecorder;
    [SerializeField] private PhoneUnlockDirector _phoneUnlock;
    [SerializeField] private ClientInteraction _client;

    private List<Step> _steps = new List<Step>();
    private int _index = -1;
    private DeliveryNoteView _deliveryNoteView;
    private IGameFlowController _flow;
    private IPlayerInput _input;
    private IPlayerBlocker _controller;
    private bool _pendingRemovePackageAfterDialogue;
    private Step _currentStep;

    private enum WaitMode { Idle, WaitingDialogueEnd, WaitingWarehouseConfirm, WaitingReturnToClientArea, WaitingClientConfirm, WaitingRadioComplete, WaitingTrigger, WaitingFreeRoamClientConfirm }
    private WaitMode _wait = WaitMode.Idle;

    public string CurrentStepId => (_index >= 0 && _index < _steps.Count) ? _steps[_index].stepId : "";
    public bool IsRunning => _index >= 0 && _index < _steps.Count && _wait != WaitMode.Idle;

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
                showRadioHintOnEnter = d.showRadioHintOnEnter
            });
        }
        return list;
    }

    private void OnDestroy()
    {
        if (_client != null) _client.ClientDialogueStepCompleted -= OnDialogueCompleted;
        _flow.OnTeleportedToWarehouse -= OnTeleportedToWarehouse;
        _flow.OnTeleportedToClient -= OnTeleportedToClient;
        if (_flow is GameFlowController gfc)
        {
            gfc.OnRadioStoryCompleted -= OnRadioStoryCompleted;
            gfc.OnTriggerFired -= OnTriggerFired;
        }
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

        if (_wait == WaitMode.WaitingTrigger && _currentStep != null && _currentStep.type == StepType.PressSpace && _input.NextPressed)
        {
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (_wait == WaitMode.WaitingFreeRoamClientConfirm && _client != null && _client.IsPlayerInside && _input.InteractPressed)
        {
            _wait = WaitMode.Idle;
            Advance();
            return;
        }

        if (!_input.ConfirmPressed) return;

        if (_wait == WaitMode.WaitingReturnToClientArea && _client != null && _client.IsPlayerInside) { _wait = WaitMode.Idle; Advance(); return; }
    }

    private void OnRadioStoryCompleted()
    {
        if (_wait != WaitMode.WaitingRadioComplete) return;
        _wait = WaitMode.Idle;
        Advance();
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
            Debug.Log("[Story] All steps complete.");
            return;
        }

        _currentStep = _steps[_index];

        if (!string.IsNullOrEmpty(_currentStep.skipIfLuaConditionFalse))
        {
            bool condition = DialogueLua.GetVariable(_currentStep.skipIfLuaConditionFalse).AsBool;
            if (!condition)
            {
                Debug.Log($"[Story] Skipping {_currentStep.stepId} (skipIfLuaConditionFalse={_currentStep.skipIfLuaConditionFalse} is false)");
                Advance();
                return;
            }
        }

        ShowHintForStep(_currentStep);
        ApplyDirectives(_currentStep);
        Debug.Log($"[Story] Step {_index + 1}/{_steps.Count}: {_currentStep.stepId} ({_currentStep.type})");

        switch (_currentStep.type)
        {
            case StepType.None: FreeRoamNone(_currentStep); break;
            case StepType.PressSpace: PressSpace(_currentStep); break;
            case StepType.GoToRouter: GoToRouter(_currentStep); break;
            case StepType.GoToPhone: GoToPhone(_currentStep); break;
            case StepType.Dialogue: StartDialogue(_currentStep); break;
            case StepType.GoToRadio: GoToRadio(_currentStep); break;
            case StepType.GoWarehouse: GoWarehouse(_currentStep); break;
            case StepType.GoWarehouseWaitReturn: GoWarehouseWaitReturn(_currentStep); break;
            case StepType.ReturnToClient: ReturnToClient(_currentStep); break;
        }
    }

    private void ShowHintForStep(Step step)
    {
        string hint = "";
        switch (step.type)
        {
            case StepType.None:
                hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.meetClientKey) ?? step.hintText ?? "????????? ????. ????????? ? ??????? ? ??????? E.";
                break;
            case StepType.PressSpace:
                hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.pressSpaceKey) ?? step.hintText ?? "??????? ??????";
                break;
            case StepType.GoToRouter:
                hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.routerHintKey) ?? step.hintText ?? "????????? ? ???????";
                break;
            case StepType.GoToPhone:
                hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.phoneHintKey) ?? step.hintText ?? "????????? ? ???????? ? ????????? ??????????";
                break;
            case StepType.GoToRadio:
                hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.radioUseKey) ?? step.hintText ?? "????????? ? ????? ? ??????? E";
                break;
            case StepType.GoWarehouseWaitReturn:
                hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.warehouseReturnKey) ?? step.hintText ?? "??????? ?? ?????. ????????? ? ???????.";
                break;
        }
        if (!string.IsNullOrEmpty(hint))
            _flow?.ShowHintRaw(hint);
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
        _flow?.ShowHintRaw(step.hintText ?? "");
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
        if (step.deliveryNoteNumber > 0 && _flow is GameFlowController gfc)
            gfc.SetFixedPackageForNextWarehouse(step.deliveryNoteNumber);

        if (step.autoTravel)
        {
            _wait = WaitMode.WaitingWarehouseConfirm;
            _flow?.ForceTravel(TravelTarget.Warehouse);
            return;
        }

        _wait = WaitMode.WaitingWarehouseConfirm;
        string hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.goWarehouseKey) ?? step.hintText ?? "??????? F ??? ??????? ?? ?????";
        _flow?.SetTravelTarget(TravelTarget.Warehouse, hint);
    }

    private void ReturnToClient(Step step)
    {
        if (step.deliveryNoteNumber > 0 && _flow is GameFlowController gfc)
            gfc.SetRequiredPackageForReturn(step.deliveryNoteNumber);

        if (step.autoTravel)
        {
            _wait = WaitMode.WaitingClientConfirm;
            _flow?.ForceTravel(TravelTarget.Client);
            return;
        }

        _wait = WaitMode.WaitingClientConfirm;
        string hint = _flow?.ResolveHintText(step.hintText, GameConfig.Tutorial.returnToClientKey) ?? step.hintText ?? "??????? F ????? ????????? ? ???????";
        _flow?.SetTravelTarget(TravelTarget.Client, hint);
    }

    private void OnDialogueCompleted(ClientDialogueStepCompletionData data)
    {
        if (_wait != WaitMode.WaitingDialogueEnd) return;

        _attitudeRecorder?.RecordFromLua();
        _phoneUnlock?.TryUnlockFromDialogue();
        if (_pendingRemovePackageAfterDialogue) { _pendingRemovePackageAfterDialogue = false; _flow?.RemovePackageFromHands(); }
        if (_currentStep?.hideDeliveryNote == true && _deliveryNoteView != null) _deliveryNoteView.Hide();

        GameStateService.SetState(GameState.None);
        ((GameFlowController)_flow).EnterClientDialogueState(false);
        _wait = WaitMode.Idle;
        Advance();
    }

    private void OnTeleportedToWarehouse() { if (_wait == WaitMode.WaitingWarehouseConfirm) { _wait = WaitMode.Idle; Advance(); } }
    private void OnTeleportedToClient() { if (_wait == WaitMode.WaitingClientConfirm) { _wait = WaitMode.Idle; Advance(); } }
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
}

public enum StepType { None, PressSpace, GoToRouter, GoToPhone, Dialogue, GoToRadio, GoWarehouse, GoWarehouseWaitReturn, ReturnToClient }
