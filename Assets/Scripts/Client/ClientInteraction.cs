using PixelCrushers.DialogueSystem;
using System;
using System.IO;
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
    [Tooltip("Панель только с портретами клиента (левый/правый). Если задана — скрываем только её, не весь _uiRoot (PlayerCanvas), чтобы не затронуть подсказки обучения и другой UI.")]
    [SerializeField] private GameObject _clientPortraitsPanel;

    [Header("Portraits (Left/Right)")]
    [SerializeField] private RectTransform _leftRoot;
    [SerializeField] private RectTransform _rightRoot;
    [SerializeField] private Image _leftImage;
    [SerializeField] private Image _rightImage;

    [Header("Positioning")]
    [SerializeField] private Vector2 _originalLeftAnchoredPosition;
    [SerializeField] private Vector2 _originalRightAnchoredPosition;
    private Vector3 _originalLeftScale = Vector3.one;
    private Vector3 _originalRightScale = Vector3.one;
    private Vector3 _originalLeftEulerAngles = Vector3.zero;
    private Vector3 _originalRightEulerAngles = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool _debugPortraits;

    [Header("Hint")]
    [SerializeField] private Sprite _hintSprite;

    [Header("Look at (для начала диалога нужно смотреть в эту точку)")]
    [SerializeField] private Transform _lookAtPoint;
    [Tooltip("Макс. расстояние до точки (м). 0 = без ограничения.")]
    [SerializeField, Min(0f)] private float _maxLookDistance = 0f;

    public bool IsActive { get; private set; }
    public bool IsPlayerInside { get; private set; }
    public bool IsWaitingForContinue => _waitingForContinue;

    private int _stepIndex = -1;
    private bool _waitingForContinue;
    private bool _wrongConversationRunning;

    private bool _isUsingOverrides = false;
    private string _currentClientIdOverride;
    private string _currentConversationOverride;
    private bool _removePackageFromHandsDoneThisConversation;

    public event Action<ClientDialogueStepCompletionData> ClientDialogueStepCompleted;
    public event Action ClientDialogueFinished;
    public event Action ClientConversationStarted;
    public event Action RequestRemovePackageFromHands;
    public int CurrentStepIndex => _stepIndex;

    private ICustomDialogueUI _customDialogueUI;

    private int _overrideStepIndex = -1;

    public void Initialize(Canvas uiRoot, Image leftImage, Image rightImage, ICustomDialogueUI customDialogueUI)
    {
        _uiRoot = uiRoot;
        _leftImage = leftImage;
        _rightImage = rightImage;

        if (_customDialogueUI != null)
            _customDialogueUI.OnSubtitleShown -= OnSubtitleShown;

        _customDialogueUI = customDialogueUI;

        if (_customDialogueUI != null)
            _customDialogueUI.OnSubtitleShown += OnSubtitleShown;

        _leftRoot = leftImage != null ? leftImage.rectTransform : null;
        _rightRoot = rightImage != null ? rightImage.rectTransform : null;

        if (_leftRoot != null)
        {
            _originalLeftAnchoredPosition = _leftRoot.anchoredPosition;
            _originalLeftScale = _leftRoot.localScale;
            _originalLeftEulerAngles = _leftRoot.localEulerAngles;
        }
        if (_rightRoot != null)
        {
            _originalRightAnchoredPosition = _rightRoot.anchoredPosition;
            _originalRightScale = _rightRoot.localScale;
            _originalRightEulerAngles = _rightRoot.localEulerAngles;
        }

        SetClientPortraitsRootActive(false);
    }

    private void Awake()
    {
        if (_leftRoot != null)
        {
            _originalLeftAnchoredPosition = _leftRoot.anchoredPosition;
            _originalLeftScale = _leftRoot.localScale;
            _originalLeftEulerAngles = _leftRoot.localEulerAngles;
        }
        if (_rightRoot != null)
        {
            _originalRightAnchoredPosition = _rightRoot.anchoredPosition;
            _originalRightScale = _rightRoot.localScale;
            _originalRightEulerAngles = _rightRoot.localEulerAngles;
        }

        SetClientPortraitsRootActive(false);

        HidePortraits();
    }

    private void Start()
    {
    }

    private void OnEnable()
    {
        if (_customDialogueUI != null)
        {
            _customDialogueUI.OnSubtitleShown -= OnSubtitleShown;
            _customDialogueUI.OnSubtitleShown += OnSubtitleShown;
        }

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationEnded += OnConversationEnded;
            DialogueManager.instance.conversationStarted += OnConversationStarted;
        }
    }

    private void OnDisable()
    {
        if (_customDialogueUI != null)
            _customDialogueUI.OnSubtitleShown -= OnSubtitleShown;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
            DialogueManager.instance.conversationStarted -= OnConversationStarted;
        }
    }

    private void OnConversationStarted(Transform _)
    {
        _removePackageFromHandsDoneThisConversation = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
            IsPlayerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
        {
            IsPlayerInside = false;
            if (!IsActive)
                StopDialogUIOnly();
        }
    }

    public bool IsPlayerLookingAtClient(PlayerView player)
    {
        if (player == null || player.PlayerCamera == null) return false;
        Transform point = _lookAtPoint != null ? _lookAtPoint : transform;
        Vector3 toPoint = point.position - player.transform.position;
        if (_maxLookDistance > 0f && toPoint.sqrMagnitude > _maxLookDistance * _maxLookDistance) return false;
        toPoint.y = 0f;
        if (toPoint.sqrMagnitude < 0.0001f) return true;
        toPoint.Normalize();
        Vector3 camForward = player.PlayerCamera.transform.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.0001f) return false;
        camForward.Normalize();
        return Vector3.Dot(camForward, toPoint) >= 0.5f;
    }

    private void Update()
    {
        if (PlayerHintView.Instance == null) return;
        GameFlowController flow = GameFlowController.Instance;
        PlayerView player = flow != null ? flow.Player : null;
        bool holdingPhone = HandsRegistry.Hands != null && HandsRegistry.Hands.HasItem && HandsRegistry.Hands.Current is PhoneItemView;
        bool isLooking = player != null && IsPlayerLookingAtClient(player);
        bool canShowClientHint = flow != null && flow.ShouldShowClientInteractHint();
        bool canInteract = !_isUsingOverrides && canShowClientHint && player != null && isLooking && !holdingPhone && (!IsActive || _waitingForContinue);
        Sprite sprite = canInteract ? _hintSprite : null;
        PlayerHintView.Instance.SetClientHint(sprite);
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
            return;

        if (DialogueManager.isConversationActive)
            DialogueManager.StopConversation();

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
        SetClientPortraitsRootActive(false);
        HidePortraits();
    }

    private void ShowUI()
    {
        SetClientPortraitsRootActive(true);
    }

    private void StopDialogUIOnly()
    {
        SetClientPortraitsRootActive(false);
        HidePortraits();
    }

    private void SetClientPortraitsRootActive(bool active)
    {
        if (_clientPortraitsPanel != null)
        {
            _clientPortraitsPanel.SetActive(active);
            return;
        }
        if (_uiRoot != null && active)
            _uiRoot.gameObject.SetActive(true);
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
            return;

        DialogueManager.StartConversation(conv);
        ClientConversationStarted?.Invoke();
    }

    private void StartCurrentStepWithOverride()
    {
        if (string.IsNullOrEmpty(_currentConversationOverride))
            return;

        DialogueManager.StartConversation(_currentConversationOverride);
        ClientConversationStarted?.Invoke();
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
            IsActive = false;
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
        int conversationID = subtitle.dialogueEntry.conversationID;

        int mapStepIndex = _isUsingOverrides ? _overrideStepIndex : GetStepIndexFromConversation(conversationID);
        if (mapStepIndex < 0)
            return;

        ClientPortraitMap.PortraitRule rule;
        // Для диалогов вроде Client_Day1.5.2 первая реплика с текстом может иметь id 1 (id 0 = START без текста), в мапе задано правило для entryID 0 — используем его как fallback
        if (!_portraitMap.TryGetRule(mapStepIndex, entryID, out rule) && !_portraitMap.TryGetRule(mapStepIndex, 0, out rule))
            return;

        if (_leftRoot != null)
        {
            bool showLeft = rule.leftSprite != null;
            _leftRoot.gameObject.SetActive(showLeft);
            if (showLeft && _leftImage != null)
            {
                _leftImage.sprite = rule.leftSprite;
                _leftImage.color = rule.leftSpriteColor.a < 0.001f ? Color.white : rule.leftSpriteColor;
            }
        }

        if (_rightRoot != null)
        {
            bool showRight = rule.rightSprite != null;
            _rightRoot.gameObject.SetActive(showRight);
            if (showRight && _rightImage != null)
            {
                _rightImage.sprite = rule.rightSprite;
                _rightImage.color = rule.rightSpriteColor.a < 0.001f ? Color.white : rule.rightSpriteColor;
            }
        }

        ApplyPriority(rule.priority);
        ApplyPositioningOverride(rule);

        if (!_removePackageFromHandsDoneThisConversation
            && mapStepIndex >= 0
            && mapStepIndex < _portraitMap.steps.Count)
        {
            ClientPortraitMap.Step step = _portraitMap.steps[mapStepIndex];
            if (step.removePackageFromHandsAfterEntryID > 0 && entryID >= step.removePackageFromHandsAfterEntryID)
            {
                _removePackageFromHandsDoneThisConversation = true;
                RequestRemovePackageFromHands?.Invoke();
            }
        }
    }

    private int GetStepIndexFromConversation(int conversationID)
    {
        if (_portraitMap == null || DialogueManager.masterDatabase == null) return _stepIndex;
        Conversation conv = DialogueManager.masterDatabase.GetConversation(conversationID);
        if (conv == null) return _stepIndex;
        int byTitle = FindStepIndexByConversation(conv.Title);
        return byTitle >= 0 ? byTitle : _stepIndex;
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

    private void ApplyPositioningOverride(ClientPortraitMap.PortraitRule rule)
    {
        if (rule.useCustomPositionAndSize)
        {
            Vector3 leftScaleVec = rule.customLeftScale.sqrMagnitude > 0.001f ? rule.customLeftScale : Vector3.one;
            Vector3 rightScaleVec = rule.customRightScale.sqrMagnitude > 0.001f ? rule.customRightScale : Vector3.one;
            if (_leftRoot != null)
            {
                _leftRoot.anchoredPosition3D = rule.customLeftAnchoredPos;
                _leftRoot.localScale = leftScaleVec;
                _leftRoot.localEulerAngles = rule.customLeftRotation;
            }
            if (_rightRoot != null)
            {
                _rightRoot.anchoredPosition3D = rule.customRightAnchoredPos;
                _rightRoot.localScale = rightScaleVec;
                _rightRoot.localEulerAngles = rule.customRightRotation;
            }
            return;
        }

        bool useOverride = rule.useCenteredPositionOverride;
        float leftScale = useOverride && rule.leftScale > 0f ? rule.leftScale : _portraitMap.centeredLeftScale;
        float rightScale = useOverride && rule.rightScale > 0f ? rule.rightScale : _portraitMap.centeredRightScale;

        if (_leftRoot != null)
        {
            if (useOverride)
                _leftRoot.anchoredPosition3D = _portraitMap.centeredLeftAnchoredPos;
            else
                _leftRoot.anchoredPosition = _originalLeftAnchoredPosition;
            _leftRoot.localScale = useOverride ? Vector3.one * leftScale : _originalLeftScale;
            _leftRoot.localEulerAngles = _originalLeftEulerAngles;
        }

        if (_rightRoot != null)
        {
            if (useOverride)
                _rightRoot.anchoredPosition3D = _portraitMap.centeredRightAnchoredPos;
            else
                _rightRoot.anchoredPosition = _originalRightAnchoredPosition;
            _rightRoot.localScale = useOverride ? Vector3.one * rightScale : _originalRightScale;
            _rightRoot.localEulerAngles = useOverride ? _portraitMap.centeredRightRotation : _originalRightEulerAngles;
        }
    }

    public void ShowPortraitOnly(string conversation)
    {
        if (_portraitMap == null || string.IsNullOrEmpty(conversation)) return;
        int stepIndex = FindStepIndexByConversation(conversation);
        if (stepIndex < 0 || stepIndex >= _portraitMap.steps.Count) return;
        var step = _portraitMap.steps[stepIndex];
        if (step.rules == null || step.rules.Count == 0) return;

        var rule = step.rules[0];
        if (_leftRoot != null)
        {
            bool showLeft = rule.leftSprite != null;
            _leftRoot.gameObject.SetActive(showLeft);
            if (showLeft && _leftImage != null)
            {
                _leftImage.sprite = rule.leftSprite;
                _leftImage.color = rule.leftSpriteColor.a < 0.001f ? Color.white : rule.leftSpriteColor;
            }
        }
        if (_rightRoot != null)
        {
            bool showRight = rule.rightSprite != null;
            _rightRoot.gameObject.SetActive(showRight);
            if (showRight && _rightImage != null)
            {
                _rightImage.sprite = rule.rightSprite;
                _rightImage.color = rule.rightSpriteColor.a < 0.001f ? Color.white : rule.rightSpriteColor;
            }
        }
        ApplyPriority(rule.priority);
        ApplyPositioningOverride(rule);
        ShowUI();
    }

    public void HidePortraitOnly()
    {
        CloseUI();
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
        ClientConversationStarted?.Invoke();

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