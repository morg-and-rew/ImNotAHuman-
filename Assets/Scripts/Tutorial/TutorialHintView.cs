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
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.18f;
    [Tooltip("Sorting order канваса туториала — должен быть меньше, чем у окна (-50), чтобы подсказка рисовалась под спрайтом окна.")]
    [SerializeField] private int _canvasSortOrder = -100;

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
}

public enum TutorialStep
{
    None,
    PressSpace,
    GoToRouter,
    GoToPhone
}
