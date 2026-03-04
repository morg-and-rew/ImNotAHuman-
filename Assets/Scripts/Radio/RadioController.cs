using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using UnityEngine.Video;
using static IGameFlowController;

[RequireComponent(typeof(Collider))]
public sealed class RadioInteractable : MonoBehaviour, IWorldInteractable
{
    [Header("Distance Volume")]
    [SerializeField] private bool _useDistanceVolume = true;
    [SerializeField, Min(0f)] private float _fullVolumeDistance = 2f;
    [SerializeField, Min(0.01f)] private float _muteDistance = 16f;
    [SerializeField, Range(0f, 1f)] private float _minDistanceVolumeMultiplier = 0f;
    [SerializeField] private Transform _listenerOverride;

    [Header("Stations (background music, loop) — клип и громкость на каждую станцию")]
    [SerializeField] private AudioSource _stationSource;
    [SerializeField] private RadioStationEntry[] _stations = new RadioStationEntry[0];

    [Header("Event clips (Inspector)")]
    [SerializeField] private RadioEventClipEntry[] _eventClips = new RadioEventClipEntry[0];
    [Header("Event videos (Inspector)")]
    [SerializeField] private RadioEventVideoEntry[] _eventVideos = new RadioEventVideoEntry[0];
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private GameObject _videoRoot;

    [Header("Static и озвучка сюжета — один источник, клипы задаются здесь и в Event clips")]
    [SerializeField] private AudioSource _voiceSource;
    [SerializeField] private AudioClip _staticClip;
    [SerializeField, Range(0f, 1f)] private float _staticVolume = 0.2f;
    [SerializeField] private string _staticClipPathOverride;

    [Header("Gate")]
    [SerializeField] private bool _requireProviderCallForDefault = true;

    [Header("Hint")]
    [SerializeField] private Sprite _hintSprite;

    [Header("Radio Dialogue Auto-Advance")]
    [SerializeField, Min(0.1f)] private float _radioDialogueAutoAdvanceSeconds = 10f;
    [SerializeField] private CustomDialogueUI _customDialogueUIRef;

    private List<RadioEventData> _storyEvents;
    private int _currentStationIndex = -1;
    private bool _storyPlaying;
    private bool _waitingStoryEnd;
    private bool _waitingPlayerReplica;
    private bool _waitingForLeaveWarehouseBeforeVideo;
    private bool _playerWasOnWarehouse;
    private bool _playDay12VideoOnNextTeleportToClient;
    private string _day12PostVideoConversation;
    private string _pendingPostVideoConversationForPlayback;
    private bool _waitingVideoEnd;
    private bool _staticPlaying;
    private CustomDialogueUI _customDialogueUI;
    private RadioEventData _phasedStory;
    private bool _teleportToTableAfterVideo;
    private bool _playerReplicaPlayed;
    private bool _radioAdvanceByTimestamps;
    private float[] _radioAdvanceTimestamps;
    private int _radioAdvanceIndex;
    private float _stationBaseVolume = 1f;
    private float _voiceBaseVolume = 1f;
    private float _nextVolumeLogTime;
    private float _lastLoggedStationVolume = -1f;
    private float _lastLoggedVoiceVolume = -1f;

    public Sprite HintSprite => _hintSprite;

    private void Start()
    {
        _storyEvents = new List<RadioEventData>(GameConfig.RadioEvents);
        _customDialogueUI = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;
        _stationBaseVolume = _stationSource != null ? _stationSource.volume : 1f;
        _voiceBaseVolume = _voiceSource != null ? _voiceSource.volume : 1f;

        // При старте играет только станция; статик включается только после ActivateRadioEvent (подсказка «послушай радио»).
        _staticPlaying = false;
        if (_voiceSource != null)
            _voiceSource.Stop();

        EnsureStationSourceLoop();
        StopAllStations();
        if (_stations != null && _stations.Length > 0)
        {
            _currentStationIndex = 0;
            PlayCurrentStation();
        }

        GameFlowController gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnRadioEventActivated += OnRadioEventActivated;
            gfc.OnRadioStaticVolumeRequested += OnRadioStaticVolumeRequested;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.errorReceived += OnVideoError;
        }
    }

    private void Update()
    {
        ApplyDistanceVolumeToSources();

        if (!_radioAdvanceByTimestamps || !_waitingStoryEnd || _voiceSource == null || !_voiceSource.isPlaying
            || _customDialogueUI == null || _radioAdvanceTimestamps == null || _radioAdvanceIndex >= _radioAdvanceTimestamps.Length)
            return;

        float t = _voiceSource.time;
        while (_radioAdvanceIndex < _radioAdvanceTimestamps.Length && t >= _radioAdvanceTimestamps[_radioAdvanceIndex])
        {
            _customDialogueUI.OnContinueConversation();
            _radioAdvanceIndex++;
        }
    }

    private void OnDestroy()
    {
        GameFlowController gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnRadioEventActivated -= OnRadioEventActivated;
            gfc.OnRadioStaticVolumeRequested -= OnRadioStaticVolumeRequested;
            UnsubscribeTeleportForVideo();
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

    private void OnRadioEventActivated(string id, float? volumeOverride)
    {
        if (_storyPlaying) return;
        if (_staticPlaying)
        {
            if (_voiceSource != null)
            {
                float vol = volumeOverride ?? _staticVolume;
                _voiceBaseVolume = vol;
                ApplyDistanceVolumeToSources();
            }
            return;
        }
        PlayStatic(volumeOverride);
    }

    private void OnRadioStaticVolumeRequested(float volume)
    {
        if (_staticPlaying && _voiceSource != null)
        {
            _voiceBaseVolume = volume;
            ApplyDistanceVolumeToSources();
        }
    }

    private void PlayStatic(float? volumeOverride = null)
    {
        if (_voiceSource == null)
            return;

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
            return;

        StopAllStations();

        float vol = volumeOverride ?? _staticVolume;
        _voiceBaseVolume = vol;
        ApplyDistanceVolumeToSources();
        _voiceSource.clip = clip;
        _voiceSource.loop = true;
        _voiceSource.Play();
        _staticPlaying = true;
    }

    private void StopStatic()
    {
        if (!_staticPlaying || _voiceSource == null) return;
        _voiceSource.Stop();
        _staticPlaying = false;
    }

    private void EnsureStationSourceLoop()
    {
        if (_stationSource != null)
            _stationSource.loop = true;
    }

    /// <summary> Остановить воспроизведение станции. </summary>
    private void StopAllStations()
    {
        if (_stationSource != null && _stationSource.isPlaying)
            _stationSource.Stop();
    }

    public void Interact(IPlayerInput input)
    {
        if (_waitingStoryEnd || _waitingPlayerReplica)
            return;
        if (_waitingForLeaveWarehouseBeforeVideo)
            return;
        if (_waitingVideoEnd)
            return;

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null)
            return;

        RadioEventData story = GetFirstAvailableStoryEvent(flow);
        if (story != null)
        {
            PlayStoryEvent(flow, story);
            return;
        }

        if (_requireProviderCallForDefault && !flow.ProviderCallDone)
            return;

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

            _storyPlaying = false;
            PlayCurrentStation();
            return;
        }

        _storyPlaying = true;
        _phasedStory = story;
        _playerReplicaPlayed = false;
        StopStatic();
        StopAllStations();

        if (!string.IsNullOrEmpty(story.conversationTitle))
        {
            RadioEventClipEntry clipEntry = GetEventClipEntry(story.eventId);
            if (_voiceSource != null)
            {
                AudioClip clip = clipEntry != null ? clipEntry.clip : null;
                if (clip == null && !string.IsNullOrEmpty(story.audioPath))
                    clip = Resources.Load<AudioClip>(story.audioPath);
                if (clip != null)
                {
                    float vol = clipEntry != null ? clipEntry.volume : 1f;
                    _voiceBaseVolume = vol;
                    ApplyDistanceVolumeToSources();
                    _voiceSource.clip = clip;
                    _voiceSource.loop = false;
                    _voiceSource.Play();
                    Debug.Log($"[Radio] Озвучивается: сюжет — «{clip.name}» (eventId: {story.eventId}, громкость {vol})");
                }
            }

            _waitingStoryEnd = true;
            if (clipEntry != null && clipEntry.advanceAtSeconds != null && clipEntry.advanceAtSeconds.Length > 0)
            {
                _radioAdvanceByTimestamps = true;
                _radioAdvanceTimestamps = clipEntry.advanceAtSeconds;
                _radioAdvanceIndex = 0;
                SetRadioDialogueAutoAdvance(false);
                _customDialogueUI?.SetManualAdvanceBlocked(true);
            }
            else
            {
                _radioAdvanceByTimestamps = false;
                SetRadioDialogueAutoAdvance(true);
            }
            DialogueManager.instance.conversationEnded += OnStoryEnded;
            if (string.Equals(story.conversationTitle, "Radio_Day1_2", System.StringComparison.OrdinalIgnoreCase))
                GameFlowController.Instance?.NotifyRadioDay1_2Started();
            DialogueManager.StartConversation(story.conversationTitle);
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
        _radioAdvanceByTimestamps = false;
        _customDialogueUI?.SetManualAdvanceBlocked(false);

        if (_voiceSource != null && _voiceSource.isPlaying)
            _voiceSource.Stop();

        AdvancePhasedFlow();
    }

    private void OnPlayerReplicaEnded(Transform actor)
    {
        OnStoryEnded(actor);
    }

    private IEnumerator StartPlayerReplicaNextFrame(string conversationTitle)
    {
        yield return null;
        if (_waitingPlayerReplica && !string.IsNullOrEmpty(conversationTitle) && DialogueManager.instance != null)
            DialogueManager.StartConversation(conversationTitle);
    }

    private void OnTeleportedToWarehouseForVideo()
    {
        if (_waitingForLeaveWarehouseBeforeVideo)
            _playerWasOnWarehouse = true;
    }

    private void OnTeleportedToClientForDay12Video()
    {
        if (!_playDay12VideoOnNextTeleportToClient) return;
        _playDay12VideoOnNextTeleportToClient = false;
        GameFlowController flow = GameFlowController.Instance;
        if (flow != null)
            flow.OnTeleportedToClient -= OnTeleportedToClientForDay12Video;
        VideoClip video = GetEventVideo("day1_2_radio_video");
        if (video == null || _videoPlayer == null)
            return;
        _pendingPostVideoConversationForPlayback = !string.IsNullOrEmpty(_day12PostVideoConversation) ? _day12PostVideoConversation : "PostVideo_Day1_2";
        _day12PostVideoConversation = null;
        GameFlowController.Instance?.TeleportToClientCounter();
        PlayEventVideo("day1_2_radio_video", video, teleportToTableAfter: true);
    }

    private void OnTeleportedToClientForVideo()
    {
        if (!_waitingForLeaveWarehouseBeforeVideo || !_playerWasOnWarehouse || _phasedStory == null) return;
        UnsubscribeTeleportForVideo();
        _waitingForLeaveWarehouseBeforeVideo = false;
        _playerWasOnWarehouse = false;
        GameFlowController.Instance?.TeleportToClientCounter();
        PlayPhasedVideo();
    }

    private void UnsubscribeTeleportForVideo()
    {
        GameFlowController flow = GameFlowController.Instance;
        if (flow != null)
        {
            flow.OnTeleportedToWarehouse -= OnTeleportedToWarehouseForVideo;
            flow.OnTeleportedToClient -= OnTeleportedToClientForVideo;
            flow.OnTeleportedToClient -= OnTeleportedToClientForDay12Video;
        }
        _playDay12VideoOnNextTeleportToClient = false;
        _day12PostVideoConversation = null;
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
            // Запуск реплики героя на следующий кадр, чтобы Dialogue System успел завершить предыдущий разговор
            string replicaConv = _phasedStory.playerReplicaConversation;
            StartCoroutine(StartPlayerReplicaNextFrame(replicaConv));
            return;
        }

        if (needsExitZone && hasVideo)
        {
            if (string.Equals(_phasedStory.eventId, "day1_2_radio_video", System.StringComparison.OrdinalIgnoreCase))
            {
                GameFlowController.Instance?.NotifyPlayerDay1_2ReplicaCompleted(null);
                string postConv = !string.IsNullOrEmpty(_phasedStory.postVideoConversation) ? _phasedStory.postVideoConversation : "PostVideo_Day1_2";
                PhasedCleanup();
                _playDay12VideoOnNextTeleportToClient = true;
                _day12PostVideoConversation = postConv;
                var gfc = GameFlowController.Instance;
                if (gfc != null)
                    gfc.OnTeleportedToClient += OnTeleportedToClientForDay12Video;
                return;
            }
            _waitingForLeaveWarehouseBeforeVideo = true;
            _playerWasOnWarehouse = false;
            GameFlowController flow = GameFlowController.Instance;
            if (flow != null)
            {
                flow.OnTeleportedToWarehouse += OnTeleportedToWarehouseForVideo;
                flow.OnTeleportedToClient += OnTeleportedToClientForVideo;
            }
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
        UnsubscribeTeleportForVideo();
        _waitingForLeaveWarehouseBeforeVideo = false;
        _playerWasOnWarehouse = false;
        _phasedStory = null;
        _pendingPostVideoConversationForPlayback = null;
        _playerReplicaPlayed = false;
        _storyPlaying = false;
        StopStatic();
        PlayCurrentStation();
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
        if (_videoRoot != null)
            _videoRoot.SetActive(true);

        StopStatic();
        StopAllStations();

        _videoPlayer.loopPointReached -= OnVideoEnded;
        _videoPlayer.loopPointReached += OnVideoEnded;
        _videoPlayer.Stop();
        _videoPlayer.clip = videoClip;
        _videoPlayer.isLooping = false;
        _videoPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        if (!_waitingVideoEnd) return;
        source.Play();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        FinishVideoPlayback(notifyStoryCompletion: true);
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        FinishVideoPlayback(notifyStoryCompletion: true);
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
        string postVideoConv = _phasedStory?.postVideoConversation ?? _pendingPostVideoConversationForPlayback;
        _teleportToTableAfterVideo = false;
        _phasedStory = null;
        _pendingPostVideoConversationForPlayback = null;

        _storyPlaying = false;
        if (_voiceSource != null && _voiceSource.isPlaying)
            _voiceSource.Stop();

        StopStatic();
        PlayCurrentStation();

        if (teleportToTable)
        {
            string dialogueToStart = !string.IsNullOrEmpty(postVideoConv) ? postVideoConv : "PostVideo_Day1_2";
            SetVideoControlLock(false);
            GameFlowController.Instance?.TeleportToTableAndFixPosition(dialogueToStart);
        }
        else
            SetVideoControlLock(false);
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

        _currentStationIndex = (_currentStationIndex + 1) % _stations.Length;
        PlayCurrentStation();
    }

    private void PlayCurrentStation()
    {
        if (_stationSource == null || _stations == null || _currentStationIndex < 0 || _currentStationIndex >= _stations.Length) return;
        RadioStationEntry entry = _stations[_currentStationIndex];
        if (entry?.clip == null) return;
        StopAllStations();
        _stationBaseVolume = entry.volume;
        ApplyDistanceVolumeToSources();
        _stationSource.clip = entry.clip;
        _stationSource.loop = true;
        _stationSource.Play();
        Debug.Log($"[Radio] Озвучивается: станция [{_currentStationIndex}] — «{entry.clip.name}» (громкость {entry.volume})");
    }

    private void ApplyDistanceVolumeToSources()
    {
        float distanceMultiplier = GetDistanceVolumeMultiplier();
        float stationVolume = Mathf.Clamp01(_stationBaseVolume * distanceMultiplier);
        float voiceVolume = Mathf.Clamp01(_voiceBaseVolume * distanceMultiplier);

        if (_stationSource != null)
            _stationSource.volume = stationVolume;
        if (_voiceSource != null)
            _voiceSource.volume = voiceVolume;

        LogCurrentVolumes(stationVolume, voiceVolume);
    }

    private float GetDistanceVolumeMultiplier()
    {
        if (!_useDistanceVolume)
            return 1f;

        Transform listener = GetListenerTransform();
        if (listener == null)
            return 1f;

        float nearDistance = Mathf.Min(_fullVolumeDistance, _muteDistance);
        float farDistance = Mathf.Max(_fullVolumeDistance, _muteDistance);
        float distance = Vector3.Distance(transform.position, listener.position);
        float t = Mathf.InverseLerp(farDistance, nearDistance, distance);
        return Mathf.Lerp(_minDistanceVolumeMultiplier, 1f, t);
    }

    private Transform GetListenerTransform()
    {
        if (_listenerOverride != null)
            return _listenerOverride;

        GameFlowController flow = GameFlowController.Instance;
        if (flow != null)
        {
            if (flow.PlayerCamera != null)
                return flow.PlayerCamera.transform;
            if (flow.Player != null)
                return flow.Player.transform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    private void LogCurrentVolumes(float stationVolume, float voiceVolume)
    {
        // Логируем не чаще раза в 0.5 сек и только если громкость заметно изменилась.
        if (Time.time < _nextVolumeLogTime)
            return;

        bool stationChanged = Mathf.Abs(stationVolume - _lastLoggedStationVolume) >= 0.01f;
        bool voiceChanged = Mathf.Abs(voiceVolume - _lastLoggedVoiceVolume) >= 0.01f;
        if (!stationChanged && !voiceChanged)
            return;

        _nextVolumeLogTime = Time.time + 0.5f;
        _lastLoggedStationVolume = stationVolume;
        _lastLoggedVoiceVolume = voiceVolume;

        Debug.Log($"[Radio] Current volume -> station: {stationVolume:0.00}, voice/static: {voiceVolume:0.00}");
    }

    private RadioEventClipEntry GetEventClipEntry(string eventId)
    {
        if (_eventClips == null || string.IsNullOrEmpty(eventId)) return null;
        return _eventClips.FirstOrDefault(e => e != null && string.Equals(e.eventId, eventId, System.StringComparison.OrdinalIgnoreCase));
    }

    private AudioClip GetEventClip(string eventId)
    {
        var entry = GetEventClipEntry(eventId);
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
public class RadioStationEntry
{
    [Tooltip("Клип фоновой музыки станции (loop).")]
    public AudioClip clip;
    [Range(0f, 1f), Tooltip("Громкость этой станции.")]
    public float volume = 1f;
}

[System.Serializable]
public class RadioEventClipEntry
{
    public string eventId;
    public AudioClip clip;
    [Range(0f, 1f), Tooltip("Громкость озвучки этого события.")]
    public float volume = 1f;
    [Tooltip("Секунды на таймлайне озвучки, в которые переключать реплику диалога: [0] — переход с 1-й на 2-ю реплику, [1] — со 2-й на 3-ю и т.д. Если пусто — используется фиксированный интервал авто-листания.")]
    public float[] advanceAtSeconds = new float[0];
}

[System.Serializable]
public class RadioEventVideoEntry
{
    public string eventId;
    public VideoClip clip;
}
