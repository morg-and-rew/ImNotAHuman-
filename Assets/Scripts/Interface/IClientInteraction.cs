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
    void Initialize(Canvas uiRoot, Image leftImage, Image rightImage, ICustomDialogueUI customDialogueUI);
    void StartClientDialog();
    void StartClientDialogWithSpecificStep(string clientId, string conversationTitle);
    void ContinueSequence();
    void CloseUI();
    void PlayWrongPackageConversation();
    void ShowPortraitOnly(string conversation);
    void HidePortraitOnly();
}