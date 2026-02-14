using System;
using PixelCrushers.DialogueSystem;

public interface ICustomDialogueUI
{
    event Action<Subtitle> OnSubtitleShown;
    event Action OnClientDialogueFinishedByKey;
    void ShowUI();
    void HideUI();
    bool IsDialogueActive { get; }
    void SetUIVisibility(bool visible);
}