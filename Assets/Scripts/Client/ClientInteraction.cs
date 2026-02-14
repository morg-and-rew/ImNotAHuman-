using PixelCrushers.DialogueSystem;
using System;
using UnityEngine;
using UnityEngine.UI;

public struct ClientDialogueStepCompletionData
{
    public string ClientId { get; }
    public string ConversationTitle { get; }
    public object ResultData { get; }

    public ClientDialogueStepCompletionData(string clientId, string conversationTitle, object resultData = null)
    {
        ClientId = clientId;
        ConversationTitle = conversationTitle;
        ResultData = resultData;
    }
}

public sealed class ClientInteraction : MonoBehaviour, IClientInteraction
{
    [Header("Dialogue Sequence")]
    [SerializeField] private ClientPortraitMap _portraitMap;

    [Header("UI Root")]
    [SerializeField] private Canvas _uiRoot;
    [SerializeField] private Canvas _hintCanvas;

    [Header("Portraits (Left/Right)")]
    [SerializeField] private RectTransform _leftRoot;
    [SerializeField] private RectTransform _rightRoot;
    [SerializeField] private Image _leftImage;    //перемещение вот тут делать
    [SerializeField] private Image _rightImage;

    [Header("Positioning")]
    [SerializeField] private Vector2 _originalLeftAnchoredPosition;
    [SerializeField] private Vector2 _originalRightAnchoredPosition;

    public bool IsActive { get; private set; }
    public bool IsPlayerInside { get; private set; }

    private int _stepIndex = -1;
    private bool _waitingForContinue;
    private bool _wrongConversationRunning;

    private bool _isUsingOverrides = false;
    private string _currentClientIdOverride;
    private string _currentConversationOverride;

    public event Action<ClientDialogueStepCompletionData> ClientDialogueStepCompleted;
    public event Action ClientDialogueFinished;
    public int CurrentStepIndex => _stepIndex;

    private ICustomDialogueUI _customDialogueUI;

    private int _overrideStepIndex = -1;

    public void Initialize(Canvas uiRoot, Image leftImage, Image rightImage, ICustomDialogueUI customDialogueUI)
    {
        _uiRoot = uiRoot;
        _leftImage = leftImage;
        _rightImage = rightImage;
        _customDialogueUI = customDialogueUI;

        _leftRoot = leftImage != null ? leftImage.rectTransform : null;
        _rightRoot = rightImage != null ? rightImage.rectTransform : null;

        if (_leftRoot != null) _originalLeftAnchoredPosition = _leftRoot.anchoredPosition;
        if (_rightRoot != null) _originalRightAnchoredPosition = _rightRoot.anchoredPosition;

        if (_uiRoot != null) _uiRoot.gameObject.SetActive(false);
        if (_hintCanvas != null) _hintCanvas.gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (_leftRoot != null) _originalLeftAnchoredPosition = _leftRoot.anchoredPosition;
        if (_rightRoot != null) _originalRightAnchoredPosition = _rightRoot.anchoredPosition;

        if (_uiRoot != null) _uiRoot.gameObject.SetActive(false);
        if (_hintCanvas != null) _hintCanvas.gameObject.SetActive(false);

        HidePortraits();
    }

    private void OnEnable()
    {
        if (_customDialogueUI != null) _customDialogueUI.OnSubtitleShown += OnSubtitleShown;

        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded += OnConversationEnded;
    }

    private void OnDisable()
    {
        if (_customDialogueUI != null) _customDialogueUI.OnSubtitleShown -= OnSubtitleShown;

        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
        {
            IsPlayerInside = true;
            if (_hintCanvas != null) _hintCanvas.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
        {
            IsPlayerInside = false;
            if (_hintCanvas != null) _hintCanvas.gameObject.SetActive(false);
            StopDialogUIOnly();
        }
    }

    public void StartClientDialog()
    {
        if (_portraitMap == null || _portraitMap.StepsCount == 0)
        {
            return;
        }

        if (DialogueManager.isConversationActive) return;

        IsActive = true;
        _waitingForContinue = false;
        _stepIndex = Mathf.Max(_stepIndex, 0);
        _isUsingOverrides = false;

        ShowUI();
        StartCurrentStep();
    }

    public void StartClientDialogWithSpecificStep(string clientId, string conversationTitle)
    {
        if (string.IsNullOrEmpty(conversationTitle))
        {
            Debug.LogError("[ClientInteraction] Cannot start specific step: conversationTitle is null or empty.");
            return;
        }

        if (DialogueManager.isConversationActive)
        {
            Debug.LogWarning("[ClientInteraction] Dialogue already active. Stopping current dialogue before starting new one.");
            DialogueManager.StopConversation();
        }

        IsActive = true;
        _waitingForContinue = false;
        _isUsingOverrides = true;
        _currentClientIdOverride = clientId;
        _currentConversationOverride = conversationTitle;

        _overrideStepIndex = FindStepIndexByConversation(conversationTitle);

        ShowUI();
        StartCurrentStepWithOverride();
    }

    public void ContinueSequence()
    {
        if (_portraitMap == null || _portraitMap.StepsCount == 0) return;
        if (DialogueManager.isConversationActive) return;

        if (!_waitingForContinue) return;

        _waitingForContinue = false;

        int next = _stepIndex + 1;

        if (next >= _portraitMap.StepsCount)
        {
            StopDialogUIOnly();
            IsActive = false;
            _stepIndex = -1;
            _isUsingOverrides = false;
            return;
        }

        _stepIndex = next;
        IsActive = true;

        ShowUI();
        StartCurrentStep();
    }

    public void CloseUI()
    {
        if (_uiRoot != null)
            _uiRoot.gameObject.SetActive(false);

        HidePortraits();
    }

    private void ShowUI()
    {
        if (_uiRoot != null) _uiRoot.gameObject.SetActive(true);
    }

    private void StopDialogUIOnly()
    {
        if (_uiRoot != null) _uiRoot.gameObject.SetActive(false);
        HidePortraits();
    }

    private void HidePortraits()
    {
        if (_leftRoot != null) _leftRoot.gameObject.SetActive(false);
        if (_rightRoot != null) _rightRoot.gameObject.SetActive(false);
    }

    private void StartCurrentStep()
    {
        if (_portraitMap == null) return;

        string conv = _portraitMap.GetConversation(_stepIndex);
        if (string.IsNullOrWhiteSpace(conv))
        {
            Debug.LogError($"[ClientInteraction] Empty conversation at step {_stepIndex}");
            return;
        }

        DialogueManager.StartConversation(conv);
    }

    private void StartCurrentStepWithOverride()
    {
        if (string.IsNullOrEmpty(_currentConversationOverride))
        {
            Debug.LogError("[ClientInteraction] Cannot start override step: conversation override is null or empty.");
            return;
        }

        DialogueManager.StartConversation(_currentConversationOverride);
    }

    private void OnConversationEnded(Transform actor)
    {
        if (!IsActive) return;

        if (_wrongConversationRunning)
        {
            _wrongConversationRunning = false;
            return;
        }

        if (_isUsingOverrides)
        {
            ClientDialogueStepCompletionData completionData = new ClientDialogueStepCompletionData(_currentClientIdOverride, _currentConversationOverride);

            ClientDialogueStepCompleted?.Invoke(completionData);
            CloseUI();
            _isUsingOverrides = false;
            _currentClientIdOverride = null;
            _currentConversationOverride = null;
        }
        else
        {
            _waitingForContinue = true;
            StopDialogUIOnly();
            ClientDialogueFinished?.Invoke();
        }
    }

    private void OnSubtitleShown(Subtitle subtitle)
    {
        if (!IsActive) return;
        if (_portraitMap == null) return;
        if (subtitle?.dialogueEntry == null) return;

        int entryID = subtitle.dialogueEntry.id;

        int mapStepIndex = _isUsingOverrides ? _overrideStepIndex : _stepIndex;
        if (mapStepIndex < 0) return;

        ClientPortraitMap.PortraitRule rule;
        if (!_portraitMap.TryGetRule(mapStepIndex, entryID, out rule))
            return;

        if (_leftRoot != null)
        {
            bool showLeft = rule.leftSprite != null;
            _leftRoot.gameObject.SetActive(showLeft);
            if (showLeft && _leftImage != null) _leftImage.sprite = rule.leftSprite;
        }

        if (_rightRoot != null)
        {
            bool showRight = rule.rightSprite != null;
            _rightRoot.gameObject.SetActive(showRight);
            if (showRight && _rightImage != null) _rightImage.sprite = rule.rightSprite;
        }

        ApplyPriority(rule.priority);
        ApplyPositioningOverride(rule.useCenteredPositionOverride);
    }

    private void ApplyPriority(ClientPortraitMap.SpeakerPriority priority)
    {
        switch (priority)
        {
            case ClientPortraitMap.SpeakerPriority.Left:
                if (_leftRoot != null) _leftRoot.SetAsLastSibling();
                break;
            case ClientPortraitMap.SpeakerPriority.Right:
                if (_rightRoot != null) _rightRoot.SetAsLastSibling();
                break;
        }
    }

    private void ApplyPositioningOverride(bool useOverride)
    {
        if (_leftRoot != null)
            _leftRoot.anchoredPosition = useOverride
                ? _portraitMap.centeredLeftAnchoredPos
                : _originalLeftAnchoredPosition;

        if (_rightRoot != null)
            _rightRoot.anchoredPosition = useOverride
                ? _portraitMap.centeredRightAnchoredPos
                : _originalRightAnchoredPosition;
    }

    public void PlayWrongPackageConversation()
    {
        if (_portraitMap == null) return;

        string conv = _portraitMap.wrongPackageConversation;
        if (string.IsNullOrWhiteSpace(conv)) return;

        if (DialogueManager.isConversationActive) return;

        _wrongConversationRunning = true;

        GameStateService.SetWrongPackageDialogue(true);

        DialogueManager.StartConversation(conv);

        if (_portraitMap.enforceCorrectAfterFirstWrong)
            GameStateService.EnforceRequiredPackageOnly = true;
    }

    private int FindStepIndexByConversation(string conversation)
    {
        if (_portraitMap == null || _portraitMap.steps == null) return -1;

        for (int i = 0; i < _portraitMap.steps.Count; i++)
        {
            if (string.Equals(_portraitMap.steps[i].conversation, conversation, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

}