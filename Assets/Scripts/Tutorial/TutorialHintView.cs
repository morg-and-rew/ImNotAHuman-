using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialHintView : MonoBehaviour
{
    [Tooltip("Только панель подсказки обучения. Не назначать весь PlayerCanvas — иначе при скрытии подсказки скроются и портреты клиента.")]
    [SerializeField] private GameObject _root;
    [Tooltip("Image, в котором отображается спрайт подсказки. Спрайты задаются в Hint Keys и Hint Sprites (одинаковый порядок).")]
    [SerializeField] private Image _image;
    [Tooltip("Ключи подсказок (tutorial.press_space, tutorial.door_warehouse и т.д.). Порядок должен совпадать с Hint Sprites.")]
    [SerializeField] private string[] _hintKeys;
    [Tooltip("Спрайты для подсказок. Добавь сюда все картинки туториала в том же порядке, что и ключи в Hint Keys.")]
    [SerializeField] private Sprite[] _hintSprites;

    public static TutorialHintView Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _root.SetActive(false);
    }

    /// <summary> Показать подсказку по ключу (например tutorial.press_space). Спрайт берётся из массивов в инспекторе. </summary>
    public void Show(string key)
    {
        bool found = false;
        if (_image != null && _hintKeys != null && _hintSprites != null && _hintKeys.Length == _hintSprites.Length)
        {
            string keyTrim = key?.Trim() ?? "";
            for (int i = 0; i < _hintKeys.Length; i++)
            {
                string entry = _hintKeys[i]?.Trim() ?? "";
                if (string.Equals(entry, keyTrim, System.StringComparison.OrdinalIgnoreCase))
                {
                    _image.sprite = _hintSprites[i];
                    _image.enabled = true;
                    _image.gameObject.SetActive(true);
                    found = true;
                    break;
                }
            }
        }
        // При ненайденном ключе компонент не выключаем — оставляем последний показанный спрайт видимым

        EnsureParentChainActive();
        _root.SetActive(true);
    }

    private void EnsureParentChainActive()
    {
        if (_root == null) return;
        Transform t = _root.transform.parent;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    public void Hide()
    {
        if (_root == null) return;
        _root.SetActive(false);
    }
}

public enum TutorialStep
{
    None,
    PressSpace,
    GoToRouter,
    GoToPhone
}
