using PixelCrushers.DialogueSystem;
using System;
using UnityEngine;
using UnityEngine.UI;

public interface IGameFlowController
{
    event Action<string> OnStoryProgressed;
    event Action<string> OnClientEncountered;
    void Init(PlayerView player, IPlayerBlocker controller, IPlayerInput input, IClientInteraction clientInteraction, DeliveryNoteView deliveryNoteView, CustomDialogueUI customDialogueUI = null);
    void SetTutorialStep(TutorialStep step);
    void ShowPhonePutHintOnce();
    void ShowPhoneHint();
    void HideHint();
    void ShowEmptyHint();
    void ShowEmptyHintAfterPackagePick();
    bool PreferEmptyOverMeetClient { get; }
    bool MeetClientHintAlreadyShown { get; }
    void MarkProviderCallDone();
    void HidePhoneHint();
    void ShowPhoneCallHint(); // <--- ?????????
    void ShowRadioHintOnce(); // <--- ?????????
    void ShowMeetClientHintOnce(); // <--- ?????????
    void ShowHintRaw(string text);
    void LockPlayerForDialogue(bool isLocked);
    event System.Action OnPlayerReturnedFromWarehouse;
    event System.Action OnPlayerReturnedToClient;
    bool ProviderCallDone { get; }
    bool IsInClientDialogState { get; }

    public enum TravelTarget
    {
        None,
        Warehouse,
        Client
    }

    event Action OnTeleportedToWarehouse;
    event Action OnTeleportedToClient;

    void SetTravelTarget(TravelTarget target, string hintText, bool useFreeTeleportPointForClient = false);
    void SetTutorialWarehouseVisit(bool isTutorial);
    void ForceTravel(TravelTarget target);
    void SetAllowReturnToClientWithoutExitZone(bool allow);
    void SetPendingDialogueReturnPackage(int packageNumber);
    bool TryPerformPendingReturnToClient();
    TravelTarget CurrentTravelTarget { get; }
    void RemovePackageFromHands();
    void ExpireAllRadioAvailable();
    void ActivateRadioEvent(string id);
    event Action<string> OnTriggerFired;
    void NotifyTrigger(string triggerId);
    bool IsStoryExpectingTrigger(string triggerId);
    event Action<string> OnExitZonePassed;
    void NotifyExitZonePassed(string zoneId);
    void TeleportToTableAndFixPosition(string postVideoConversation = null);
    string ResolveHintText(string hintText, string fallbackLocalizationKey);
    void PlayFadeToBlack(float durationSeconds, Action onComplete);
}