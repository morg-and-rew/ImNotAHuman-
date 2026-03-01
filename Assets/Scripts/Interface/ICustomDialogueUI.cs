using System;
using UnityEngine;
using UnityEngine.UI;
using PixelCrushers.DialogueSystem;

public interface ICustomDialogueUI
{
    event Action<Subtitle> OnSubtitleShown;
    event Action OnClientDialogueFinishedByKey;
    /// <summary> Вызывается при показе меню выбора ответов (responses). Подписчики могут скрыть плашку имени и т.п. </summary>
    event Action OnResponseMenuShown;
    /// <summary> Вызывается при скрытии меню выбора. </summary>
    event Action OnResponseMenuHidden;
    void ShowUI();
    void HideUI();
    bool IsDialogueActive { get; }
    void SetUIVisibility(bool visible);
    /// <summary> Добавить объект в Hide On choice mode вручную (для объектов со сцены, когда UI — префаб). Скрывается при выборе, показывается при снятии выбора. </summary>
    void AddToHideOnChoiceMode(GameObject go);
    /// <summary> То же по Image: в список попадёт image.gameObject (плашка имени и т.п.). </summary>
    void AddToHideOnChoiceMode(Image image);
}