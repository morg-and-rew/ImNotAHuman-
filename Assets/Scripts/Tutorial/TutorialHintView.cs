using UnityEngine;
using UnityEngine.UI;
using PixelCrushers;

public sealed class TutorialHintView : MonoBehaviour
{
    [Tooltip("Только панель подсказки обучения. Не назначать весь PlayerCanvas — иначе при скрытии подсказки скроются и портреты клиента.")]
    [SerializeField] private GameObject _root;
    [Tooltip("Image, в котором отображается спрайт подсказки. Спрайты задаются в Hint Keys и Hint Sprites (одинаковый порядок).")]
    [SerializeField] private Image _image;
    [Tooltip("Ключи подсказок (tutorial.press_space, tutorial.door_warehouse и т.д.). Порядок должен совпадать с Hint Sprites.")]
    [SerializeField] private string[] _hintKeys;
    [Tooltip("Спрайты для подсказок по умолчанию (например, RU). Добавь сюда все картинки туториала в том же порядке, что и ключи в Hint Keys.")]
    [SerializeField] private Sprite[] _hintSprites;
    [Tooltip("Спрайты для подсказок на английском. Порядок должен совпадать с Hint Keys. Если массив пустой или неполный, используется Hint Sprites.")]
    [SerializeField] private Sprite[] _hintSpritesEnglish;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.18f;
    [Tooltip("Sorting order канваса туториала — должен быть меньше, чем у окна (-50), чтобы подсказка рисовалась под спрайтом окна.")]
    [SerializeField] private int _canvasSortOrder = -100;
    [Tooltip("Ключ языка в PlayerPrefs (должен совпадать с UI Localization Manager).")]
    [SerializeField] private string _languagePlayerPrefsKey = "Language";
    [Tooltip("Код английского языка, при котором используются Hint Sprites English.")]
    [SerializeField] private string _englishLanguageCode = "en";

    public static TutorialHintView Instance { get; private set; }
    private CanvasGroup _canvasGroup;
    private float _targetAlpha;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureCanvasGroup();
        SetAlphaImmediate(0f);
        _root.SetActive(false);
        ApplyTutorialSortOrder();
    }

    private void ApplyTutorialSortOrder()
    {
        if (_root == null) return;
        Canvas canvas = _root.GetComponentInParent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = _canvasSortOrder;
    }

    private void Update()
    {
        TickFade();
    }

    /// <summary> Ключ из GameConfig без отдельной строки в Hint Keys — тот же спрайт, что у door_warehouse. </summary>
    private static string ResolveSpriteLookupKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        string k = key.Trim();
        if (string.Equals(k, "tutorial.press_f_to_warehouse", System.StringComparison.OrdinalIgnoreCase))
            return "tutorial.door_warehouse";
        return k;
    }

    /// <summary> Показать подсказку по ключу (например tutorial.press_space). Спрайт берётся из массивов в инспекторе. </summary>
    public void Show(string key)
    {
        bool found = false;
        Sprite[] sprites = GetSpritesForCurrentLanguage();
        if (_image != null && _hintKeys != null && sprites != null && _hintKeys.Length == sprites.Length)
        {
            string keyTrim = ResolveSpriteLookupKey(key?.Trim() ?? "");
            for (int i = 0; i < _hintKeys.Length; i++)
            {
                string entry = _hintKeys[i]?.Trim() ?? "";
                if (string.Equals(entry, keyTrim, System.StringComparison.OrdinalIgnoreCase))
                {
                    _image.sprite = sprites[i];
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
        _targetAlpha = 1f;
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
        _targetAlpha = 0f;
    }

    private void EnsureCanvasGroup()
    {
        if (_root == null)
            return;
        _canvasGroup = _root.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _root.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void TickFade()
    {
        if (_root == null)
            return;

        if (_canvasGroup == null)
            EnsureCanvasGroup();

        if (_canvasGroup == null)
            return;

        float duration = Mathf.Max(0.01f, _fadeDuration);
        float step = Time.deltaTime / duration;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, step);

        bool keepVisible = _targetAlpha > 0f || _canvasGroup.alpha > 0.001f;
        if (_root.activeSelf != keepVisible)
            _root.SetActive(keepVisible);

        if (!keepVisible && _image != null)
            _image.enabled = false;
    }

    private void SetAlphaImmediate(float alpha)
    {
        if (_canvasGroup == null)
            EnsureCanvasGroup();
        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
        _targetAlpha = Mathf.Clamp01(alpha);
    }

    private Sprite[] GetSpritesForCurrentLanguage()
    {
        if (IsEnglishLanguage() && _hintSpritesEnglish != null && _hintKeys != null && _hintSpritesEnglish.Length == _hintKeys.Length)
            return _hintSpritesEnglish;
        return _hintSprites;
    }

    private bool IsEnglishLanguage()
    {
        if (GameFlowController.Instance != null)
            return GameFlowController.Instance.IsUiEnglishLocale;

        string lang = "";
        if (UILocalizationManager.instance != null)
            lang = UILocalizationManager.instance.currentLanguage ?? "";
        else if (!string.IsNullOrWhiteSpace(_languagePlayerPrefsKey))
            lang = PlayerPrefs.GetString(_languagePlayerPrefsKey, "");

        return GameFlowController.LocaleIndicatesEnglish(lang);
    }
}

public enum TutorialStep
{
    None,
    PressSpace,
    GoToRouter,
    GoToPhone
}
