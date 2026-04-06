using System;
using UnityEngine;

/// <summary>
/// Цепочка Radio_Day1_2 → Player_Day1_2_Replica → видео при телепорте к клиенту → PostVideo за столом:
/// при старте видео включается <see cref="_duringVideoAndPostRoot"/>, после завершения пост-диалога он выключается и включается <see cref="_afterPostVideoRoot"/>.
/// </summary>
public sealed class PostRadioVideoCutsceneObjects : MonoBehaviour
{
    [Tooltip("Event ID видео в конфиге радио (например day1_2_radio_video). Пусто — реагировать на любой id.")]
    [SerializeField] private string _videoEventIdFilter = "day1_2_radio_video";

    [Tooltip("Имя диалога после видео (например PostVideo_Day1_2). Пусто — любой диалог после TeleportToTableAndFixPosition.")]
    [SerializeField] private string _postVideoConversationFilter = "PostVideo_Day1_2";

    [SerializeField] private GameObject _duringVideoAndPostRoot;
    [SerializeField] private GameObject _afterPostVideoRoot;

    [Tooltip("При старте сцены выключить оба root, чтобы не «горели» до кат-сцены.")]
    [SerializeField] private bool _deactivateBothOnAwake = true;

    private GameFlowController _flow;
    private bool _hooked;

    private void Awake()
    {
        if (_deactivateBothOnAwake)
        {
            if (_duringVideoAndPostRoot != null)
                _duringVideoAndPostRoot.SetActive(false);
            if (_afterPostVideoRoot != null)
                _afterPostVideoRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Update()
    {
        if (!_hooked)
            TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_hooked)
            return;
        GameFlowController flow = GameFlowController.Instance;
        if (flow == null)
            return;
        flow.OnRadioStoryVideoStarted += OnRadioVideoStarted;
        flow.OnPostVideoTableDialogueCompleted += OnPostVideoDialogueCompleted;
        _flow = flow;
        _hooked = true;
    }

    private void OnDisable()
    {
        if (_hooked && _flow != null)
        {
            _flow.OnRadioStoryVideoStarted -= OnRadioVideoStarted;
            _flow.OnPostVideoTableDialogueCompleted -= OnPostVideoDialogueCompleted;
        }
        _hooked = false;
        _flow = null;
    }

    private void OnRadioVideoStarted(string eventId)
    {
        if (!string.IsNullOrEmpty(_videoEventIdFilter)
            && !string.Equals(eventId, _videoEventIdFilter, StringComparison.OrdinalIgnoreCase))
            return;

        if (_duringVideoAndPostRoot != null)
            _duringVideoAndPostRoot.SetActive(true);
        if (_afterPostVideoRoot != null)
            _afterPostVideoRoot.SetActive(false);
    }

    private void OnPostVideoDialogueCompleted(string conversationTitle)
    {
        if (!string.IsNullOrEmpty(_postVideoConversationFilter)
            && !string.Equals(conversationTitle, _postVideoConversationFilter, StringComparison.OrdinalIgnoreCase))
            return;

        if (_duringVideoAndPostRoot != null)
            _duringVideoAndPostRoot.SetActive(false);
        if (_afterPostVideoRoot != null)
            _afterPostVideoRoot.SetActive(true);
    }
}
