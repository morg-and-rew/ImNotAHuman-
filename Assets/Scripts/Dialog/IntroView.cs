using System;
using System.Collections;
using TMPro;
using UnityEngine;

public sealed class IntroView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _overlayGroup;
    [SerializeField] private TMP_Text _titleText;

    public bool IsPlaying { get; private set; }

    private Coroutine _routine;

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

        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;

            if (_overlayGroup != null)
                _overlayGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        if (_overlayGroup != null)
        {
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
        }

        IsPlaying = false;

        gameObject.SetActive(false);

        onFinished?.Invoke();
    }
}
