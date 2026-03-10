using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Воспроизводит звук при нажатии на кнопку ответа в диалоге. Добавляется на кнопки ответов из CustomDialogueUI.
/// </summary>
public sealed class DialogueResponseClickSound : MonoBehaviour
{
    private bool _listenerAdded;

    private void Awake()
    {
        TryAddListener();
    }

    private void OnEnable()
    {
        TryAddListener();
    }

    private void TryAddListener()
    {
        if (_listenerAdded) return;
        Button button = GetComponent<Button>();
        if (button == null) return;
        GameSoundController sound = FindFirstObjectByType<GameSoundController>();
        if (sound == null) return;
        button.onClick.AddListener(() => sound.PlayDialogueResponseClick());
        _listenerAdded = true;
    }
}
