using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IntroView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _overlayGroup;
    [SerializeField] private TMP_Text _titleText;
    [Tooltip("Опционально: спрайт/картинка, которая плавно уйдёт из полностью видимой в прозрачную вместе с оверлеем.")]
    [SerializeField] private Image _spriteImage;

    public bool IsPlaying { get; private set; }

    private Coroutine _routine;

    /// <summary> Плавный переход из полностью чёрного оверлея/картинки в прозрачность (без текста). Для начала второго дня. </summary>
    public void PlayFadeFromBlack(float fadeDuration, Action onFinished)
    {
        Stop();
        if (_titleText != null)
            _titleText.gameObject.SetActive(false);
        gameObject.SetActive(true);
        _routine = StartCoroutine(PlayRoutine(fadeDuration, () =>
        {
            if (_titleText != null)
                _titleText.gameObject.SetActive(true);
            onFinished?.Invoke();
        }));
    }

    public void Play(string title, float fadeDuration, Action onFinished)
    {
        Stop();

        if (_titleText != null)
            _titleText.text = title;

        gameObject.SetActive(true);

        _routine = StartCoroutine(PlayRoutine(fadeDuration, onFinished));
    }

    public void Stop()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        IsPlaying = false;
    }

    private IEnumerator PlayRoutine(float fadeDuration, Action onFinished)
    {
        IsPlaying = true;

        if (_overlayGroup != null)
        {
            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = true;
            _overlayGroup.interactable = false;
        }

        if (_spriteImage != null)
        {
            Color c = _spriteImage.color;
            c.a = 1f;
            _spriteImage.color = c;
        }

        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float alpha = Mathf.Lerp(1f, 0f, t);

            if (_overlayGroup != null)
                _overlayGroup.alpha = alpha;

            if (_spriteImage != null)
            {
                Color c = _spriteImage.color;
                c.a = alpha;
                _spriteImage.color = c;
            }

            yield return null;
        }

        if (_overlayGroup != null)
        {
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
        }

        if (_spriteImage != null)
        {
            Color c = _spriteImage.color;
            c.a = 0f;
            _spriteImage.color = c;
        }

        IsPlaying = false;

        gameObject.SetActive(false);

        onFinished?.Invoke();
    }
}
