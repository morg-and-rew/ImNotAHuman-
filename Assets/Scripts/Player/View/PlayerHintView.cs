using UnityEngine;
using UnityEngine.UI;
using PixelCrushers;

public sealed class PlayerHintView : MonoBehaviour
{
    [System.Serializable]
    private struct LocalizedSpritePair
    {
        public Sprite source;
        public Sprite english;
    }

    public static PlayerHintView Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Image _image;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.18f;
    [Header("Localization")]
    [Tooltip("Пары спрайтов для английского: source (текущий/RU) -> english (перевод).")]
    [SerializeField] private LocalizedSpritePair[] _englishSpritePairs;
    [SerializeField] private string _languagePlayerPrefsKey = "Language";
    [SerializeField] private string _englishLanguageCode = "en";

    private Sprite _raycastSprite;
    private Sprite _windowSprite;
    private Sprite _doorSprite;
    private Sprite _clientSprite;
    private int _raycastSetFrame = -1;
    private int _windowSetFrame = -1;
    private int _doorSetFrame = -1;
    private int _clientSetFrame = -1;

    private bool _suspended;
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
        if (_root != null) _root.SetActive(false);
    }

    private void Start()
    {
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetRaycastHint(Sprite sprite)
    {
        _raycastSprite = ResolveLocalizedSprite(sprite);
        _raycastSetFrame = Time.frameCount;
    }

    public void SetWindowHint(Sprite sprite)
    {
        _windowSprite = ResolveLocalizedSprite(sprite);
        _windowSetFrame = Time.frameCount;
    }

    public void SetDoorHint(Sprite sprite)
    {
        _doorSprite = ResolveLocalizedSprite(sprite);
        _doorSetFrame = Time.frameCount;
    }

    public void SetClientHint(Sprite sprite)
    {
        _clientSprite = ResolveLocalizedSprite(sprite);
        _clientSetFrame = Time.frameCount;
    }

    /// <summary>Включить/выключить временное скрытие всех подсказок (например, во время просмотра видео на мониторе).</summary>
    public void SetSuspended(bool value)
    {
        _suspended = value;
        if (_suspended)
        {
            SetAlphaImmediate(0f);
            if (_root != null)
                _root.SetActive(false);
            if (_image != null)
                _image.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (_suspended)
        {
            if (_root != null)
                _root.SetActive(false);
            if (_image != null)
                _image.enabled = false;
            return;
        }

        int frame = Time.frameCount;
        Sprite clientSprite = _clientSetFrame == frame ? _clientSprite : null;
        Sprite raycastSprite = _raycastSetFrame == frame ? _raycastSprite : null;
        Sprite windowSprite = _windowSetFrame == frame ? _windowSprite : null;
        Sprite doorSprite = _doorSetFrame == frame ? _doorSprite : null;

        // Используем только подсказки, подтвержденные в текущем кадре:
        // если источник перестал обновляться, старый спрайт не "залипнет".
        Sprite showSprite = clientSprite ?? raycastSprite ?? windowSprite ?? doorSprite;
        bool shouldShow = showSprite != null;
        if (_root != null)
        {
            if (shouldShow)
            {
                _root.SetActive(true);
                // Включаем родителей, если рут был выключен из-за неактивного родителя
                Transform p = _root.transform.parent;
                while (p != null && !p.gameObject.activeSelf)
                {
                    p.gameObject.SetActive(true);
                    p = p.parent;
                }
            }
        }

        if (_image != null)
        {
            _image.enabled = true;
            if (showSprite != null)
                _image.sprite = showSprite;
        }

        _targetAlpha = shouldShow ? 1f : 0f;
        TickFade();
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
        {
            _root.SetActive(_targetAlpha > 0.5f);
            return;
        }

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

    private Sprite ResolveLocalizedSprite(Sprite sprite)
    {
        if (sprite == null)
            return null;
        if (!IsEnglishLanguage())
            return sprite;
        if (_englishSpritePairs == null || _englishSpritePairs.Length == 0)
            return sprite;

        for (int i = 0; i < _englishSpritePairs.Length; i++)
        {
            if (_englishSpritePairs[i].source == sprite && _englishSpritePairs[i].english != null)
                return _englishSpritePairs[i].english;
        }

        return sprite;
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
