using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHintView : MonoBehaviour
{
    public static PlayerHintView Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Image _image;

    private Sprite _raycastSprite;
    private Sprite _windowSprite;
    private Sprite _doorSprite;
    private Sprite _clientSprite;
    private string _lastLoggedWinner = "";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (_root != null) _root.SetActive(false);
    }

    private void Start()
    {
        // #region agent log
        try
        {
            var logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-ffa72b.log"));
            var line = "{\"sessionId\":\"ffa72b\",\"hypothesisId\":\"H0\",\"location\":\"PlayerHintView.Start\",\"message\":\"PlayerHintView started\",\"data\":{\"logPath\":\"" + logPath.Replace("\\", "\\\\") + "\"},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n";
            File.AppendAllText(logPath, line);
            Debug.Log("[Hint] PlayerHintView.Start | Instance ready, logPath=" + logPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Hint] PlayerHintView.Start log failed: " + e.Message);
        }
        // #endregion
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

    private void LateUpdate()
    {
        // Оператор ?? (null-coalescing): берём первый не-null спрайт в порядке приоритета.
        // client → raycast (луч по предметам) → window → door. Если все четыре null, showSprite = null и рут не включится.
        // showSprite будет null, если: ClientInteraction не вызвал SetClientHint(спрайт), или вызвал SetClientHint(null);
        // и остальные системы тоже не выставили свой спрайт. Проверь порядок выполнения: SetClientHint вызывается в ClientInteraction.Update(),
        // а LateUpdate идёт после всех Update — значит к этому моменту спрайты уже должны быть установлены.
        Sprite showSprite = _clientSprite ?? _raycastSprite ?? _windowSprite ?? _doorSprite;
        // winner по первому непустому (при всех null было "client" из-за null == null)
        string winner = _clientSprite != null ? "client" : _raycastSprite != null ? "raycast" : _windowSprite != null ? "window" : _doorSprite != null ? "door" : "none";

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
            else
                _root.SetActive(false);
        }

        if (_image != null)
        {
            _image.enabled = shouldShow;
            if (showSprite != null)
                _image.sprite = showSprite;
        }

        // #region agent log
        bool shouldLog = winner != _lastLoggedWinner || (showSprite != null && Time.frameCount % 60 == 0) || Time.frameCount % 120 == 0;
        if (shouldLog)
        {
            if (winner != _lastLoggedWinner) _lastLoggedWinner = winner;
            try
            {
                var logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-ffa72b.log"));
                var line = "{\"sessionId\":\"ffa72b\",\"hypothesisId\":\"H2\",\"location\":\"PlayerHintView.LateUpdate\",\"message\":\"hint winner\",\"data\":{\"winner\":\"" + winner + "\",\"hasClient\":\"" + (_clientSprite != null) + "\",\"hasRaycast\":\"" + (_raycastSprite != null) + "\",\"rootActive\":\"" + (_root != null && _root.activeSelf) + "\"},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n";
                File.AppendAllText(logPath, line);
            }
            catch (Exception ex) { Debug.LogWarning("[Hint] View log ex: " + ex.Message); }
            Debug.Log("[Hint] View: winner=" + winner + " hasClient=" + (_clientSprite != null) + " hasRaycast=" + (_raycastSprite != null) + " rootOn=" + (_root != null && _root.activeSelf));
        }
        // #endregion
    }
}
