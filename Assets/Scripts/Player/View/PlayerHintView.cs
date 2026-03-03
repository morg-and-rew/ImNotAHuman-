using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHintView : MonoBehaviour
{
    public static PlayerHintView Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Image _image;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.18f;

    private Sprite _raycastSprite;
    private Sprite _windowSprite;
    private Sprite _doorSprite;
    private Sprite _clientSprite;

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
        _raycastSprite = sprite;
    }

    public void SetWindowHint(Sprite sprite)
    {
        _windowSprite = sprite;
    }

    public void SetDoorHint(Sprite sprite)
    {
        _doorSprite = sprite;
    }

    public void SetClientHint(Sprite sprite)
    {
        _clientSprite = sprite;
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

        // Оператор ?? (null-coalescing): берём первый не-null спрайт в порядке приоритета.
        // client → raycast (луч по предметам) → window → door. Если все четыре null, showSprite = null и рут не включится.
        // showSprite будет null, если: ClientInteraction не вызвал SetClientHint(спрайт), или вызвал SetClientHint(null);
        // и остальные системы тоже не выставили свой спрайт. Проверь порядок выполнения: SetClientHint вызывается в ClientInteraction.Update(),
        // а LateUpdate идёт после всех Update — значит к этому моменту спрайты уже должны быть установлены.
        Sprite showSprite = _clientSprite ?? _raycastSprite ?? _windowSprite ?? _doorSprite;
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
}
