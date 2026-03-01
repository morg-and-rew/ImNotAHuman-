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
    [Tooltip("Минимальная задержка между нажатиями пробела (сек). 0 = без задержки.")]
    [SerializeField, Min(0f)] private float manualAdvanceMinInterval = 2f;
    [Header("Forced Auto Advance")]
    [SerializeField, Min(0.1f)] private float autoAdvanceIntervalSeconds = 10f;
    [SerializeField] private GameObject[] hideOnForcedAutoAdvanceMode;
    [Tooltip("Диалоги, которые должны сами пролистываться (как радио). Добавь сюда название conversation при необходимости.")]
    [SerializeField] private string[] autoAdvanceConversations = new string[0];
    [SerializeField, Min(0.1f)] private float autoAdvanceIntervalForList = 8f;

    [Header("Finish (Only for selected conversations)")]
    [SerializeField] private KeyCode finishKey = KeyCode.F;

    [SerializeField] private string[] finishByKeyConversations;

    [Header("Panels")]
    [SerializeField] private GameObject npcSubtitlePanel;
    [SerializeField] private GameObject[] hideOnChoiceMode;
    [Header("Name Plate (плашки имён — только во время разговора с клиентом, когда говорят двое — две плашки)")]
    [Tooltip("Канвас плашек имени со сцены. Виден только во время диалога с клиентом, спрайты из мапы. Скрывается при выборе (Hide On choice mode).")]
    [SerializeField] private Canvas namePlateHideOnChoiceCanvas;
    [Tooltip("Image для имени левого говорящего — nameSprite из Client Portrait Map.")]
    [SerializeField] private Image namePlateImageLeft;
    [Tooltip("Image для имени правого говорящего — nameSpriteRight из мапы.")]
    [SerializeField] private Image namePlateImageRight;
    [Tooltip("Client Portrait Map: по нему определяем, что диалог «клиентский», и берём спрайт имени (nameSprite).")]
    [SerializeField] private ClientPortraitMap clientPortraitMap;
    [Tooltip("Sorting Order для канваса имени (если задан). Чем больше — тем выше слой.")]
    [SerializeField] private int namePlateCanvasSortOrder = 100;
    [SerializeField] private string[] hideSubtitlePanelOnChoiceConversations;
    [SerializeField] private RectTransform responseMenuRect;

    [Header("NPC Subtitle Panel Components")]
    [SerializeField] private RectTransform npcSubtitleRect;
    [SerializeField] private Image npcSubtitleImage;
    [SerializeField] private Text npcSubtitleText;
    [SerializeField] private VerticalLayoutGroup npcSubtitleTextVerticalLayoutGroup;
    [Tooltip("Базовый цвет панели NPC Subtitle (Image).")]
    [SerializeField] private Color npcSubtitlePanelBaseColor = Color.white;

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
    private float _nextManualAdvanceAllowedAt;
    private bool _subtitlePanelHiddenByChoiceRule;
    private bool _manualAdvanceBlocked;

    public event Action<Subtitle> OnSubtitleShown;
    public event Action OnClientDialogueFinishedByKey;
    public event Action OnResponseMenuShown;
    public event Action OnResponseMenuHidden;

    private readonly List<GameObject> _hideOnChoiceModeRuntime = new List<GameObject>();
    private bool _namePlateVisibleByClientConversation;

    public bool IsDialogueActive => DialogueManager.isConversationActive;

    public override void Awake()
    {
        base.Awake();

        HideContinueButtons();

        if (npcSubtitleRect == null && npcSubtitlePanel != null)
            npcSubtitleRect = npcSubtitlePanel.GetComponent<RectTransform>();

        if (npcSubtitleImage == null && npcSubtitlePanel != null)
            npcSubtitleImage = npcSubtitlePanel.GetComponent<Image>();

        if (namePlateHideOnChoiceCanvas != null)
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
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

    private void OnEnable()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStarted;
            DialogueManager.instance.conversationEnded += OnConversationEnded;
        }
    }

    private void OnDisable()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStarted;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
        }
    }

    private void OnConversationStarted(Transform actor)
    {
        string title = DialogueManager.lastConversationStarted;
        if (string.IsNullOrEmpty(title)) return;
        for (int i = 0; i < autoAdvanceConversations?.Length; i++)
        {
            if (string.Equals(autoAdvanceConversations[i], title, StringComparison.OrdinalIgnoreCase))
            {
                SetForcedAutoAdvance(true, autoAdvanceIntervalForList);
                return;
            }
        }
    }

    private void OnConversationEnded(Transform actor)
    {
        _namePlateVisibleByClientConversation = false;
        if (namePlateHideOnChoiceCanvas != null)
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
        SetForcedAutoAdvance(false);
        _awaitFinish = false;
        RefreshAwaitingFinishKeyUI();
    }

    private void Start()
    {
        // Диалоги не пролистываются сами — только по пробелу или F (кроме тех, что в autoAdvanceConversations)
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
                RefreshAwaitingFinishKeyUI();
                OnClientDialogueFinishedByKey?.Invoke();
            }
            return;
        }

        if (_forcedAutoAdvanceEnabled)
        {
            if (advanceOnlyWhenNoResponses && _inChoiceMode) return;
            if (Time.unscaledTime < _nextForcedAutoAdvanceAt) return;

            _nextForcedAutoAdvanceAt = Time.unscaledTime + _forcedAutoAdvanceDelay;
            OnContinueConversation();
            return;
        }

        if (advanceOnlyWhenNoResponses && _inChoiceMode) return;
        if (_manualAdvanceBlocked) return;
        if (manualAdvanceMinInterval > 0f && Time.unscaledTime < _nextManualAdvanceAllowedAt) return;

        if (Input.GetKeyDown(advanceKey))
        {
            if (TryAdvanceOrRequireFinishKey()) return;
            base.OnContinueConversation();
        }
    }

    /// <summary>
    /// Вызывается при нажатии пробела или при вызове OnContinue из кнопки/Submit.
    /// Для диалогов из finishByKeyConversations на последней реплике не листаем — только F завершает (через _awaitFinish).
    /// Иначе пробел мог бы закрывать диалог через другой обработчик (например Submit у кнопки), и сюжет не переходил бы на склад.
    /// </summary>
    public override void OnContinueConversation()
    {
        if (TryAdvanceOrRequireFinishKey())
            return;
        base.OnContinueConversation();
    }

    /// <returns>true, если продолжать не нужно (ожидаем F).</returns>
    private bool TryAdvanceOrRequireFinishKey()
    {
        if (_finishByKeyAllowed && _currentIsLastEntry && !_inChoiceMode)
        {
            _awaitFinish = true;
            RefreshAwaitingFinishKeyUI();
            return true;
        }
        return false;
    }

    public override void ShowSubtitle(Subtitle subtitle)
    {
        base.ShowSubtitle(subtitle);

        _subtitleVisible = true;

        _finishByKeyAllowed = IsFinishByKeyConversation(subtitle);
        _currentIsLastEntry = IsLastEntry(subtitle);
        // Как только показана последняя реплика диалога «только F» — сразу ждём F, чтобы по F перейти на склад без лишнего пробела.
        _awaitFinish = _finishByKeyAllowed && _currentIsLastEntry && !_inChoiceMode;
        RefreshAwaitingFinishKeyUI();

        if (manualAdvanceMinInterval > 0f && !_forcedAutoAdvanceEnabled && !_manualAdvanceBlocked)
            _nextManualAdvanceAllowedAt = Time.unscaledTime + manualAdvanceMinInterval;

        if (_forcedAutoAdvanceEnabled)
            _nextForcedAutoAdvanceAt = Time.unscaledTime + _forcedAutoAdvanceDelay;

        if (npcSubtitleText != null)
            npcSubtitleText.text = subtitle != null ? subtitle.formattedText.text : "";

        if (!_inChoiceMode && npcSubtitlePanel != null)
            npcSubtitlePanel.SetActive(true);

        UpdateNamePlateFromClientMap(subtitle);

        OnSubtitleShown?.Invoke(subtitle);
    }

    public override void ShowResponses(Subtitle subtitle, Response[] responses, float timeout)
    {
        _inChoiceMode = true;
        _subtitleVisible = true;
        _awaitFinish = false;
        RefreshChoiceModeHiddenObjectsVisibility();
        OnResponseMenuShown?.Invoke();
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
        OnResponseMenuHidden?.Invoke();
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
        RefreshSpacePlaqueVisibility();
    }

    /// <summary> Блокирует ручное перелистывание (пробел, кнопка). Используется для радио: листается только по таймлайну озвучки. Скрывает плашку Space (hideOnForcedAutoAdvanceMode). </summary>
    public void SetManualAdvanceBlocked(bool blocked)
    {
        _manualAdvanceBlocked = blocked;
        if (blocked)
        {
            HideContinueButtons();
            SetAutoAdvanceHiddenObjectsVisible(false);
        }
        else
        {
            RefreshSpacePlaqueVisibility();
        }
    }

    private bool IsLastEntry(Subtitle subtitle)
    {
        if (subtitle?.dialogueEntry == null) return false;

        List<Link> links = subtitle.dialogueEntry.outgoingLinks;
        if (links == null || links.Count == 0) return true;
        // В Dialogue System последняя реплика часто имеет одну ссылку на ноду 0 (START/конец) — тогда пробел не должен завершать диалог, только F.
        // Учитываем и случай одной ссылки на 0, и случай нескольких ссылок, среди которых есть переход в конец (чтобы не было "раз через раз").
        int convId = subtitle.dialogueEntry.conversationID;
        for (int i = 0; i < links.Count; i++)
        {
            Link link = links[i];
            if (link.destinationConversationID == convId && link.destinationDialogueID == 0)
                return true;
        }
        return false;
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
        if (subtitle?.dialogueEntry == null)
            return false;

        DialogueDatabase db = DialogueManager.masterDatabase;
        if (db == null) return false;

        Conversation conv = db.GetConversation(subtitle.dialogueEntry.conversationID);
        if (conv == null) return false;

        string title = conv.Title;

        // Только Client_Day1.4: правила отдельно, без влияния на остальные диалоги. Если выбрал «отдать посылку» — завершать по F, иначе — пробелом.
        if (string.Equals(title, "Client_Day1.4", StringComparison.OrdinalIgnoreCase))
            return DialogueLua.GetVariable("ChoseToGivePackage5577").AsBool;

        if (finishByKeyConversations == null || finishByKeyConversations.Length == 0)
            return false;

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
            npcSubtitleImage.color = npcSubtitlePanelBaseColor;
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
        if (hideOnChoiceMode != null)
        {
            for (int i = 0; i < hideOnChoiceMode.Length; i++)
            {
                GameObject go = hideOnChoiceMode[i];
                if (go != null)
                    go.SetActive(visible);
            }
        }
        if (namePlateHideOnChoiceCanvas != null)
            namePlateHideOnChoiceCanvas.gameObject.SetActive(visible && _namePlateVisibleByClientConversation);
        for (int i = 0; i < _hideOnChoiceModeRuntime.Count; i++)
        {
            GameObject go = _hideOnChoiceModeRuntime[i];
            if (go != null)
                go.SetActive(visible);
        }
    }

    public void AddToHideOnChoiceMode(GameObject go)
    {
        if (go != null && !_hideOnChoiceModeRuntime.Contains(go))
            _hideOnChoiceModeRuntime.Add(go);
    }

    public void AddToHideOnChoiceMode(Image image)
    {
        if (image != null)
            AddToHideOnChoiceMode(image.gameObject);
    }

    private void UpdateNamePlateFromClientMap(Subtitle subtitle)
    {
        if (clientPortraitMap == null || namePlateHideOnChoiceCanvas == null) return;
        if (subtitle?.dialogueEntry == null)
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        string conversationTitle = null;
        if (DialogueManager.masterDatabase != null)
        {
            var conv = DialogueManager.masterDatabase.GetConversation(subtitle.dialogueEntry.conversationID);
            if (conv != null) conversationTitle = conv.Title;
        }
        if (string.IsNullOrEmpty(conversationTitle))
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        int stepIndex = FindStepIndexByConversation(clientPortraitMap, conversationTitle);
        if (stepIndex < 0)
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        int entryID = subtitle.dialogueEntry.id;
        if (!clientPortraitMap.TryGetRule(stepIndex, entryID, out var rule) && !clientPortraitMap.TryGetRule(stepIndex, 0, out rule))
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        bool showLeft = rule.nameSprite != null;
        bool showRight = rule.nameSpriteRight != null;
        bool showAny = showLeft || showRight;
        _namePlateVisibleByClientConversation = showAny;
        namePlateHideOnChoiceCanvas.gameObject.SetActive(showAny);
        if (showAny)
        {
            if (namePlateImageLeft != null)
            {
                namePlateImageLeft.gameObject.SetActive(showLeft);
                if (showLeft)
                {
                    namePlateImageLeft.sprite = rule.nameSprite;
                    namePlateImageLeft.color = rule.nameSpriteColor.a < 0.001f ? Color.white : rule.nameSpriteColor;
                }
            }
            if (namePlateImageRight != null)
            {
                namePlateImageRight.gameObject.SetActive(showRight);
                if (showRight)
                {
                    namePlateImageRight.sprite = rule.nameSpriteRight;
                    namePlateImageRight.color = rule.nameSpriteColorRight.a < 0.001f ? Color.white : rule.nameSpriteColorRight;
                }
            }
            namePlateHideOnChoiceCanvas.sortingOrder = namePlateCanvasSortOrder;
        }
    }

    private static int FindStepIndexByConversation(ClientPortraitMap map, string conversation)
    {
        if (map == null || map.steps == null) return -1;
        for (int i = 0; i < map.steps.Count; i++)
        {
            if (string.Equals(map.steps[i].conversation, conversation, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
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

    /// <summary>
    /// Плашка Space видна только когда не радио и не ожидаем F. При прослушивании радио (авто-лист или блок по таймлайну) и при «нажми F» — скрыта.
    /// </summary>
    private void RefreshSpacePlaqueVisibility()
    {
        bool visible = !_forcedAutoAdvanceEnabled && !_awaitFinish && !_manualAdvanceBlocked;
        SetAutoAdvanceHiddenObjectsVisible(visible);
    }

    /// <summary>
    /// Когда ждём F для перехода на склад: скрываем плашку Space, показываем плашку F на игроке.
    /// Когда не ждём F — обновляем видимость плашки Space (учитывая радио) и скрываем плашку F.
    /// </summary>
    private void RefreshAwaitingFinishKeyUI()
    {
        if (_awaitFinish)
        {
            RefreshSpacePlaqueVisibility();
            PressFToWarehouseHintView.Instance?.Show();
        }
        else
        {
            RefreshSpacePlaqueVisibility();
            PressFToWarehouseHintView.Instance?.Hide();
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