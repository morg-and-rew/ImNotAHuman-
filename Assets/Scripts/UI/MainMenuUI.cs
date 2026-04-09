using System;
using PixelCrushers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Scene-driven main menu. Assign buttons and optional background video in Inspector.
/// </summary>
public sealed class MainMenuUI : MonoBehaviour
{
    [Serializable]
    private sealed class LocalizedButtonSprite
    {
        public Button button;
        public Sprite russianSprite;
        public Sprite englishSprite;
    }

    [Header("Buttons")]
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _optionsButton;
    [SerializeField] private Button _exitButton;

    [Header("Localized Button Sprites (optional)")]
    [SerializeField] private LocalizedButtonSprite _continueButtonSprites;
    [SerializeField] private LocalizedButtonSprite _newGameButtonSprites;
    [SerializeField] private LocalizedButtonSprite _optionsButtonSprites;
    [SerializeField] private LocalizedButtonSprite _exitButtonSprites;

    [Header("Optional Label Overrides")]
    [SerializeField] private TMP_Text _continueText;
    [SerializeField] private TMP_Text _newGameText;
    [SerializeField] private TMP_Text _optionsText;
    [SerializeField] private TMP_Text _exitText;

    [Header("Optional Background Video")]
    [SerializeField] private VideoPlayer _backgroundVideo;
    [SerializeField] private VideoClip _backgroundClip;
    [SerializeField] private bool _forceVideoLoop = true;
    [Tooltip("Optional RawImage that should display the video. If assigned, script routes VideoPlayer to this UI element.")]
    [SerializeField] private RawImage _backgroundVideoRawImage;
    [Tooltip("Optional target render texture for menu video. If empty and RawImage is assigned, it will be auto-created.")]
    [SerializeField] private RenderTexture _backgroundVideoRenderTexture;

    private Action _onContinue;
    private Action _onNewGame;
    private Action _onOptions;
    private Action _onExit;

    public void Configure(
        bool canContinue,
        Action onContinue,
        Action onNewGame,
        Action onOptions,
        Action onExit)
    {
        _onContinue = onContinue;
        _onNewGame = onNewGame;
        _onOptions = onOptions;
        _onExit = onExit;

        RebindButton(_continueButton, () => _onContinue?.Invoke());
        RebindButton(_newGameButton, () => _onNewGame?.Invoke());
        RebindButton(_optionsButton, () => _onOptions?.Invoke());
        RebindButton(_exitButton, () => _onExit?.Invoke());

        ApplyLocalizedButtonSprites();
        SetButtonsInteractable(canContinue, newGameEnabled: true, exitEnabled: true, optionsEnabled: true);
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

    public void SetVisible(bool value)
    {
        gameObject.SetActive(value);
        if (value) PlayBackgroundVideo();
        else StopBackgroundVideo();
    }

    private void OnEnable()
    {
        ApplyLocalizedButtonSprites();
        PlayBackgroundVideo();
    }

    private void OnDisable()
    {
        StopBackgroundVideo();
    }

    private static void RebindButton(Button button, Action callback)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        if (callback != null) button.onClick.AddListener(() => callback());
    }

    private void PlayBackgroundVideo()
    {
        if (_backgroundVideo == null) return;
        if (_backgroundClip != null && _backgroundVideo.clip != _backgroundClip)
            _backgroundVideo.clip = _backgroundClip;
        ConfigureVideoOutput();
        if (_forceVideoLoop) _backgroundVideo.isLooping = true;
        _backgroundVideo.playOnAwake = false;
        _backgroundVideo.waitForFirstFrame = true;
        if (!_backgroundVideo.isPlaying) _backgroundVideo.Play();
    }

    private void StopBackgroundVideo()
    {
        if (_backgroundVideo != null && _backgroundVideo.isPlaying)
            _backgroundVideo.Stop();
    }

    private void ApplyLocalizedButtonSprites()
    {
        bool useEnglish = IsEnglishLanguage();
        ApplyLocalizedButtonSprite(_continueButtonSprites, useEnglish);
        ApplyLocalizedButtonSprite(_newGameButtonSprites, useEnglish);
        ApplyLocalizedButtonSprite(_optionsButtonSprites, useEnglish);
        ApplyLocalizedButtonSprite(_exitButtonSprites, useEnglish);
    }

    private static void ApplyLocalizedButtonSprite(LocalizedButtonSprite data, bool useEnglish)
    {
        if (data == null || data.button == null || data.button.image == null) return;
        Sprite sprite = useEnglish ? data.englishSprite : data.russianSprite;
        if (sprite != null) data.button.image.sprite = sprite;
    }

    private static bool IsEnglishLanguage()
    {
        if (GameFlowController.Instance != null)
            return GameFlowController.Instance.IsUiEnglishLocale;

        string lang = "";
        if (UILocalizationManager.instance != null)
            lang = UILocalizationManager.instance.currentLanguage ?? "";
        else
            lang = PlayerPrefs.GetString("Language", "");

        return GameFlowController.LocaleIndicatesEnglish(lang);
    }

    private void ConfigureVideoOutput()
    {
        if (_backgroundVideoRawImage == null) return;

        if (_backgroundVideoRenderTexture == null)
        {
            _backgroundVideoRenderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "MainMenuVideoRT"
            };
        }

        _backgroundVideo.renderMode = VideoRenderMode.RenderTexture;
        _backgroundVideo.targetTexture = _backgroundVideoRenderTexture;
        _backgroundVideoRawImage.texture = _backgroundVideoRenderTexture;
    }
}

