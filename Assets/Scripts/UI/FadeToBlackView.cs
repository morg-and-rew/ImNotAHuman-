using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen black overlay. Fades from transparent to black over duration, then invokes callback.
/// Use for "end of day" or scene transitions.
/// </summary>
public sealed class FadeToBlackView : MonoBehaviour
{
    [SerializeField] private Image _overlayImage;
    [SerializeField] private Canvas _canvas;
    [SerializeField, Min(0.01f)] private float _defaultDuration = 3f;

    private Coroutine _routine;

    private void Awake()
    {
        if (_overlayImage != null)
        {
            _overlayImage.color = new Color(0f, 0f, 0f, 0f);
            _overlayImage.raycastTarget = false;
        }
        if (_canvas != null)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32767;
            _canvas.gameObject.SetActive(false);
        }
        else if (_overlayImage != null)
            _overlayImage.gameObject.SetActive(false);
    }

    /// <summary> Fade to full black over durationSeconds, then call onComplete. </summary>
    public void Play(float durationSeconds, Action onComplete)
    {
        Stop();
        float dur = durationSeconds > 0f ? durationSeconds : _defaultDuration;
        if (_canvas != null) _canvas.gameObject.SetActive(true);
        else if (_overlayImage != null) _overlayImage.gameObject.SetActive(true);
        if (_overlayImage != null) _overlayImage.raycastTarget = true;
        _routine = StartCoroutine(FadeRoutine(dur, onComplete));
    }

    public void Stop()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator FadeRoutine(float duration, Action onComplete)
    {
        if (_overlayImage == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.SmoothStep(0f, 1f, t);
            _overlayImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        _overlayImage.color = new Color(0f, 0f, 0f, 1f);
        _routine = null;
        onComplete?.Invoke();
    }
}
