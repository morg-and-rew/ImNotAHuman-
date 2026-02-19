using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public sealed class Computer : MonoBehaviour
{
    public const string KindStreet = "street";
    public const string KindIndoor = "indoor";

    [Header("Zone (подход к компу)")]
    [Tooltip("Один коллайдер-триггер: игрок в зоне и нажимает E — открывается экран с кнопками, курсор виден.")]
    [SerializeField] private Collider _zoneTrigger;

    [Header("Кнопки на мониторе (улица / запись помещения)")]
    [SerializeField] private Button _streetButton;
    [SerializeField] private Button _indoorButton;

    [Header("Video")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private GameObject _videoRoot;
    [SerializeField] private VideoClip _streetClip;
    [SerializeField] private VideoClip _indoorClip;

    private bool _isPlayerInZone;
    private bool _computerOpen;
    private bool _videoPlaying;

    /// <summary> Общее для всех экземпляров: какую кнопку разрешил сюжет (чтобы кнопки на любом компе работали). </summary>
    private static string s_allowedVideoKind;

    public bool IsPlayerInZone => _isPlayerInZone;

    /// <summary> Для обратной совместимости со старыми ComputerButtonZone; сейчас используются UI-кнопки. </summary>
    public void SetPlayerInButtonZone(string kind, bool inside) { }

    public void Initialize(UnityEngine.Camera mainCamera) { }

    private void Awake()
    {
        if (_videoRoot != null)
            _videoRoot.SetActive(false);
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached += OnVideoEnded;
            _videoPlayer.errorReceived += OnVideoError;
        }
        if (_streetButton != null)
            _streetButton.onClick.AddListener(OnStreetButtonClicked);
        if (_indoorButton != null)
            _indoorButton.onClick.AddListener(OnIndoorButtonClicked);
    }

    private void OnDestroy()
    {
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoEnded;
            _videoPlayer.errorReceived -= OnVideoError;
        }
        if (_streetButton != null)
            _streetButton.onClick.RemoveListener(OnStreetButtonClicked);
        if (_indoorButton != null)
            _indoorButton.onClick.RemoveListener(OnIndoorButtonClicked);
    }

    private void Update()
    {
        if (_computerOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (!_videoPlaying && Input.GetKeyDown(KeyCode.Escape))
                CloseComputer();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerView>(out _)) return;
        _isPlayerInZone = true;
        if (!_videoPlaying)
            OpenComputer();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
        {
            _isPlayerInZone = false;
            if (_computerOpen && !_videoPlaying)
                CloseComputer();
        }
    }

    private void OpenComputer()
    {
        if (_computerOpen) return;
        _computerOpen = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RefreshButtonStates();
    }

    /// <summary>
    /// Разрешить нажатие одной из кнопок. kind: "street" (улица), "indoor" (запись) или null — обе неактивны.
    /// Состояние общее для всех Computer в сцене (кнопки могут быть на другом объекте).
    /// </summary>
    public void SetAllowedVideoKind(string kind)
    {
        s_allowedVideoKind = string.IsNullOrEmpty(kind) ? null : kind.Trim().ToLowerInvariant();
        if (s_allowedVideoKind != null && s_allowedVideoKind != KindStreet && s_allowedVideoKind != KindIndoor)
            s_allowedVideoKind = KindIndoor;
        RefreshButtonStates();
    }

    private void RefreshButtonStates()
    {
        if (_streetButton != null)
            _streetButton.interactable = s_allowedVideoKind == KindStreet;
        if (_indoorButton != null)
            _indoorButton.interactable = s_allowedVideoKind == KindIndoor;
    }

    /// <summary>
    /// Открытие по E больше не требуется; оставлено для совместимости с PlayerCameraBob (вызов при E в зоне просто возвращает true).
    /// </summary>
    public bool TryOpenOrInteract()
    {
        return _isPlayerInZone && _computerOpen;
    }

    private void CloseComputer()
    {
        if (!_computerOpen) return;
        _computerOpen = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        GameFlowController.Instance?.SetPlayerControlBlocked(false);
    }

    private void OnStreetButtonClicked()
    {
        Debug.Log("[Computer] Street button clicked.");
        TryPlayVideo(KindStreet);
    }

    private void OnIndoorButtonClicked()
    {
        Debug.Log("[Computer] Indoor button clicked.");
        TryPlayVideo(KindIndoor);
    }

    private void TryPlayVideo(string kind)
    {
        if (_videoPlaying)
        {
            Debug.Log("[Computer] TryPlayVideo skipped: already playing.");
            return;
        }
        if (s_allowedVideoKind != kind)
        {
            Debug.Log($"[Computer] TryPlayVideo skipped: kind={kind}, allowed={s_allowedVideoKind ?? "null"}.");
            return;
        }
        VideoClip clip = kind == KindStreet ? _streetClip : _indoorClip;
        if (clip == null || _videoPlayer == null)
        {
            Debug.LogWarning($"[Computer] Video clip for '{kind}' or VideoPlayer not assigned (clip={clip != null}, player={_videoPlayer != null}).");
            return;
        }

        _videoPlaying = true;
        GameFlowController.Instance?.SetPlayerControlBlocked(true);
        if (_videoRoot != null)
            _videoRoot.SetActive(true);
        else
            Debug.LogWarning("[Computer] Video Root not assigned — экран видео не включится.");
        _videoPlayer.Stop();
        _videoPlayer.clip = clip;
        _videoPlayer.isLooping = false;
        _videoPlayer.prepareCompleted += OnVideoPrepared;
        if (_videoPlayer.targetCamera == null && _videoPlayer.targetTexture == null)
            Debug.LogWarning("[Computer] VideoPlayer has no targetCamera and no targetTexture — видео может не отображаться. Назначьте в инспекторе Render Mode и цель отрисовки.");
        _videoPlayer.Prepare();
        StartCoroutine(PlayVideoWhenReady());
        Debug.Log("[Computer] Preparing " + kind + " video (clip: " + clip.name + ").");
    }

    private IEnumerator PlayVideoWhenReady()
    {
        float timeout = 5f;
        float t = 0f;
        while (t < timeout && _videoPlaying && _videoPlayer != null && !_videoPlayer.isPlaying)
        {
            yield return null;
            t += Time.deltaTime;
        }
        if (_videoPlaying && _videoPlayer != null && !_videoPlayer.isPlaying)
        {
            Debug.Log("[Computer] Starting playback (fallback).");
            _videoPlayer.Play();
        }
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        _videoPlayer.prepareCompleted -= OnVideoPrepared;
        Debug.Log("[Computer] Video prepared, playing.");
        source.Play();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[Computer] Video error: {message}");
        FinishVideo();
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        FinishVideo();
    }

    private void FinishVideo()
    {
        if (!_videoPlaying) return;
        _videoPlaying = false;
        if (_videoPlayer != null)
            _videoPlayer.prepareCompleted -= OnVideoPrepared;
        if (_videoRoot != null)
            _videoRoot.SetActive(false);
        CloseComputer();
        GameFlowController.Instance?.NotifyComputerVideoEnded();
        Debug.Log("[Computer] Video finished.");
    }

    public void SwitchCamera() { }
}
