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
    [Tooltip("Ролик «улица» (по умолчанию RU / основной язык).")]
    [SerializeField] private VideoClip _streetClip;
    [Tooltip("Ролик «помещение» (по умолчанию RU / основной язык). Для дня 1 — шаг watch_computer_indoor_day1_5, kind indoor.")]
    [SerializeField] private VideoClip _indoorClip;
    [Tooltip("Опционально: отдельный ролик «помещение» для дня 2+. Если пусто — используется Indoor Clip.")]
    [SerializeField] private VideoClip _indoorClipDay2;
    [Tooltip("Опционально: «улица» для английского UI. Если пусто — используется Street Clip.")]
    [SerializeField] private VideoClip _streetClipEnglish;
    [Tooltip("Опционально: «помещение» для английского UI. Если пусто — используется Indoor Clip.")]
    [SerializeField] private VideoClip _indoorClipEnglish;

    private bool _isPlayerInZone;
    private bool _computerOpen;
    private bool _videoPlaying;

    private static string s_allowedVideoKind;

    public bool IsPlayerInZone => _isPlayerInZone;

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

    public bool TryOpenOrInteract()
    {
        return _isPlayerInZone && _computerOpen;
    }

    public bool TryPlayAllowedVideoImmediately()
    {
        if (_videoPlaying)
            return false;

        string kind = string.IsNullOrEmpty(s_allowedVideoKind) ? KindIndoor : s_allowedVideoKind;
        if (kind != KindStreet && kind != KindIndoor)
            kind = KindIndoor;

        TryPlayVideo(kind);
        return _videoPlaying;
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
        TryPlayVideo(KindStreet);
    }

    private void OnIndoorButtonClicked()
    {
        TryPlayVideo(KindIndoor);
    }

    private VideoClip ResolveClipForCurrentLanguage(string kind)
    {
        bool english = GameFlowController.Instance != null && GameFlowController.Instance.IsUiEnglishLocale;
        if (string.Equals(kind, KindStreet, System.StringComparison.OrdinalIgnoreCase))
        {
            if (english && _streetClipEnglish != null)
                return _streetClipEnglish;
            return _streetClip;
        }
        if (string.Equals(kind, KindIndoor, System.StringComparison.OrdinalIgnoreCase))
        {
            bool isDay2OrLater = GameFlowController.Instance != null && GameFlowController.Instance.IsDay2OrLater();
            if (isDay2OrLater && _indoorClipDay2 != null)
                return _indoorClipDay2;
            if (english && _indoorClipEnglish != null)
                return _indoorClipEnglish;
            return _indoorClip;
        }
        return null;
    }

    private void TryPlayVideo(string kind)
    {
        if (_videoPlaying)
            return;
        if (s_allowedVideoKind != kind)
            return;
        VideoClip clip = ResolveClipForCurrentLanguage(kind);
        if (clip == null || _videoPlayer == null)
            return;

        _videoPlaying = true;
        if (PlayerHintView.Instance != null)
            PlayerHintView.Instance.SetSuspended(true);
        GameFlowController.Instance?.SetPlayerControlBlocked(true);
        if (_videoRoot != null)
            _videoRoot.SetActive(true);
        _videoPlayer.Stop();
        _videoPlayer.clip = clip;
        _videoPlayer.isLooping = false;
        _videoPlayer.prepareCompleted += OnVideoPrepared;
        _videoPlayer.Prepare();
        StartCoroutine(PlayVideoWhenReady());
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
            _videoPlayer.Play();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        _videoPlayer.prepareCompleted -= OnVideoPrepared;
        source.Play();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
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
        if (PlayerHintView.Instance != null)
            PlayerHintView.Instance.SetSuspended(false);
        GameFlowController.Instance?.NotifyComputerVideoEnded();
    }

    public void SwitchCamera() { }
}
