using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime UI стартового меню: фон (Image) + кнопки.
/// Не зависит от наличия Canvas/Prefab в сцене.
/// </summary>
public sealed class MainMenuUI : MonoBehaviour
{
    private const float DefaultButtonWidth = 340f;
    private const float DefaultButtonHeight = 56f;
    private const float DefaultButtonSpacing = 14f;

    private Button _continueButton;
    private Button _newGameButton;
    private Button _optionsButton;
    private Button _exitButton;

    private TMP_Text _continueText;
    private TMP_Text _newGameText;
    private TMP_Text _optionsText;
    private TMP_Text _exitText;

    private GameObject _root;

    public static MainMenuUI Create(
        Sprite backgroundSprite,
        TMP_FontAsset font,
        bool canContinue,
        Action onContinue,
        Action onNewGame,
        Action onOptions,
        Action onExit)
    {
        var go = new GameObject("MainMenuUI");
        var ui = go.AddComponent<MainMenuUI>();
        ui.Build(backgroundSprite, font, canContinue, onContinue, onNewGame, onOptions, onExit);
        return ui;
    }

    public void SetButtonsInteractable(bool canContinue, bool newGameEnabled, bool exitEnabled, bool optionsEnabled)
    {
        if (_continueButton != null) _continueButton.interactable = canContinue;
        if (_newGameButton != null) _newGameButton.interactable = newGameEnabled;
        if (_exitButton != null) _exitButton.interactable = exitEnabled;
        if (_optionsButton != null) _optionsButton.interactable = optionsEnabled;

        // TextMeshPro не всегда корректно тускнеет вместе с Button.colors в зависимости от TMP настройки,
        // поэтому вручную подсветим disabled-текст.
        if (_continueText != null) _continueText.color = canContinue ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    private void Build(
        Sprite backgroundSprite,
        TMP_FontAsset font,
        bool canContinue,
        Action onContinue,
        Action onNewGame,
        Action onOptions,
        Action onExit)
    {
        EnsureEventSystemExists();

        _root = new GameObject("Root");
        _root.transform.SetParent(transform, worldPositionStays: false);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        CreateBackground(backgroundSprite);
        CreateButtons(font, onContinue, onNewGame, onOptions, onExit, canContinue);
    }

    private void CreateBackground(Sprite backgroundSprite)
    {
        var bg = new GameObject("Background");
        bg.transform.SetParent(_root.transform, worldPositionStays: false);

        var rt = bg.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var img = bg.AddComponent<Image>();
        img.sprite = backgroundSprite ?? CreateSolidSprite();
        img.type = Image.Type.Sliced;
        img.preserveAspect = true;
        img.color = backgroundSprite != null ? Color.white : new Color(0f, 0f, 0f, 1f);

        // Тонкая затемняющая вуаль поверх картинки, чтобы кнопки читались.
        var overlay = new GameObject("BackgroundOverlay");
        overlay.transform.SetParent(bg.transform, worldPositionStays: false);

        var overlayRt = overlay.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        overlayRt.anchoredPosition = Vector2.zero;

        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.sprite = CreateSolidSprite();
        overlayImg.color = new Color(0f, 0f, 0f, 0.35f);
    }

    private void CreateButtons(
        TMP_FontAsset font,
        Action onContinue,
        Action onNewGame,
        Action onOptions,
        Action onExit,
        bool canContinue)
    {
        var container = new GameObject("ButtonsContainer");
        container.transform.SetParent(_root.transform, worldPositionStays: false);

        var containerRt = container.AddComponent<RectTransform>();
        // Прижимаем к правому краю, как на скриншоте.
        containerRt.anchorMin = new Vector2(1f, 0.5f);
        containerRt.anchorMax = new Vector2(1f, 0.5f);
        containerRt.pivot = new Vector2(1f, 0.5f);
        containerRt.anchoredPosition = new Vector2(-360f, 0f);
        containerRt.sizeDelta = new Vector2(420f, 320f);

        var layout = container.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = DefaultButtonSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;

        _continueButton = CreateButton(
            "CONTINUE",
            font,
            container.transform,
            DefaultButtonWidth,
            DefaultButtonHeight,
            onContinue,
            out _continueText);

        _newGameButton = CreateButton(
            "NEW GAME",
            font,
            container.transform,
            DefaultButtonWidth,
            DefaultButtonHeight,
            onNewGame,
            out _newGameText);

        _optionsButton = CreateButton(
            "OPTIONS",
            font,
            container.transform,
            DefaultButtonWidth,
            DefaultButtonHeight,
            onOptions,
            out _optionsText);

        _exitButton = CreateButton(
            "EXIT",
            font,
            container.transform,
            DefaultButtonWidth,
            DefaultButtonHeight,
            onExit,
            out _exitText);

        // Начальные состояния.
        SetButtonsInteractable(canContinue, newGameEnabled: true, exitEnabled: true, optionsEnabled: true);
    }

    private Button CreateButton(
        string label,
        TMP_FontAsset font,
        Transform parent,
        float width,
        float height,
        Action onClick,
        out TMP_Text text)
    {
        var btnGo = new GameObject(label);
        btnGo.transform.SetParent(parent, worldPositionStays: false);

        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.sizeDelta = new Vector2(width, height);

        // Outer = рамка, Inner = область кнопки под tint'ы.
        var outer = new GameObject("OuterBackground");
        outer.transform.SetParent(btnGo.transform, worldPositionStays: false);
        var outerRt = outer.AddComponent<RectTransform>();
        outerRt.anchorMin = Vector2.zero;
        outerRt.anchorMax = Vector2.one;
        outerRt.offsetMin = Vector2.zero;
        outerRt.offsetMax = Vector2.zero;

        var outerImg = outer.AddComponent<Image>();
        outerImg.sprite = CreateSolidSprite();
        outerImg.color = new Color(0.55f, 0.55f, 0.55f, 0.75f);

        var inner = new GameObject("InnerBackground");
        inner.transform.SetParent(btnGo.transform, worldPositionStays: false);
        var innerRt = inner.AddComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(2f, 2f);
        innerRt.offsetMax = new Vector2(-2f, -2f);

        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = CreateSolidSprite();
        innerImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        var button = btnGo.AddComponent<Button>();
        button.targetGraphic = innerImg;

        var colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.16f, 0.16f, 0.95f);
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.98f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 0.98f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.16f, 0.16f, 0.16f, 0.25f);
        button.colors = colors;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, worldPositionStays: false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var t = textGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.font = font;
        t.fontSize = 28;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;

        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(1f, -1f);

        text = t;
        return button;
    }

    private static Sprite CreateSolidSprite()
    {
        // Небольшая фабрика 1x1 белого спрайта для Image.
        // Чтобы не плодить текстуры, можно было бы кэшировать, но это меню создаётся 1 раз.
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}

