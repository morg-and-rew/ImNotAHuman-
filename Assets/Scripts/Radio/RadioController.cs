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
    public static event System.Action OnAnyRadioInteracted;
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
    [Header("Interaction Side")]
    [Tooltip("Требовать взаимодействие только с фронтальной стороны радио.")]
    [SerializeField] private bool _requireFrontSide = true;
    [Tooltip("Инвертировать направление фронта (если forward модели смотрит назад).")]
    [SerializeField] private bool _invertFrontSide = false;
    [Tooltip("Порог dot для проверки фронтальной стороны. 0 = полуплоскость перед радио; 0.1..0.3 = строже.")]
    [SerializeField, Range(-1f, 1f)] private float _frontSideDotThreshold = 0f;

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
    private string _playingVideoEventId;
    private bool _radioAdvanceByTimestamps;
    private float[] _radioAdvanceTimestamps;
    private int _radioAdvanceIndex;
    private float _stationBaseVolume = 1f;
    private float _voiceBaseVolume = 1f;
    private float _currentStaticVolume;
    private bool _forcedStaticOnlyMode;
    private bool _voiceHeldPausedForGamePause;
    private bool _radioVideoHeldPausedForGamePause;

    public Sprite HintSprite => _hintSprite;

    /// <summary> После LoadScene: ссылка на CustomDialogueUI могла указывать на уничтоженный объект — авто-листание и тайм-коды не работали. </summary>
    public void SyncCustomDialogueUi(CustomDialogueUI ui)
    {
        _customDialogueUI = ui != null ? ui : (_customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI);
    }

    private void Start()
    {
        _storyEvents = new List<RadioEventData>(GameConfig.RadioEvents);
        _customDialogueUI = _customDialogueUIRef ?? GameFlowController.Instance?.CustomDialogueUI;
        _stationBaseVolume = _stationSource != null ? _stationSource.volume : 1f;
        _voiceBaseVolume = _voiceSource != null ? _voiceSource.volume : 1f;
        _currentStaticVolume = _staticVolume;

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
            gfc.OnInGamePauseChanged += OnInGamePauseChanged;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.errorReceived += OnVideoError;
        }

        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationStarted += OnAnyConversationStarted;
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
            gfc.OnInGamePauseChanged -= OnInGamePauseChanged;
            UnsubscribeTeleportForVideo();
        }
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoEnded;
            _videoPlayer.prepareCompleted -= OnVideoPrepared;
            _videoPlayer.errorReceived -= OnVideoError;
        }
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationStarted -= OnAnyConversationStarted;
        SetRadioDialogueAutoAdvance(false);
        SetVideoControlLock(false);
    }

    private void OnAnyConversationStarted(Transform _)
    {
        // После старта клиентских диалогов, где по логике не должно быть помех, выключаем статик сразу.
        string title = DialogueManager.lastConversationStarted ?? string.Empty;
        if (string.Equals(title, "Client_Day1.3", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(title, "Client_day2.1", System.StringComparison.OrdinalIgnoreCase))
            StopStatic();
    }

    private void OnRadioEventActivated(string id, float? volumeOverride)
    {
        if (_storyPlaying) return;
        if (_staticPlaying)
        {
            if (_voiceSource != null)
            {
                float vol = volumeOverride ?? _currentStaticVolume;
                _currentStaticVolume = vol;
                _voiceBaseVolume = vol;
                ApplyDistanceVolumeToSources();
            }
            return;
        }
        PlayStatic(volumeOverride);
    }

    private void OnRadioStaticVolumeRequested(float volume)
    {
        _currentStaticVolume = volume;
        if (_staticPlaying && _voiceSource != null)
        {
            _voiceBaseVolume = volume;
            ApplyDistanceVolumeToSources();
        }
    }

    private void OnInGamePauseChanged(bool paused)
    {
        if (paused)
        {
            if (_voiceSource != null && _voiceSource.isPlaying)
            {
                _voiceSource.Pause();
                _voiceHeldPausedForGamePause = true;
            }

            if (_waitingVideoEnd && _videoPlayer != null && _videoPlayer.isPlaying)
            {
                _videoPlayer.Pause();
                _radioVideoHeldPausedForGamePause = true;
            }
        }
        else
        {
            if (_voiceHeldPausedForGamePause && _voiceSource != null)
            {
                _voiceSource.UnPause();
                _voiceHeldPausedForGamePause = false;
            }

            if (_radioVideoHeldPausedForGamePause && _videoPlayer != null)
            {
                _videoPlayer.Play();
                _radioVideoHeldPausedForGamePause = false;
            }
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

        // Фоновая музыка (_stationSource) не останавливается — играет всегда независимо от шипения/озвучки радио.
        // StopAllStations(); // убрано: шипение только на _voiceSource

        float vol = volumeOverride ?? _currentStaticVolume;
        _currentStaticVolume = vol;
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
        _voiceHeldPausedForGamePause = false;
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
        OnAnyRadioInteracted?.Invoke();

        if (_waitingStoryEnd || _waitingPlayerReplica)
            return;
        if (_waitingForLeaveWarehouseBeforeVideo)
            return;
        if (_waitingVideoEnd)
            return;

        if (_forcedStaticOnlyMode)
        {
            GameFlowController flowForced = GameFlowController.Instance;
            flowForced?.HideHint();
            PlayStatic();
            return;
        }

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null)
            return;
        if (!CanInteractFromPlayerSide(flow))
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

    public bool CanShowInteractionFeedback()
    {
        if (_waitingStoryEnd || _waitingPlayerReplica || _waitingForLeaveWarehouseBeforeVideo || _waitingVideoEnd)
            return false;

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null)
            return false;
        if (!CanInteractFromPlayerSide(flow))
            return false;

        if (_forcedStaticOnlyMode)
            return true;

        // Для радио показываем подсветку/иконку только когда есть доступный сюжетный запуск.
        return GetFirstAvailableStoryEvent(flow) != null;
    }

    public void SetForcedStaticOnlyMode(bool enabled)
    {
        _forcedStaticOnlyMode = enabled;
        if (!enabled)
            StopStatic();
    }

    private bool CanInteractFromPlayerSide(GameFlowController flow)
    {
        if (!_requireFrontSide)
            return true;
        if (flow == null || flow.Player == null)
            return false;

        Vector3 playerOffset = flow.Player.transform.position - transform.position;
        playerOffset.y = 0f;
        if (playerOffset.sqrMagnitude < 0.0001f)
            return true;
        playerOffset.Normalize();

        Vector3 front = transform.forward;
        front.y = 0f;
        if (front.sqrMagnitude < 0.0001f)
            return true;
        front.Normalize();
        if (_invertFrontSide)
            front = -front;

        float dot = Vector3.Dot(playerOffset, front);
        return dot >= _frontSideDotThreshold;
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
        // Фоновая музыка не останавливается при озвучке сюжета по радио.
        // StopAllStations(); // убрано

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
            flow.ReleaseTravelFadeHoldIfAny();
            flow.SetDeferTravelFadeFromBlackForDay12RadioVideo(false);
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
                {
                    gfc.OnTeleportedToClient += OnTeleportedToClientForDay12Video;
                    gfc.SetDeferTravelFadeFromBlackForDay12RadioVideo(true);
                }
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
        _playingVideoEventId = eventId;
        _teleportToTableAfterVideo = teleportToTableAfter;
        SetVideoControlLock(true);
        if (_videoRoot != null)
            _videoRoot.SetActive(true);

        StopStatic();
        // Фоновая музыка не останавливается при воспроизведении видео с радио.
        // StopAllStations(); // убрано

        _videoPlayer.loopPointReached -= OnVideoEnded;
        _videoPlayer.loopPointReached += OnVideoEnded;
        _videoPlayer.Stop();
        _videoPlayer.clip = videoClip;
        VideoRenderTextureUtil.ClearVideoTargetIfRenderTexture(_videoPlayer);
        _videoPlayer.isLooping = false;
        _videoPlayer.waitForFirstFrame = true;
        _videoPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        if (!_waitingVideoEnd) return;
        source.Play();
        GameFlowController.Instance?.NotifyRadioStoryVideoStarted(_playingVideoEventId);
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
        GameFlowController.Instance?.ReleaseTravelFadeHoldIfAny();

        _radioVideoHeldPausedForGamePause = false;
        _voiceHeldPausedForGamePause = false;

        _waitingVideoEnd = false;
        _playingVideoEventId = null;
        SetRadioDialogueAutoAdvance(false);

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

        void cleanupVideoUi()
        {
            if (_videoPlayer != null)
                _videoPlayer.loopPointReached -= OnVideoEnded;
            if (_videoRoot != null)
                _videoRoot.SetActive(false);
        }

        if (teleportToTable)
        {
            string dialogueToStart = !string.IsNullOrEmpty(postVideoConv) ? postVideoConv : "PostVideo_Day1_2";
            GameFlowController gfc = GameFlowController.Instance;
            if (gfc != null)
            {
                gfc.BeginPostRadioVideoTeleportToTable(dialogueToStart, notifyStoryCompletion, () =>
                {
                    cleanupVideoUi();
                    SetVideoControlLock(false);
                });
            }
            else
            {
                cleanupVideoUi();
                SetVideoControlLock(false);
                GameFlowController.Instance?.TeleportToTableAndFixPosition(dialogueToStart);
                if (notifyStoryCompletion)
                    GameFlowController.Instance?.NotifyRadioStoryCompleted();
            }
            return;
        }

        cleanupVideoUi();
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

        // Если уже играет эта же станция — только обновляем громкость, не перезапускаем (клик по радио не режет музыку).
        if (_stationSource.isPlaying && _stationSource.clip == entry.clip)
        {
            _stationBaseVolume = entry.volume;
            ApplyDistanceVolumeToSources();
            return;
        }

        StopAllStations();
        _stationBaseVolume = entry.volume;
        ApplyDistanceVolumeToSources();
        _stationSource.clip = entry.clip;
        _stationSource.loop = true;
        _stationSource.Play();
    }

    private void ApplyDistanceVolumeToSources()
    {
        GameAudioSettings.EnsureLoaded();
        float distanceMultiplier = GetDistanceVolumeMultiplier();
        float stationVolume = Mathf.Clamp01(_stationBaseVolume * distanceMultiplier * GameAudioSettings.MusicVolume01);
        float voiceVolume = Mathf.Clamp01(_voiceBaseVolume * distanceMultiplier * GameAudioSettings.SfxVolume01);

        if (_stationSource != null)
            _stationSource.volume = stationVolume;
        if (_voiceSource != null)
            _voiceSource.volume = voiceVolume;

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
