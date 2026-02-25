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

    public Sprite HintSprite => _hintSprite;

    private void Start()
    {
        _storyEvents = new List<RadioEventData>(GameConfig.RadioEvents);
        _customDialogueUI = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;

        // При старте играет только станция; статик включается только после ActivateRadioEvent (подсказка «послушай радио»).
        _staticPlaying = false;
        if (_staticAudioSource != null)
            _staticAudioSource.Stop();

        EnsureStationsLoop();
        if (_stations != null && _stations.Length > 0)
        {
            _currentStationIndex = 0;
            PlayCurrentStation();
        }

        GameFlowController gfc = GameFlowController.Instance;
        if (gfc != null)
            gfc.OnRadioEventActivated += OnRadioEventActivated;

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.errorReceived += OnVideoError;
        }
    }

    private void Update()
    {
        if (!_radioAdvanceByTimestamps || !_waitingStoryEnd || _storyAudioSource == null || !_storyAudioSource.isPlaying
            || _customDialogueUI == null || _radioAdvanceTimestamps == null || _radioAdvanceIndex >= _radioAdvanceTimestamps.Length)
            return;

        float t = _storyAudioSource.time;
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

    private void OnRadioEventActivated(string id)
    {
        if (_staticPlaying || _storyPlaying) return;
        PlayStatic();
    }

    private void PlayStatic()
    {
        if (_staticAudioSource == null)
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

        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying) s.Stop();
        }

        _staticAudioSource.clip = clip;
        _staticAudioSource.loop = true;
        _staticAudioSource.Play();
        _staticPlaying = true;
    }

    private void StopStatic()
    {
        if (!_staticPlaying || _staticAudioSource == null) return;
        _staticAudioSource.Stop();
        _staticPlaying = false;
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
        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying)
                s.Stop();
        }

        if (!string.IsNullOrEmpty(story.conversationTitle))
        {
            RadioEventClipEntry clipEntry = GetEventClipEntry(story.eventId);
            if (_storyAudioSource != null)
            {
                AudioClip clip = clipEntry != null ? clipEntry.clip : null;
                if (clip == null && !string.IsNullOrEmpty(story.audioPath))
                    clip = Resources.Load<AudioClip>(story.audioPath);
                if (clip != null)
                {
                    _storyAudioSource.clip = clip;
                    _storyAudioSource.loop = false;
                    _storyAudioSource.Play();
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

        if (_storyAudioSource != null && _storyAudioSource.isPlaying)
            _storyAudioSource.Stop();

        AdvancePhasedFlow();
    }

    private void OnPlayerReplicaEnded(Transform actor)
    {
        OnStoryEnded(actor);
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
            DialogueManager.StartConversation(_phasedStory.playerReplicaConversation);
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
        if (_storyAudioSource != null && _storyAudioSource.isPlaying)
            _storyAudioSource.Stop();

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

        if (_currentStationIndex >= 0 && _currentStationIndex < _stations.Length)
        {
            AudioSource s = _stations[_currentStationIndex];
            if (s != null && s.isPlaying) s.Stop();
        }

        _currentStationIndex = (_currentStationIndex + 1) % _stations.Length;
        PlayCurrentStation();
    }

    private void PlayCurrentStation()
    {
        if (_stations == null || _currentStationIndex < 0 || _currentStationIndex >= _stations.Length) return;
        AudioSource s = _stations[_currentStationIndex];
        if (s != null) s.Play();
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
public class RadioEventClipEntry
{
    public string eventId;
    public AudioClip clip;
    [Tooltip("Секунды на таймлайне озвучки, в которые переключать реплику диалога: [0] — переход с 1-й на 2-ю реплику, [1] — со 2-й на 3-ю и т.д. Если пусто — используется фиксированный интервал авто-листания.")]
    public float[] advanceAtSeconds = new float[0];
}

[System.Serializable]
public class RadioEventVideoEntry
{
    public string eventId;
    public VideoClip clip;
}
