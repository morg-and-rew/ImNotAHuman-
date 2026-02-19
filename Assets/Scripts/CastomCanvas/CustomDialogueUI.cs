using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class CustomDialogueUI : StandardDialogueUI, ICustomDialogueUI
{
    [Header("Advance")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;
    [SerializeField] private bool advanceOnlyWhenNoResponses = true;
    [Header("Forced Auto Advance")]
    [SerializeField, Min(0.1f)] private float autoAdvanceIntervalSeconds = 10f;
    [SerializeField] private GameObject[] hideOnForcedAutoAdvanceMode;

    [Header("Finish (Only for selected conversations)")]
    [SerializeField] private KeyCode finishKey = KeyCode.F;

    [SerializeField] private string[] finishByKeyConversations;

    [Header("Panels")]
    [SerializeField] private GameObject npcSubtitlePanel;
    [SerializeField] private GameObject[] hideOnChoiceMode;
    [SerializeField] private string[] hideSubtitlePanelOnChoiceConversations;
    [SerializeField] private RectTransform responseMenuRect;

    [Header("NPC Subtitle Panel Components")]
    [SerializeField] private RectTransform npcSubtitleRect;
    [SerializeField] private Image npcSubtitleImage;
    [SerializeField] private Text npcSubtitleText;
    [SerializeField] private VerticalLayoutGroup npcSubtitleTextVerticalLayoutGroup;

    [Header("NPC Subtitle - Normal State")]
    [SerializeField] private Vector2 normalAnchoredPos;
    [SerializeField] private Vector2 normalSizeDelta;
    [SerializeField] private Vector2 normalSizeText;
    [SerializeField] private int normalSubtitleFontSize = 24;
    [SerializeField] private Sprite normalSprite;
    [Header("NPC Text Rect - Normal State")]
    [SerializeField] private Vector2 normalTextAnchoredPos;
    [SerializeField] private Vector2 normalTextSize;

    [Header("NPC Subtitle - Choice State")]
    [SerializeField] private Vector2 choiceAnchoredPos;
    [SerializeField] private Vector2 choiceSizeDelta;
    [SerializeField] private Vector2 choiceSizeText;
    [SerializeField] private int choiceSubtitleFontSize = 29;
    [SerializeField] private Sprite choiceSprite;
    [Header("NPC Text Rect - Choice State")]
    [SerializeField] private Vector2 choiceTextAnchoredPos = new Vector2(177.5352f, -6f);
    [SerializeField] private Vector2 choiceTextSize = new Vector2(404.5963f, 297.4301f);
    [Header("Response Buttons (Choice options)")]
    [SerializeField] private Color responseButtonTextColor = new Color(240f / 255f, 209f / 255f, 133f / 255f, 1f);
    [SerializeField] private bool applyResponseButtonTextColor = true;
    [SerializeField] private int responseButtonTextSize = 28;
    [SerializeField] private bool applyResponseButtonTextSize = true;
    [Tooltip("Цвет фона (Image) кнопок ответа. По умолчанию как у панели выбора (choiceImageColor).")]
    [SerializeField] private bool applyResponseButtonImageColor = true;
    [SerializeField] private Color responseButtonImageColor = new Color(82f / 255f, 79f / 255f, 13f / 255f, 1f);

    [Header("Three Choices Layout (Panels)")]
    [SerializeField] private bool useThreeChoicesPanelLayout = true;
    [SerializeField] private float threeChoicesNpcLeft = 450f;
    [SerializeField] private float threeChoicesNpcTop = 47f;
    [SerializeField] private float threeChoicesNpcRight = -411.9493f;
    [SerializeField] private float threeChoicesNpcBottom = 320.5624f;
    [SerializeField] private float threeChoicesResponseLeft = 490f;
    [SerializeField] private float threeChoicesResponseTop = 290.2039f;
    [SerializeField] private float threeChoicesResponseRight = 52.87378f;
    [SerializeField] private float threeChoicesResponseBottom = 63f;

    private readonly Color choiceImageColor = new Color(82 / 255f, 79f / 255f, 13f / 255f, 1f);
    private readonly Color choiceTextColor = new Color(240f / 255f, 209f / 255f, 133f / 255f, 1f);

    private bool _inChoiceMode;
    private bool _subtitleVisible;
    private bool _finishByKeyAllowed;
    private bool _currentIsLastEntry;
    private bool _awaitFinish;
    private bool _hasCachedTextRect;
    private Vector2 _normalTextAnchoredPosCached;
    private Vector2 _normalTextSizeCached;
    private bool _isThreeChoicesMode;
    private bool _hasCachedNpcPanelOffsets;
    private Vector2 _normalNpcOffsetMin;
    private Vector2 _normalNpcOffsetMax;
    private bool _hasCachedResponsePanelOffsets;
    private Vector2 _normalResponseOffsetMin;
    private Vector2 _normalResponseOffsetMax;
    private bool _forcedAutoAdvanceEnabled;
    private float _forcedAutoAdvanceDelay;
    private float _nextForcedAutoAdvanceAt;
    private bool _subtitlePanelHiddenByChoiceRule;

    public event Action<Subtitle> OnSubtitleShown;
    public event Action OnClientDialogueFinishedByKey;

    public bool IsDialogueActive => DialogueManager.isConversationActive;

    public override void Awake()
    {
        base.Awake();

        HideContinueButtons();

        if (npcSubtitleRect == null && npcSubtitlePanel != null)
            npcSubtitleRect = npcSubtitlePanel.GetComponent<RectTransform>();

        if (npcSubtitleImage == null && npcSubtitlePanel != null)
            npcSubtitleImage = npcSubtitlePanel.GetComponent<Image>();

        if (npcSubtitlePanel != null) npcSubtitlePanel.SetActive(false);
        RefreshChoiceModeHiddenObjectsVisibility();
        SetAutoAdvanceHiddenObjectsVisible(true);
        CachePanelDefaults();
        _forcedAutoAdvanceDelay = Mathf.Max(0.1f, autoAdvanceIntervalSeconds);

        if (npcSubtitleText != null)
        {
            RectTransform textRect = npcSubtitleText.rectTransform;
            if (textRect != null)
            {
                _hasCachedTextRect = true;
                _normalTextAnchoredPosCached = textRect.anchoredPosition;
                _normalTextSizeCached = textRect.sizeDelta;
            }
        }

        ApplyNormalState();
    }

    private void Start()
    {
        // Диалоги не пролистываются сами — только по пробелу или F
        if (DialogueManager.instance != null && DialogueManager.displaySettings != null
            && DialogueManager.displaySettings.subtitleSettings != null)
        {
            DialogueManager.displaySettings.subtitleSettings.continueButton =
                DisplaySettings.SubtitleSettings.ContinueButtonMode.Always;
        }
    }

    private void HideContinueButtons()
    {
        if (conversationUIElements == null || conversationUIElements.subtitlePanels == null) return;
        for (int i = 0; i < conversationUIElements.subtitlePanels.Length; i++)
        {
            var panel = conversationUIElements.subtitlePanels[i];
            if (panel != null && panel.continueButton != null)
                panel.continueButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsDialogueActive) return;

        if (_awaitFinish)
        {
            if (Input.GetKeyDown(finishKey))
            {
                DialogueManager.StopConversation();
                _awaitFinish = false;
                OnClientDialogueFinishedByKey?.Invoke();
            }
            return;
        }

        if (_forcedAutoAdvanceEnabled)
        {
            if (advanceOnlyWhenNoResponses && _inChoiceMode) return;
            if (!_subtitleVisible) return;
            if (Time.unscaledTime < _nextForcedAutoAdvanceAt) return;

            _nextForcedAutoAdvanceAt = Time.unscaledTime + _forcedAutoAdvanceDelay;
            OnContinueConversation();
            return;
        }

        if (advanceOnlyWhenNoResponses && _inChoiceMode) return;
        if (!_subtitleVisible) return;

        if (Input.GetKeyDown(advanceKey))
        {
            if (_finishByKeyAllowed && _currentIsLastEntry && !_inChoiceMode)
            {
                _awaitFinish = true;
                return;
            }

            OnContinueConversation();
        }
    }

    public override void ShowSubtitle(Subtitle subtitle)
    {
        base.ShowSubtitle(subtitle);

        _subtitleVisible = true;
        _awaitFinish = false;

        _finishByKeyAllowed = IsFinishByKeyConversation(subtitle);
        _currentIsLastEntry = IsLastEntry(subtitle);
        if (_forcedAutoAdvanceEnabled)
            _nextForcedAutoAdvanceAt = Time.unscaledTime + _forcedAutoAdvanceDelay;

        if (npcSubtitleText != null)
            npcSubtitleText.text = subtitle != null ? subtitle.formattedText.text : "";

        if (!_inChoiceMode && npcSubtitlePanel != null)
            npcSubtitlePanel.SetActive(true);

        OnSubtitleShown?.Invoke(subtitle);
    }

    public override void ShowResponses(Subtitle subtitle, Response[] responses, float timeout)
    {
        _inChoiceMode = true;
        _subtitleVisible = true;
        _awaitFinish = false;
        RefreshChoiceModeHiddenObjectsVisibility();
        _isThreeChoicesMode = useThreeChoicesPanelLayout && responses != null && responses.Length == 3;

        _finishByKeyAllowed = IsFinishByKeyConversation(subtitle);
        _currentIsLastEntry = IsLastEntry(subtitle);

        ApplyChoiceState();

        base.ShowResponses(subtitle, responses, timeout);

        ApplyResponseButtonColors();

        if (npcSubtitleText != null)
            npcSubtitleText.text = subtitle != null ? subtitle.formattedText.text : "";

        OnSubtitleShown?.Invoke(subtitle);

        if (IsHideSubtitlePanelOnChoiceConversation(subtitle))
        {
            _subtitlePanelHiddenByChoiceRule = true;
            StartCoroutine(HideSubtitlePanelNextFrame());
        }
    }

    public override void HideResponses()
    {
        if (_subtitlePanelHiddenByChoiceRule)
        {
            if (npcSubtitlePanel != null) npcSubtitlePanel.SetActive(true);
            var defPanel = conversationUIElements?.defaultNPCSubtitlePanel;
            if (defPanel != null)
            {
                var go = defPanel.panel != null ? defPanel.panel.gameObject : defPanel.gameObject;
                go.SetActive(true);
            }
            _subtitlePanelHiddenByChoiceRule = false;
        }

        base.HideResponses();

        _inChoiceMode = false;
        _isThreeChoicesMode = false;
        RefreshChoiceModeHiddenObjectsVisibility();
        RestorePanelDefaults();

        ApplyNormalState();
    }

    public override void HideSubtitle(Subtitle subtitle)
    {
        base.HideSubtitle(subtitle);
        _subtitleVisible = false;
    }

    public void ShowUI()
    {
        if (npcSubtitlePanel != null) npcSubtitlePanel.SetActive(true);
    }

    public void HideUI()
    {
        if (npcSubtitlePanel != null) npcSubtitlePanel.SetActive(false);
    }

    public void SetUIVisibility(bool visible)
    {
        if (npcSubtitlePanel != null) npcSubtitlePanel.SetActive(visible);
    }

    public void SetForcedAutoAdvance(bool enabled, float intervalSeconds = -1f)
    {
        _forcedAutoAdvanceEnabled = enabled;
        _forcedAutoAdvanceDelay = intervalSeconds > 0f
            ? Mathf.Max(0.1f, intervalSeconds)
            : Mathf.Max(0.1f, autoAdvanceIntervalSeconds);
        _nextForcedAutoAdvanceAt = Time.unscaledTime + _forcedAutoAdvanceDelay;
        RefreshChoiceModeHiddenObjectsVisibility();
        SetAutoAdvanceHiddenObjectsVisible(!enabled);
    }

    private bool IsLastEntry(Subtitle subtitle)
    {
        if (subtitle?.dialogueEntry == null) return false;

        List<Link> links = subtitle.dialogueEntry.outgoingLinks;
        return (links == null || links.Count == 0);
    }

    private IEnumerator HideSubtitlePanelNextFrame()
    {
        yield return null;
        if (!_subtitlePanelHiddenByChoiceRule) yield break;

        if (npcSubtitlePanel != null)
            npcSubtitlePanel.SetActive(false);
        var defPanel = conversationUIElements?.defaultNPCSubtitlePanel;
        if (defPanel != null)
        {
            var go = defPanel.panel != null ? defPanel.panel.gameObject : defPanel.gameObject;
            go.SetActive(false);
        }
    }

    private void ApplyResponseButtonColors()
    {
        // Собираем все кнопки ответа: и назначенные в панели, и созданные из шаблона
        var allButtons = new List<StandardUIResponseButton>();

        if (conversationUIElements?.menuPanels != null)
        {
            for (int p = 0; p < conversationUIElements.menuPanels.Length; p++)
            {
                var panel = conversationUIElements.menuPanels[p];
                if (panel == null) continue;

                if (panel.buttons != null)
                {
                    for (int i = 0; i < panel.buttons.Length; i++)
                    {
                        if (panel.buttons[i] != null && panel.buttons[i].gameObject.activeInHierarchy)
                            allButtons.Add(panel.buttons[i]);
                    }
                }

                if (panel.instantiatedButtons != null)
                {
                    for (int i = 0; i < panel.instantiatedButtons.Count; i++)
                    {
                        var go = panel.instantiatedButtons[i];
                        if (go == null) continue;
                        var rb = go.GetComponent<StandardUIResponseButton>();
                        if (rb != null && rb.gameObject.activeInHierarchy)
                            allButtons.Add(rb);
                    }
                }

                // Кнопки из шаблона могут быть под buttonTemplateHolder
                if (panel.buttonTemplateHolder != null)
                {
                    var fromHolder = panel.buttonTemplateHolder.GetComponentsInChildren<StandardUIResponseButton>(true);
                    for (int i = 0; i < fromHolder.Length; i++)
                    {
                        if (fromHolder[i] != null && fromHolder[i].gameObject.activeInHierarchy && !allButtons.Contains(fromHolder[i]))
                            allButtons.Add(fromHolder[i]);
                    }
                }
            }
        }

        for (int i = 0; i < allButtons.Count; i++)
            ApplyResponseButtonStyleToSingle(allButtons[i]);
    }

    private void ApplyResponseButtonStyleToSingle(StandardUIResponseButton rb)
    {
        if (rb.label == null) return;

        if (applyResponseButtonTextColor)
            rb.label.color = responseButtonTextColor;

        if (applyResponseButtonTextSize)
        {
            if (rb.label.uiText != null)
                rb.label.uiText.fontSize = responseButtonTextSize;
#if TMP_PRESENT
            if (rb.label.textMeshProUGUI != null)
                rb.label.textMeshProUGUI.fontSize = responseButtonTextSize;
#endif
        }

        if (applyResponseButtonImageColor)
        {
            Image plaqueImage = rb.transform.Find("Background")?.GetComponent<Image>();
            if (plaqueImage == null && rb.button != null)
                plaqueImage = rb.button.image;
            if (plaqueImage != null)
                plaqueImage.color = responseButtonImageColor;
        }
    }

    private bool IsHideSubtitlePanelOnChoiceConversation(Subtitle subtitle)
    {
        if (hideSubtitlePanelOnChoiceConversations == null || hideSubtitlePanelOnChoiceConversations.Length == 0)
            return false;
        if (subtitle?.dialogueEntry == null) return false;

        DialogueDatabase db = DialogueManager.masterDatabase;
        if (db == null) return false;
        Conversation conv = db.GetConversation(subtitle.dialogueEntry.conversationID);
        if (conv == null) return false;

        string title = conv.Title;
        return hideSubtitlePanelOnChoiceConversations.Any(s =>
            !string.IsNullOrEmpty(s) && string.Equals(s.Trim(), title, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsFinishByKeyConversation(Subtitle subtitle)
    {
        if (finishByKeyConversations == null || finishByKeyConversations.Length == 0)
            return false;

        if (subtitle?.dialogueEntry == null)
            return false;

        int conversationID = subtitle.dialogueEntry.conversationID;

        DialogueDatabase db = DialogueManager.masterDatabase;
        if (db == null) return false;

        Conversation conv = db.GetConversation(conversationID);
        if (conv == null) return false;

        string title = conv.Title;
        return finishByKeyConversations.Any(s =>
            !string.IsNullOrEmpty(s) && string.Equals(s.Trim(), title, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyNormalState()
    {
        if (npcSubtitleRect != null)
        {
            npcSubtitleRect.anchoredPosition = normalAnchoredPos;
            npcSubtitleRect.localScale = normalSizeDelta;
        }

        if (npcSubtitleText != null)
        {
            npcSubtitleText.color = Color.white;
            npcSubtitleText.fontSize = normalSubtitleFontSize;
            npcSubtitleText.transform.localScale = normalSizeText;
            RectTransform textRect = npcSubtitleText.rectTransform;
            if (textRect != null)
            {
                if (normalTextSize != Vector2.zero)
                {
                    textRect.anchoredPosition = normalTextAnchoredPos;
                    textRect.sizeDelta = normalTextSize;
                }
                else if (_hasCachedTextRect)
                {
                    textRect.anchoredPosition = _normalTextAnchoredPosCached;
                    textRect.sizeDelta = _normalTextSizeCached;
                }
            }
        }

        if (npcSubtitleTextVerticalLayoutGroup != null)
        {
            npcSubtitleTextVerticalLayoutGroup.padding.left = 0;
            npcSubtitleTextVerticalLayoutGroup.padding.top = -110;
        }

        if (npcSubtitleImage != null)
        {
            npcSubtitleImage.color = Color.white;
            if (normalSprite != null) npcSubtitleImage.sprite = normalSprite;
        }
    }

    private void ApplyChoiceState()
    {
        if (npcSubtitleRect != null)
        {
            npcSubtitleRect.anchoredPosition = choiceAnchoredPos;
            npcSubtitleRect.localScale = choiceSizeDelta;
        }

        if (npcSubtitleImage != null)
        {
            npcSubtitleImage.color = choiceImageColor;
            if (choiceSprite != null) npcSubtitleImage.sprite = choiceSprite;
        }

        if (npcSubtitleText != null)
        {
            npcSubtitleText.color = choiceTextColor;
            npcSubtitleText.fontSize = choiceSubtitleFontSize;
            npcSubtitleText.transform.localScale = choiceSizeText;
            RectTransform textRect = npcSubtitleText.rectTransform;
            if (textRect != null)
            {
                textRect.anchoredPosition = choiceTextAnchoredPos;
                textRect.sizeDelta = choiceTextSize;
            }
        }

        if (npcSubtitleTextVerticalLayoutGroup != null)
        {
            npcSubtitleTextVerticalLayoutGroup.padding.left = -291;
            npcSubtitleTextVerticalLayoutGroup.padding.top = _isThreeChoicesMode ? -35 : -18;
        }

        if (_isThreeChoicesMode)
        {
            ApplyPanelOffsets(npcSubtitleRect, threeChoicesNpcLeft, threeChoicesNpcTop, threeChoicesNpcRight, threeChoicesNpcBottom);
            RectTransform menuRect = ResolveResponseMenuRect();
            ApplyPanelOffsets(menuRect, threeChoicesResponseLeft, threeChoicesResponseTop, threeChoicesResponseRight, threeChoicesResponseBottom);
        }
    }

    private void SetChoiceModeHiddenObjectsVisible(bool visible)
    {
        if (hideOnChoiceMode == null) return;
        for (int i = 0; i < hideOnChoiceMode.Length; i++)
        {
            GameObject go = hideOnChoiceMode[i];
            if (go != null)
                go.SetActive(visible);
        }
    }

    private void RefreshChoiceModeHiddenObjectsVisibility()
    {
        bool visible = !_inChoiceMode && !_forcedAutoAdvanceEnabled;
        SetChoiceModeHiddenObjectsVisible(visible);
    }

    private void SetAutoAdvanceHiddenObjectsVisible(bool visible)
    {
        if (hideOnForcedAutoAdvanceMode == null) return;
        for (int i = 0; i < hideOnForcedAutoAdvanceMode.Length; i++)
        {
            GameObject go = hideOnForcedAutoAdvanceMode[i];
            if (go != null)
                go.SetActive(visible);
        }
    }

    private void CachePanelDefaults()
    {
        if (npcSubtitleRect != null)
        {
            _hasCachedNpcPanelOffsets = true;
            _normalNpcOffsetMin = npcSubtitleRect.offsetMin;
            _normalNpcOffsetMax = npcSubtitleRect.offsetMax;
        }

        RectTransform menuRect = ResolveResponseMenuRect();
        if (menuRect != null)
        {
            _hasCachedResponsePanelOffsets = true;
            _normalResponseOffsetMin = menuRect.offsetMin;
            _normalResponseOffsetMax = menuRect.offsetMax;
        }
    }

    private void RestorePanelDefaults()
    {
        if (_hasCachedNpcPanelOffsets && npcSubtitleRect != null)
        {
            npcSubtitleRect.offsetMin = _normalNpcOffsetMin;
            npcSubtitleRect.offsetMax = _normalNpcOffsetMax;
        }

        RectTransform menuRect = ResolveResponseMenuRect();
        if (_hasCachedResponsePanelOffsets && menuRect != null)
        {
            menuRect.offsetMin = _normalResponseOffsetMin;
            menuRect.offsetMax = _normalResponseOffsetMax;
        }
    }

    private RectTransform ResolveResponseMenuRect()
    {
        if (responseMenuRect != null) return responseMenuRect;
        if (conversationUIElements?.menuPanels == null) return null;

        foreach (var panel in conversationUIElements.menuPanels.Where(p => p != null))
        {
            if (panel.panel != null) return panel.panel.rectTransform;
            var rt = panel.GetComponent<RectTransform>();
            if (rt != null) return rt;
        }
        return null;
    }

    private void ApplyPanelOffsets(RectTransform rt, float left, float top, float right, float bottom)
    {
        if (rt == null) return;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
}