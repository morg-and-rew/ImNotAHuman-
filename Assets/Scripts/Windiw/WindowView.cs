using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PixelCrushers.DialogueSystem;

public sealed class WindowView : MonoBehaviour
{
    [Header("Fullscreen UI")]
    [SerializeField] private Image _fullScreenImage;
    [SerializeField] private Sprite _fullScreenSprite;
    [SerializeField, Min(0f)] private float _fadeInDuration = 2f;
    [SerializeField] private int _windowSortOrder = -50;
    [Header("Day sprites")]
    [SerializeField] private WindowDaySpriteEntry[] _daySprites = new WindowDaySpriteEntry[0];
    [Header("Day replicas")]
    [SerializeField] private WindowDayReplicaEntry[] _dayReplicas = new WindowDayReplicaEntry[0];

    private bool _isPlayerInZone = false;
    private bool _isViewing = false;
    private Coroutine _fadeRoutine;
    private int _originalCanvasSortOrder;

    public bool IsPlayerInZone => _isPlayerInZone;

    private void Start()
    {
        if (_fullScreenImage != null)
        {
            _fullScreenImage.gameObject.SetActive(false);
            if (_fullScreenSprite != null)
                _fullScreenImage.sprite = _fullScreenSprite;
            SetImageAlpha(0f);
        }

        WindowViewManager.Instance?.RegisterWindow(this);
    }

    private void OnDestroy()
    {
        WindowViewManager.Instance?.UnregisterWindow(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerView playerView))
            _isPlayerInZone = true;
    }

    private void TryPlayDayReplica()
    {
        if (_dayReplicas == null || _dayReplicas.Length == 0) return;
        if (DialogueManager.isConversationActive) return;

        string conversationTitle = SelectReplicaForDay(WindowViewManager.Instance != null ? WindowViewManager.Instance.CurrentDay : 1);
        if (string.IsNullOrEmpty(conversationTitle)) return;

        DialogueManager.StartConversation(conversationTitle);
    }

    private string SelectReplicaForDay(int day)
    {
        WindowDayReplicaEntry exact = null;
        WindowDayReplicaEntry fallback = null;
        int bestFallbackDay = int.MinValue;

        for (int i = 0; i < _dayReplicas.Length; i++)
        {
            WindowDayReplicaEntry entry = _dayReplicas[i];
            if (entry == null || entry.day <= 0 || string.IsNullOrEmpty(entry.conversationTitle))
                continue;

            if (entry.day == day)
            {
                exact = entry;
                break;
            }

            if (entry.day < day && entry.day > bestFallbackDay)
            {
                bestFallbackDay = entry.day;
                fallback = entry;
            }
        }

        if (exact != null) return exact.conversationTitle;
        if (fallback != null) return fallback.conversationTitle;

        for (int i = 0; i < _dayReplicas.Length; i++)
        {
            if (_dayReplicas[i] != null && !string.IsNullOrEmpty(_dayReplicas[i].conversationTitle))
                return _dayReplicas[i].conversationTitle;
        }

        return null;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerView playerView))
        {
            _isPlayerInZone = false;
            // Не закрывать окно при выходе из зоны, пока диалог не прочитан
            if (_isViewing && !DialogueManager.isConversationActive)
            {
                ExitView();
            }
        }
    }

    /// <summary>
    /// Открывает или закрывает окно. Возвращает true, если состояние изменилось; false, если закрытие заблокировано (диалог ещё не дочитан).
    /// </summary>
    public bool ToggleView()
    {
        if (_isViewing)
        {
            // Не закрывать окно по E, пока не прочитан весь диалог
            if (DialogueManager.isConversationActive)
                return false;
            ExitView();
            return true;
        }

        EnterView();
        return true;
    }

    private void EnterView()
    {
        _isViewing = true;

        if (_fullScreenImage == null)
        {
            return;
        }

        if (_fullScreenSprite != null)
            _fullScreenImage.sprite = _fullScreenSprite;
        _fullScreenImage.gameObject.SetActive(true);

        Canvas canvas = _fullScreenImage.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            _originalCanvasSortOrder = canvas.sortingOrder;
            canvas.sortingOrder = _windowSortOrder;
        }

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        if (_fadeInDuration <= 0f)
        {
            SetImageAlpha(1f);
            TryPlayDayReplica();
            return;
        }

        SetImageAlpha(0f);
        _fadeRoutine = StartCoroutine(FadeInRoutine());
    }

    public void ExitView()
    {
        _isViewing = false;

        if (_fullScreenImage != null)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
            Canvas canvas = _fullScreenImage.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = _originalCanvasSortOrder;
            SetImageAlpha(0f);
            _fullScreenImage.gameObject.SetActive(false);
        }
    }

    public void ApplyDayVisual(int day)
    {
        Sprite selected = SelectSpriteForDay(day);
        if (selected != null)
            _fullScreenSprite = selected;

        if (_isViewing && _fullScreenImage != null && _fullScreenSprite != null)
            _fullScreenImage.sprite = _fullScreenSprite;
    }

    private Sprite SelectSpriteForDay(int day)
    {
        if (_daySprites == null || _daySprites.Length == 0)
            return _fullScreenSprite;

        WindowDaySpriteEntry exact = null;
        WindowDaySpriteEntry fallback = null;
        int bestFallbackDay = int.MinValue;

        for (int i = 0; i < _daySprites.Length; i++)
        {
            WindowDaySpriteEntry entry = _daySprites[i];
            if (entry == null || entry.day <= 0 || entry.sprite == null)
                continue;

            if (entry.day == day)
            {
                exact = entry;
                break;
            }

            if (entry.day < day && entry.day > bestFallbackDay)
            {
                bestFallbackDay = entry.day;
                fallback = entry;
            }
        }

        if (exact != null) return exact.sprite;
        if (fallback != null) return fallback.sprite;

        for (int i = 0; i < _daySprites.Length; i++)
        {
            if (_daySprites[i] != null && _daySprites[i].sprite != null)
                return _daySprites[i].sprite;
        }

        return _fullScreenSprite;
    }

    private IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeInDuration);
            SetImageAlpha(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        SetImageAlpha(1f);
        _fadeRoutine = null;
        TryPlayDayReplica();
    }

    private void SetImageAlpha(float alpha)
    {
        if (_fullScreenImage == null) return;
        Color c = _fullScreenImage.color;
        c.a = Mathf.Clamp01(alpha);
        _fullScreenImage.color = c;
    }
}

[System.Serializable]
public class WindowDaySpriteEntry
{
    public int day = 1;
    public Sprite sprite;
}

[System.Serializable]
public class WindowDayReplicaEntry
{
    public int day = 1;
    public string conversationTitle;
}