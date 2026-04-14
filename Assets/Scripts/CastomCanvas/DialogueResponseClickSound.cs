using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Воспроизводит звук при нажатии на кнопку ответа в диалоге. Добавляется на кнопки ответов из CustomDialogueUI.
/// </summary>
public sealed class DialogueResponseClickSound : MonoBehaviour
{
    private void Awake()
    {
        RegisterClick();
    }

    private void OnEnable()
    {
        RegisterClick();
    }

    /// <summary>
    /// Не захватывать GameSoundController в замыкании: при перезагрузке сцены старый инстанс уничтожается,
    /// а кнопка (или шаблон) может пережить сцену — иначе MissingReferenceException при клике.
    /// </summary>
    private void RegisterClick()
    {
        Button button = GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveListener(OnDialogueResponseClicked);
        button.onClick.AddListener(OnDialogueResponseClicked);
    }

    private void OnDestroy()
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.onClick.RemoveListener(OnDialogueResponseClicked);
    }

    private static void OnDialogueResponseClicked()
    {
        GameSoundController gsc = GameSoundController.Instance;
        if (gsc == null) return;
        gsc.PlayDialogueResponseClick();
    }
}
