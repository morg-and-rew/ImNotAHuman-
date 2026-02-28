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
    void NotifyTutorialActionCompleted(TutorialPendingAction action);
    void ShowPhonePutHintOnce();
    void ShowPhoneHint();
    void HideHint();
    void ShowEmptyHint();
    void ShowEmptyHintAfterPackagePick();
    bool PreferEmptyOverMeetClient { get; }
    bool MeetClientHintAlreadyShown { get; }
    void MarkProviderCallDone();
    void HidePhoneHint();
    void ShowPhoneCallHint();
    void ShowRadioHintOnce();
    void NotifyPhonePutDown();
    void ShowMeetClientHintOnce();
    void ShowHintRaw(string key);
    void ShowHintOnceByKey(string key);
    /// <summary> Флаг: если шаг туториала уже выполнен игроком — принудительно не показываем снова. </summary>
    bool IsTutorialStepAlreadyShown(string key);
    /// <summary> Пометить шаг туториала как выполненный (игрок совершил действие). </summary>
    void MarkTutorialStepCompleted(string key);
    void LockPlayerForDialogue(bool isLocked);
    event System.Action OnPlayerReturnedFromWarehouse;
    event System.Action OnPlayerReturnedToClient;
    bool ProviderCallDone { get; }
    bool IsInClientDialogState { get; }
    PlayerView Player { get; }

    /// <summary> Действие, которое должен выполнить игрок, чтобы туториал исчез и мог смениться другим. </summary>
    public enum TutorialPendingAction
    {
        None,
        PressSpace,
        WarehousePick
    }

    public enum TravelTarget
    {
        None,
        Warehouse,
        Client
    }

    event Action OnTeleportedToWarehouse;
    event Action OnTeleportedToClient;

    void SetTravelTarget(TravelTarget target, string hintKey, bool useFreeTeleportPointForClient = false, bool allowWarehouseConfirmFromClient = false);
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
    bool IsPhonePickupAllowed();
    bool AcceptAnyPackageForReturn { get; }
    bool IsPackagePickAllowedByStory { get; }
    event Action<string> OnExitZonePassed;
    void NotifyExitZonePassed(string zoneId);
    void TeleportToTableAndFixPosition(string postVideoConversation = null);
    string ResolveHintText(string hintText, string fallbackLocalizationKey);
    void PlayFadeToBlack(float durationSeconds, Action onComplete);
}