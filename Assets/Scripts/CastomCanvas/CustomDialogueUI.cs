using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PixelCrushers;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if TMP_PRESENT
using TMPro;
#endif

public sealed class CustomDialogueUI : StandardDialogueUI, ICustomDialogueUI
{
    [Header("Advance")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;
    [SerializeField] private bool advanceOnlyWhenNoResponses = true;
    [Tooltip("Минимальная задержка между нажатиями пробела (сек). 0 = без задержки. Плашка Space появляется после этой задержки.")]
    [SerializeField, Min(0f)] private float manualAdvanceMinInterval = 1f;
    [Header("Forced Auto Advance")]
    [SerializeField, Min(0.1f)] private float autoAdvanceIntervalSeconds = 10f;
    [SerializeField] private GameObject[] hideOnForcedAutoAdvanceMode;
    [Tooltip("Диалоги, которые должны сами пролистываться (как радио). Добавь сюда название conversation при необходимости.")]
    [SerializeField] private string[] autoAdvanceConversations = new string[0];
    [SerializeField, Min(0.1f)] private float autoAdvanceIntervalForList = 8f;

    [Header("Finish (Only for selected conversations)")]
    [SerializeField] private KeyCode finishKey = KeyCode.F;

    [SerializeField] private string[] finishByKeyConversations;

    [Header("Key Hint Plaque (одна плашка внизу — меняем спрайт Space / F)")]
    [Tooltip("Image плашки внизу диалога. Если задан, спрайт переключается между Space и F в зависимости от состояния. Не добавляй этот объект в Hide On Forced Auto Advance Mode.")]
    [SerializeField] private Image keyHintPlaqueImage;
    [Tooltip("Спрайт для «перелистнуть» (пробел).")]
    [SerializeField] private Sprite keyHintSpaceSprite;
    [Tooltip("Спрайт для «завершить и на склад» (F).")]
    [SerializeField] private Sprite keyHintFinishSprite;
    [Tooltip("Если включено, при показе плашки F позиция берётся из Key Hint Plaque Position When F; иначе плашка остаётся на месте (как в сцене).")]
    [SerializeField] private bool keyHintPlaqueUseCustomPositionForF;
    [Tooltip("Anchored position плашки, когда показывается F. Используется только если включено Key Hint Plaque Use Custom Position For F.")]
    [SerializeField] private Vector2 keyHintPlaquePositionWhenF;
    [SerializeField, Min(0.01f)] private float keyHintPlaqueFadeDuration = 0.18f;
    [Header("Key Hint Plaque — режим выбора (цвет плашки и текст белый)")]
    [Tooltip("Цвет Image плашки при возможности выбора. Если alpha = 0, смена цвета не применяется.")]
    [SerializeField] private Color keyHintPlaqueChoiceColor = new Color(82f / 255f, 79f / 255f, 13f / 255f, 1f);
    [Tooltip("Цвет Image плашки в обычное время (когда нет выбора) — белый = без подкрашивания, как в префабе.")]
    [SerializeField] private Color keyHintPlaqueNormalColor = Color.white;
    [Tooltip("Текст на плашке (UI Text). В режиме выбора становится белым.")]
    [SerializeField] private Text keyHintPlaqueText;
    [Tooltip("Цвет текста плашки в обычное время (когда нет выбора) — белый, как обычно у подсказки клавиши.")]
    [SerializeField] private Color keyHintPlaqueTextNormalColor = Color.white;

    [Header("Panels")]
    [SerializeField] private GameObject npcSubtitlePanel;
    [SerializeField] private GameObject[] hideOnChoiceMode;
    [Header("Name Plate (плашки имён — только во время разговора с клиентом, когда говорят двое — две плашки)")]
    [Tooltip("Канвас плашек имени со сцены. Виден только во время диалога с клиентом, спрайты из мапы. Скрывается при выборе (Hide On choice mode).")]
    [SerializeField] private Canvas namePlateHideOnChoiceCanvas;
    [Tooltip("Image для имени левого говорящего.")]
    [SerializeField] private Image namePlateImageLeft;
    [Tooltip("Image для имени правого говорящего.")]
    [SerializeField] private Image namePlateImageRight;
    [Tooltip("Мапа спрайтов имён (плашки «Бабушка», «Клиент»). Если нет — плашки имён не показываются. Порядок шагов и conversation как в Client Portrait Map.")]
    [SerializeField] private ClientNamePlateMap clientNamePlateMap;
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
    [Tooltip("Если включено — подставляем цвет фона (Image) кнопок. Если выключено — цвет берётся из шаблона (один Image, цвет уже верный).")]
    [SerializeField] private bool applyResponseButtonImageColor = false;
    [SerializeField] private Color responseButtonImageColor = new Color(82f / 255f, 79f / 255f, 13f / 255f, 1f);
    [Header("Response Buttons — при наведении (кнопки из шаблона, настройки только здесь)")]
    [Tooltip("Цвет текста при наведении на кнопку ответа. Обычный цвет текста — выше, «Response Button Text Color».")]
    [SerializeField] private Color responseButtonHoverTextColor = Color.white;
    [Tooltip("Цвет Image при наведении — полностью белый и немного прозрачный.")]
    [SerializeField] private Color responseButtonHoverImageColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("Спрайт Image кнопки при наведении — картинка кнопки меняется полностью (опционально).")]
    [SerializeField] private Sprite responseButtonHoverSprite;

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
    private Vector2 _keyHintPlaqueNormalPosition;
    private CanvasGroup _keyHintPlaqueCanvasGroup;
    private float _keyHintPlaqueTargetAlpha;

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
        if (keyHintPlaqueImage != null)
        {
            _keyHintPlaqueNormalPosition = keyHintPlaqueImage.rectTransform.anchoredPosition;
            EnsureKeyHintPlaqueCanvasGroup();
            SetKeyHintPlaqueAlphaImmediate(0f);
            keyHintPlaqueImage.gameObject.SetActive(false);
        }
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
        TickKeyHintPlaqueFade();

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

        // Обновляем видимость плашки Space, чтобы она появилась сразу после истечения задержки
        if (!_inChoiceMode && !_manualAdvanceBlocked && manualAdvanceMinInterval > 0f)
            RefreshSpacePlaqueVisibility();

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
        // Сразу пересчитать видимость Space/F-плашек, чтобы Space не оставался на экране в момент входа в выбор.
        RefreshAwaitingFinishKeyUI();
        RefreshChoiceModeHiddenObjectsVisibility();
        ApplyKeyHintPlaqueChoiceState(true);
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

        // Сбросить подсветку (плашку) выбранного ответа, чтобы при следующем показе меню не оставался старый спрайт/цвет
        var allButtons = GetAllResponseButtons();
        for (int i = 0; i < allButtons.Count; i++)
        {
            var hover = allButtons[i].GetComponent<ResponseButtonHoverColors>();
            if (hover != null) hover.ResetToNormal();
        }
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        base.HideResponses();

        _inChoiceMode = false;
        _isThreeChoicesMode = false;
        ApplyKeyHintPlaqueChoiceState(false);
        // После выхода из выбора немедленно вернуть корректную видимость плашек Space/F.
        RefreshAwaitingFinishKeyUI();
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

    private List<StandardUIResponseButton> GetAllResponseButtons()
    {
        var allButtons = new List<StandardUIResponseButton>();
        if (conversationUIElements?.menuPanels == null) return allButtons;

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
        return allButtons;
    }

    private void ApplyResponseButtonColors()
    {
        var allButtons = GetAllResponseButtons();
        for (int i = 0; i < allButtons.Count; i++)
            ApplyResponseButtonStyleToSingle(allButtons[i]);
    }

    private void ApplyResponseButtonStyleToSingle(StandardUIResponseButton rb)
    {
        bool wasNull = rb != null && rb.label != null && UITextField.IsNull(rb.label);
        EnsureResponseButtonLabelAssigned(rb);
        if (UITextField.IsNull(rb.label)) return;
        // Если label только что подставили — текст из диалога ещё не попал в компонент, выставляем из response
        if (wasNull && rb.response != null)
            rb.SetFormattedText(rb.response.formattedText);

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

        Image plaqueImage = rb.transform.Find("Background")?.GetComponent<Image>();
        if (plaqueImage == null && rb.button != null)
            plaqueImage = rb.button.image;
        if (applyResponseButtonImageColor && plaqueImage != null)
            plaqueImage.color = responseButtonImageColor;

        // Наведение: текст меняет цвет, Image меняется полностью (спрайт)
        Graphic textGraphic = rb.label.uiText;
#if TMP_PRESENT
        if (textGraphic == null && rb.label.textMeshProUGUI != null) textGraphic = rb.label.textMeshProUGUI;
#endif
        if (textGraphic != null || plaqueImage != null)
        {
            var hover = rb.GetComponent<ResponseButtonHoverColors>();
            if (hover == null) hover = rb.gameObject.AddComponent<ResponseButtonHoverColors>();
            hover.Setup(textGraphic, plaqueImage, responseButtonTextColor, responseButtonHoverTextColor, responseButtonHoverImageColor, responseButtonHoverSprite);
        }
    }

    /// <summary>
    /// Если у кнопки ответа не задан Label (текст создаётся из шаблона и ссылку некуда перенести) — ищем Text или TextMeshPro среди дочерних и подставляем в label.
    /// </summary>
    private static void EnsureResponseButtonLabelAssigned(StandardUIResponseButton rb)
    {
        if (rb == null || rb.label == null) return;
        if (!UITextField.IsNull(rb.label)) return;

        Text uiText = rb.GetComponentInChildren<Text>(true);
        if (uiText != null)
        {
            rb.label.uiText = uiText;
            return;
        }
#if TMP_PRESENT
        TextMeshProUGUI tmp = rb.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
            rb.label.textMeshProUGUI = tmp;
#endif
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
        if (clientNamePlateMap == null || namePlateHideOnChoiceCanvas == null) return;
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

        if (clientNamePlateMap == null)
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        int stepIndex = clientNamePlateMap.FindStepIndexByConversation(conversationTitle);
        if (stepIndex < 0)
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        int entryID = subtitle.dialogueEntry.id;
        if (!clientNamePlateMap.TryGetRule(stepIndex, entryID, out var nameRule))
        {
            _namePlateVisibleByClientConversation = false;
            namePlateHideOnChoiceCanvas.gameObject.SetActive(false);
            return;
        }

        bool showLeft = nameRule.nameSprite != null;
        bool showRight = nameRule.nameSpriteRight != null;
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
                    namePlateImageLeft.sprite = nameRule.nameSprite;
                    namePlateImageLeft.color = nameRule.nameSpriteColor.a < 0.001f ? Color.white : nameRule.nameSpriteColor;
                }
            }
            if (namePlateImageRight != null)
            {
                namePlateImageRight.gameObject.SetActive(showRight);
                if (showRight)
                {
                    namePlateImageRight.sprite = nameRule.nameSpriteRight;
                    namePlateImageRight.color = nameRule.nameSpriteColorRight.a < 0.001f ? Color.white : nameRule.nameSpriteColorRight;
                }
            }
            namePlateHideOnChoiceCanvas.sortingOrder = namePlateCanvasSortOrder;
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

    /// <summary>
    /// Плашка Space видна только когда можно перелистнуть: не радио, не выбор ответа, не ожидаем F, и прошла задержка manualAdvanceMinInterval.
    /// Если задан keyHintPlaqueImage — одна плашка внизу переключает спрайт (Space / F) и показывается когда можно листать или ждём F.
    /// </summary>
    private void RefreshSpacePlaqueVisibility()
    {
        bool canAdvanceNow = manualAdvanceMinInterval <= 0f || Time.unscaledTime >= _nextManualAdvanceAllowedAt;
        bool spaceVisible = !_forcedAutoAdvanceEnabled && !_awaitFinish && !_manualAdvanceBlocked && !_inChoiceMode && canAdvanceNow;
        SetAutoAdvanceHiddenObjectsVisible(spaceVisible);
        RefreshKeyHintPlaqueSprite(canAdvanceNow);
    }

    /// <summary>
    /// Когда ждём F: плашка внизу переключается на спрайт F (если задан keyHintPlaqueImage), показываем плашку F на игроке.
    /// Когда не ждём F — обновляем видимость плашки Space / ключа и скрываем плашку F на игроке.
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

    /// <summary>
    /// Одна плашка внизу: показываем когда можно листать, ждём F или режим выбора; спрайт — Space или F; в режиме выбора — цвет и текст по настройкам.
    /// </summary>
    private void RefreshKeyHintPlaqueSprite(bool canAdvanceNow)
    {
        if (keyHintPlaqueImage == null) return;
        bool visible = !_forcedAutoAdvanceEnabled && !_manualAdvanceBlocked && !_inChoiceMode && (canAdvanceNow || _awaitFinish);
        if (visible)
        {
            bool showingF = _awaitFinish && keyHintFinishSprite != null;
            if (showingF)
                keyHintPlaqueImage.sprite = keyHintFinishSprite;
            else if (!_awaitFinish && keyHintSpaceSprite != null)
                keyHintPlaqueImage.sprite = keyHintSpaceSprite;
            if (keyHintPlaqueUseCustomPositionForF)
                keyHintPlaqueImage.rectTransform.anchoredPosition = showingF ? keyHintPlaquePositionWhenF : _keyHintPlaqueNormalPosition;
            ApplyKeyHintPlaqueChoiceState(_inChoiceMode);
        }
        SetKeyHintPlaqueVisible(visible);
    }

    /// <summary>
    /// В режиме выбора: цвет плашки по keyHintPlaqueChoiceColor, текст — белый. Иначе — нормальные цвета.
    /// </summary>
    private void ApplyKeyHintPlaqueChoiceState(bool isChoiceMode)
    {
        if (keyHintPlaqueImage != null && keyHintPlaqueChoiceColor.a > 0.001f)
            keyHintPlaqueImage.color = isChoiceMode ? keyHintPlaqueChoiceColor : keyHintPlaqueNormalColor;
        if (keyHintPlaqueText != null)
            keyHintPlaqueText.color = isChoiceMode ? Color.white : keyHintPlaqueTextNormalColor;
    }

    private void EnsureKeyHintPlaqueCanvasGroup()
    {
        if (keyHintPlaqueImage == null)
            return;
        _keyHintPlaqueCanvasGroup = keyHintPlaqueImage.GetComponent<CanvasGroup>();
        if (_keyHintPlaqueCanvasGroup == null)
            _keyHintPlaqueCanvasGroup = keyHintPlaqueImage.gameObject.AddComponent<CanvasGroup>();
        _keyHintPlaqueCanvasGroup.interactable = false;
        _keyHintPlaqueCanvasGroup.blocksRaycasts = false;
    }

    private void SetKeyHintPlaqueVisible(bool visible)
    {
        if (keyHintPlaqueImage == null)
            return;

        if (_keyHintPlaqueCanvasGroup == null)
            EnsureKeyHintPlaqueCanvasGroup();

        _keyHintPlaqueTargetAlpha = visible ? 1f : 0f;
        if (visible && !keyHintPlaqueImage.gameObject.activeSelf)
            keyHintPlaqueImage.gameObject.SetActive(true);
    }

    private void TickKeyHintPlaqueFade()
    {
        if (keyHintPlaqueImage == null)
            return;

        if (_keyHintPlaqueCanvasGroup == null)
            EnsureKeyHintPlaqueCanvasGroup();

        if (_keyHintPlaqueCanvasGroup == null)
        {
            if (keyHintPlaqueImage.gameObject.activeSelf != (_keyHintPlaqueTargetAlpha > 0.5f))
                keyHintPlaqueImage.gameObject.SetActive(_keyHintPlaqueTargetAlpha > 0.5f);
            return;
        }

        float duration = Mathf.Max(0.01f, keyHintPlaqueFadeDuration);
        float step = Time.unscaledDeltaTime / duration;
        _keyHintPlaqueCanvasGroup.alpha = Mathf.MoveTowards(_keyHintPlaqueCanvasGroup.alpha, _keyHintPlaqueTargetAlpha, step);

        bool keepVisible = _keyHintPlaqueTargetAlpha > 0f || _keyHintPlaqueCanvasGroup.alpha > 0.001f;
        if (keyHintPlaqueImage.gameObject.activeSelf != keepVisible)
            keyHintPlaqueImage.gameObject.SetActive(keepVisible);
    }

    private void SetKeyHintPlaqueAlphaImmediate(float alpha)
    {
        if (_keyHintPlaqueCanvasGroup == null)
            EnsureKeyHintPlaqueCanvasGroup();
        if (_keyHintPlaqueCanvasGroup != null)
            _keyHintPlaqueCanvasGroup.alpha = Mathf.Clamp01(alpha);
        _keyHintPlaqueTargetAlpha = Mathf.Clamp01(alpha);
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