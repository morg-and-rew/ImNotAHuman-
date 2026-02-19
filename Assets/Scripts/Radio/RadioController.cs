using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using UnityEngine.Video;
using static IGameFlowController;

[RequireComponent(typeof(Collider))]
public sealed class RadioInteractable : MonoBehaviour, IWorldInteractable
{
    [Header("Stations (background music, loop)")]
    [SerializeField] private AudioSource[] _stations = new AudioSource[0];

    [Header("Story voice (one-shot)")]
    [SerializeField] private AudioSource _storyAudioSource;
    [Header("Event clips (Inspector)")]
    [SerializeField] private RadioEventClipEntry[] _eventClips = new RadioEventClipEntry[0];
    [Header("Event videos (Inspector)")]
    [SerializeField] private RadioEventVideoEntry[] _eventVideos = new RadioEventVideoEntry[0];
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private GameObject _videoRoot;

    [Header("Static (before story moment)")]
    [SerializeField] private AudioSource _staticAudioSource;
    [SerializeField] private AudioClip _staticClip;
    [SerializeField] private string _staticClipPathOverride;

    [Header("Gate")]
    [SerializeField] private bool _requireProviderCallForDefault = true;

    [Header("UI")]
    [SerializeField] private Canvas _hintCanvas;
    [Header("Radio Dialogue Auto-Advance")]
    [SerializeField, Min(0.1f)] private float _radioDialogueAutoAdvanceSeconds = 10f;
    [SerializeField] private CustomDialogueUI _customDialogueUIRef;

    private List<RadioEventData> _storyEvents;
    private int _currentStationIndex = -1;
    private bool _storyPlaying;
    private bool _waitingStoryEnd;
    private bool _waitingPlayerReplica;
    private bool _waitingExitZone;
    private bool _waitingVideoEnd;
    private bool _staticPlaying;
    private CustomDialogueUI _customDialogueUI;
    private RadioEventData _phasedStory;
    private string _phasedExitZoneId;
    private bool _teleportToTableAfterVideo;
    private bool _playerReplicaPlayed;

    public Canvas hint => _hintCanvas;

    private void Start()
    {
        _storyEvents = new List<RadioEventData>(GameConfig.RadioEvents);
        Debug.Log($"[Radio] Loaded {_storyEvents.Count} story events from config.");
        _customDialogueUI = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;

        EnsureStationsLoop();
        if (_stations != null && _stations.Length > 0)
        {
            _currentStationIndex = 0;
            PlayCurrentStation();
            Debug.Log("[Radio] Started background music, station 0.");
        }
        else
        {
            Debug.LogWarning("[Radio] No stations assigned. Music will not play.");
        }

        GameFlowController gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnRadioEventActivated += OnRadioEventActivated;
            gfc.OnExitZonePassed += OnExitZonePassed;
        }
        else
        {
            Debug.LogWarning("[Radio] GameFlowController.Instance is null at Start, will not receive static signal.");
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.errorReceived += OnVideoError;
        }
    }

    private void OnDestroy()
    {
        GameFlowController gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnRadioEventActivated -= OnRadioEventActivated;
            gfc.OnExitZonePassed -= OnExitZonePassed;
        }
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoEnded;
            _videoPlayer.prepareCompleted -= OnVideoPrepared;
            _videoPlayer.errorReceived -= OnVideoError;
        }
        SetRadioDialogueAutoAdvance(false);
        SetVideoControlLock(false);
    }

    private void OnRadioEventActivated(string id)
    {
        if (_staticPlaying || _storyPlaying) return;
        PlayStatic();
    }

    private void PlayStatic()
    {
        if (_staticAudioSource == null)
        {
            Debug.LogWarning("[Radio] Static: Static Audio Source not assigned in inspector.");
            return;
        }

        AudioClip clip = _staticClip;
        if (clip == null)
        {
            string path = !string.IsNullOrEmpty(_staticClipPathOverride)
                ? _staticClipPathOverride
                : GameConfig.RadioStaticPath;
            if (!string.IsNullOrEmpty(path))
                clip = Resources.Load<AudioClip>(path);
        }
        if (clip == null)
        {
            Debug.LogWarning("[Radio] Static: assign Static Clip in inspector or set path in GameConfig.json.");
            return;
        }

        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying) s.Stop();
        }

        _staticAudioSource.clip = clip;
        _staticAudioSource.loop = true;
        _staticAudioSource.Play();
        _staticPlaying = true;
        Debug.Log($"[Radio] Playing static (signal to approach radio).");
    }

    private void StopStatic()
    {
        if (!_staticPlaying || _staticAudioSource == null) return;
        _staticAudioSource.Stop();
        _staticPlaying = false;
        Debug.Log("[Radio] Stopped static.");
    }

    private void EnsureStationsLoop()
    {
        if (_stations == null) return;
        for (int i = 0; i < _stations.Length; i++)
        {
            if (_stations[i] != null)
                _stations[i].loop = true;
        }
    }

    public void Interact(IPlayerInput input)
    {
        if (_waitingStoryEnd || _waitingPlayerReplica)
        {
            Debug.Log("[Radio] Ignored E: story dialogue in progress.");
            return;
        }
        if (_waitingExitZone)
        {
            Debug.Log("[Radio] Ignored E: waiting for exit zone.");
            return;
        }
        if (_waitingVideoEnd)
        {
            Debug.Log("[Radio] Ignored E: story video in progress.");
            return;
        }

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null)
        {
            Debug.LogWarning("[Radio] GameFlowController.Instance is null.");
            return;
        }

        RadioEventData story = GetFirstAvailableStoryEvent(flow);
        if (story != null)
        {
            Debug.Log($"[Radio] Story event available: {story.eventId}. Playing.");
            PlayStoryEvent(flow, story);
            return;
        }

        if (_storyEvents != null && _storyEvents.Count > 0)
            Debug.Log("[Radio] E pressed but no story event active. Story must run first (autoStartStory or E at client).");

        if (_requireProviderCallForDefault && !flow.ProviderCallDone)
        {
            Debug.Log("[Radio] Ignored E: provider call not done (default gate).");
            return;
        }

        flow.HideHint();
        SwitchToNextStation();
    }

    private RadioEventData GetFirstAvailableStoryEvent(GameFlowController flow)
    {
        if (_storyEvents == null || _storyEvents.Count == 0) return null;

        List<RadioEventData> sorted = new List<RadioEventData>(_storyEvents);
        sorted.Sort((a, b) => b.priority.CompareTo(a.priority));

        for (int i = 0; i < sorted.Count; i++)
        {
            RadioEventData ev = sorted[i];
            if (string.IsNullOrEmpty(ev.eventId)) continue;
            if (flow.IsRadioEventAvailable(ev.eventId)) return ev;
        }
        return null;
    }

    private void PlayStoryEvent(GameFlowController flow, RadioEventData story)
    {
        flow.ConsumeRadioEvent(story.eventId);
        flow.HideHint();

        bool hasVideo = GetEventVideo(story.eventId) != null && _videoPlayer != null;
        bool needsExitZone = story.requireExitZoneBeforeVideo && hasVideo;
        bool hasPlayerReplica = !string.IsNullOrEmpty(story.playerReplicaConversation);

        if (string.IsNullOrEmpty(story.conversationTitle) && !needsExitZone && !hasPlayerReplica)
        {
            VideoClip videoWithoutConversation = GetEventVideo(story.eventId);
            if (videoWithoutConversation != null && _videoPlayer != null)
            {
                PlayEventVideo(story.eventId, videoWithoutConversation, teleportToTableAfter: false);
                return;
            }

            Debug.LogWarning($"[Radio] Event '{story.eventId}' has no conversation and no video.");
            _storyPlaying = false;
            PlayCurrentStation();
            return;
        }

        _storyPlaying = true;
        _phasedStory = story;
        _playerReplicaPlayed = false;
        StopStatic();
        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying)
            {
                s.Stop();
                Debug.Log("[Radio] Stopped background music for story.");
            }
        }

        if (!string.IsNullOrEmpty(story.conversationTitle))
        {
            if (_storyAudioSource != null)
            {
                AudioClip clip = GetEventClip(story.eventId);
                if (clip == null && !string.IsNullOrEmpty(story.audioPath))
                    clip = Resources.Load<AudioClip>(story.audioPath);
                if (clip != null)
                {
                    _storyAudioSource.clip = clip;
                    _storyAudioSource.loop = false;
                    _storyAudioSource.Play();
                    Debug.Log($"[Radio] Playing story voice: {story.eventId}");
                }
            }

            _waitingStoryEnd = true;
            SetRadioDialogueAutoAdvance(true);
            DialogueManager.instance.conversationEnded += OnStoryEnded;
            DialogueManager.StartConversation(story.conversationTitle);
            Debug.Log($"[Radio] Started conversation: {story.conversationTitle}");
            return;
        }

        AdvancePhasedFlow();
    }

    private void OnStoryEnded(Transform actor)
    {
        if (!_waitingStoryEnd && !_waitingPlayerReplica) return;

        if (_waitingStoryEnd)
        {
            _waitingStoryEnd = false;
            DialogueManager.instance.conversationEnded -= OnStoryEnded;
        }
        if (_waitingPlayerReplica)
        {
            _waitingPlayerReplica = false;
            DialogueManager.instance.conversationEnded -= OnPlayerReplicaEnded;
        }
        SetRadioDialogueAutoAdvance(false);

        if (_storyAudioSource != null && _storyAudioSource.isPlaying)
        {
            _storyAudioSource.Stop();
            Debug.Log("[Radio] Stopped story voice.");
        }

        AdvancePhasedFlow();
    }

    private void OnPlayerReplicaEnded(Transform actor)
    {
        OnStoryEnded(actor);
    }

    private void OnExitZonePassed(string zoneId)
    {
        if (!_waitingExitZone || _phasedStory == null) return;
        string expected = string.IsNullOrEmpty(_phasedExitZoneId) ? "warehouse_exit_passed" : _phasedExitZoneId;
        if (!string.Equals(zoneId, expected, System.StringComparison.OrdinalIgnoreCase))
            return;

        _waitingExitZone = false;
        GameFlowController.Instance.OnExitZonePassed -= OnExitZonePassed;
        Debug.Log("[Radio] Exit zone passed. Playing video.");
        PlayPhasedVideo();
    }

    private void AdvancePhasedFlow()
    {
        if (_phasedStory == null)
        {
            PhasedCleanup();
            return;
        }

        bool hasPlayerReplica = !string.IsNullOrEmpty(_phasedStory.playerReplicaConversation);
        bool needsExitZone = _phasedStory.requireExitZoneBeforeVideo;
        bool hasVideo = GetEventVideo(_phasedStory.eventId) != null && _videoPlayer != null;

        if (hasPlayerReplica && !_playerReplicaPlayed)
        {
            _playerReplicaPlayed = true;
            _waitingPlayerReplica = true;
            SetRadioDialogueAutoAdvance(false);
            DialogueManager.instance.conversationEnded += OnPlayerReplicaEnded;
            DialogueManager.StartConversation(_phasedStory.playerReplicaConversation);
            Debug.Log($"[Radio] Started player replica: {_phasedStory.playerReplicaConversation}");
            return;
        }

        if (needsExitZone && hasVideo)
        {
            _phasedExitZoneId = string.IsNullOrEmpty(_phasedStory.exitZoneId) ? "warehouse_exit_passed" : _phasedStory.exitZoneId;
            _waitingExitZone = true;
            GameFlowController flow = GameFlowController.Instance;
            if (flow != null)
                flow.OnExitZonePassed += OnExitZonePassed;
            Debug.Log("[Radio] Waiting for exit zone. No teleport.");
            return;
        }

        if (hasVideo)
        {
            PlayPhasedVideo();
            return;
        }

        PhasedCleanup();
    }

    private void PlayPhasedVideo()
    {
        if (_phasedStory == null) return;
        VideoClip video = GetEventVideo(_phasedStory.eventId);
        if (video == null || _videoPlayer == null)
        {
            PhasedCleanup();
            return;
        }
        PlayEventVideo(_phasedStory.eventId, video, teleportToTableAfter: true);
    }

    private void PhasedCleanup()
    {
        _phasedStory = null;
        _playerReplicaPlayed = false;
        _storyPlaying = false;
        StopStatic();
        PlayCurrentStation();
        Debug.Log("[Radio] Resumed background music.");
        GameFlowController.Instance?.NotifyRadioStoryCompleted();
    }

    private void PlayEventVideo(string eventId, VideoClip videoClip, bool teleportToTableAfter = false)
    {
        if (_videoPlayer == null || videoClip == null)
            return;

        _storyPlaying = true;
        _waitingVideoEnd = true;
        _teleportToTableAfterVideo = teleportToTableAfter;
        SetVideoControlLock(true);
        if (_videoRoot != null) _videoRoot.SetActive(true);

        StopStatic();
        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying) s.Stop();
        }

        _videoPlayer.loopPointReached -= OnVideoEnded;
        _videoPlayer.loopPointReached += OnVideoEnded;
        _videoPlayer.Stop();
        _videoPlayer.clip = videoClip;
        _videoPlayer.isLooping = false;
        _videoPlayer.Prepare();
        Debug.Log($"[Radio] Preparing event video: {eventId}. RenderMode={_videoPlayer.renderMode}, TargetTexture={_videoPlayer.targetTexture}, TargetCamera={_videoPlayer.targetCamera}");
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        if (!_waitingVideoEnd) return;
        source.Play();
        Debug.Log("[Radio] Event video playback started.");
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[Radio] VideoPlayer error: {message}");
        FinishVideoPlayback(notifyStoryCompletion: true);
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        FinishVideoPlayback(notifyStoryCompletion: true);
        Debug.Log("[Radio] Event video finished.");
    }

    private void FinishVideoPlayback(bool notifyStoryCompletion)
    {
        _waitingVideoEnd = false;
        SetRadioDialogueAutoAdvance(false);
        if (_videoPlayer != null)
            _videoPlayer.loopPointReached -= OnVideoEnded;
        if (_videoRoot != null)
            _videoRoot.SetActive(false);

        bool teleportToTable = _teleportToTableAfterVideo;
        string postVideoConv = _phasedStory?.postVideoConversation;
        _teleportToTableAfterVideo = false;
        _phasedStory = null;

        SetVideoControlLock(false);
        _storyPlaying = false;
        if (_storyAudioSource != null && _storyAudioSource.isPlaying)
            _storyAudioSource.Stop();

        StopStatic();
        PlayCurrentStation();

        if (teleportToTable)
            GameFlowController.Instance?.TeleportToTableAndFixPosition(postVideoConv);
        if (notifyStoryCompletion)
            GameFlowController.Instance?.NotifyRadioStoryCompleted();
    }

    private void SetRadioDialogueAutoAdvance(bool enabled)
    {
        if (_customDialogueUI == null)
            _customDialogueUI = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;
        if (_customDialogueUI == null) return;

        _customDialogueUI.SetForcedAutoAdvance(enabled, _radioDialogueAutoAdvanceSeconds);
    }

    private void SetVideoControlLock(bool isLocked)
    {
        GameFlowController flow = GameFlowController.Instance;
        if (flow == null) return;
        flow.SetPlayerControlBlocked(isLocked);
    }

    private void SwitchToNextStation()
    {
        if (_stations == null || _stations.Length == 0 || _storyPlaying) return;
        StopStatic();

        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying) s.Stop();
        }

        _currentStationIndex = (_currentStationIndex + 1) % _stations.Length;
        PlayCurrentStation();
        Debug.Log($"[Radio] Switched to station {_currentStationIndex}.");
    }

    private void PlayCurrentStation()
    {
        if (_stations == null || _currentStationIndex < 0 || _currentStationIndex >= _stations.Length) return;
        AudioSource s = _stations[_currentStationIndex];
        if (s != null) s.Play();
    }

    private AudioClip GetEventClip(string eventId)
    {
        if (_eventClips == null || string.IsNullOrEmpty(eventId)) return null;
        var entry = _eventClips.FirstOrDefault(e => e != null && string.Equals(e.eventId, eventId, System.StringComparison.OrdinalIgnoreCase));
        return entry?.clip;
    }

    private VideoClip GetEventVideo(string eventId)
    {
        if (_eventVideos == null || string.IsNullOrEmpty(eventId)) return null;
        var entry = _eventVideos.FirstOrDefault(e => e != null && string.Equals(e.eventId, eventId, System.StringComparison.OrdinalIgnoreCase));
        return entry?.clip;
    }
}

[System.Serializable]
public class RadioEventClipEntry
{
    public string eventId;
    public AudioClip clip;
}

[System.Serializable]
public class RadioEventVideoEntry
{
    public string eventId;
    public VideoClip clip;
}
