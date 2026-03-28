using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Нижняя плашка "E - закрыть окно". Показывается отдельным UI-объектом,
/// а не через центральный PlayerHintView.
/// </summary>
public sealed class WindowCloseHintView : MonoBehaviour
{
    public static WindowCloseHintView Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Image _image;
    [SerializeField] private Sprite _hintSprite;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.18f;

    private CanvasGroup _canvasGroup;
    private float _targetAlpha;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureCanvasGroup();
        SetAlphaImmediate(0f);
        if (_root != null)
            _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        TickFade();
    }

    public void Show()
    {
        if (_image != null && _hintSprite != null)
            _image.sprite = _hintSprite;
        _targetAlpha = 1f;
        if (_root != null && !_root.activeSelf)
            _root.SetActive(true);
    }

    public void Hide()
    {
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

        float step = Time.deltaTime / Mathf.Max(0.01f, _fadeDuration);
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, step);

        bool keepVisible = _targetAlpha > 0f || _canvasGroup.alpha > 0.001f;
        if (_root.activeSelf != keepVisible)
            _root.SetActive(keepVisible);
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
