using UnityEngine;
using TMPro;

public sealed class FPSDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _updateInterval = 0.25f;
    [SerializeField] private string _format = "FPS: {0}";

    private float _accumulator;
    private int _frameCount;
    private float _nextUpdate;

    private void Update()
    {
        if (_text == null) return;

        _accumulator += Time.unscaledDeltaTime;
        _frameCount++;

        if (_accumulator >= _updateInterval)
        {
            int fps = Mathf.RoundToInt(_frameCount / _accumulator);
            _text.text = string.Format(_format, fps);

            _accumulator = 0f;
            _frameCount = 0;
        }
    }
}
