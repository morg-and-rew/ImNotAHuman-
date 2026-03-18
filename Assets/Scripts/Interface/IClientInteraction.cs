using System;
using UnityEngine;
using UnityEngine.UI;

public interface IClientInteraction
{
    event Action<ClientDialogueStepCompletionData> ClientDialogueStepCompleted;
    event Action ClientDialogueFinished;
    event Action ClientConversationStarted;
    event Action RequestRemovePackageFromHands;
    int CurrentStepIndex { get; }
    bool IsActive { get; }
    bool IsPlayerInside { get; }
    /// <summary> true, когда диалог в паузе и ждёт продолжения — подсказка «поговорить с клиентом» должна показываться. </summary>
    bool IsWaitingForContinue { get; }
    bool IsPlayerLookingAtClient(PlayerView player);
    void Initialize(Canvas uiRoot, Image leftImage, Image rightImage, ICustomDialogueUI customDialogueUI);
    void StartClientDialog();
    void StartClientDialogWithSpecificStep(string clientId, string conversationTitle);
    void ContinueSequence();
    void CloseUI();
    /// <summary>Сброс флагов диалога при телепорте на склад — иначе IsActive «залипает» и складской Warehouse_WrongPackage включает портреты клиента.</summary>
    void ResetClientDialogFlagsForWarehouse();
    void PlayWrongPackageConversation();
    void ShowPortraitOnly(string conversation);
    void HidePortraitOnly();
}