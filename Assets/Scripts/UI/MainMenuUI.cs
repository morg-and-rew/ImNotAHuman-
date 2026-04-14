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

    [Header("Settings — подкнопки (язык / музыка / звук / назад)")]
    [Tooltip("Родитель с кнопками настроек; по умолчанию скрыт, показывается по клику «Настройки».")]
    [SerializeField] private GameObject _settingsSubPanel;
    [SerializeField] private Button _languageSettingsButton;
    [SerializeField] private Button _musicSettingsButton;
    [SerializeField] private Button _soundSettingsButton;
    [SerializeField] private Slider _musicSettingsSlider;
    [SerializeField] private Slider _soundSettingsSlider;
    [SerializeField] private Button _backSettingsButton;
    [SerializeField] private TMP_Text _languageSettingsLabel;
    [SerializeField] private TMP_Text _musicSettingsLabel;
    [SerializeField] private TMP_Text _soundSettingsLabel;

    [Header("Localized sprites — настройки (optional)")]
    [SerializeField] private LocalizedButtonSprite _languageSettingsButtonSprites;
    [SerializeField] private LocalizedButtonSprite _musicSettingsButtonSprites;
    [SerializeField] private LocalizedButtonSprite _soundSettingsButtonSprites;
    [SerializeField] private LocalizedButtonSprite _backSettingsButtonSprites;

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
    private Action _onLanguageSettings;
    private Action<float> _onMusicVolumeChanged;
    private Action<float> _onSoundVolumeChanged;
    private Action _onSettingsBack;

    public void Configure(
        bool canContinue,
        Action onContinue,
        Action onNewGame,
        Action onOptions,
        Action onExit,
        Action onLanguage = null,
        Action<float> onMusicVolumeChanged = null,
        Action<float> onSoundVolumeChanged = null,
        Action onSettingsBack = null)
    {
        _onContinue = onContinue;
        _onNewGame = onNewGame;
        _onOptions = onOptions;
        _onExit = onExit;
        _onLanguageSettings = onLanguage;
        _onMusicVolumeChanged = onMusicVolumeChanged;
        _onSoundVolumeChanged = onSoundVolumeChanged;
        _onSettingsBack = onSettingsBack;

        RebindButton(_continueButton, () => _onContinue?.Invoke());
        RebindButton(_newGameButton, () => _onNewGame?.Invoke());
        RebindButton(_optionsButton, () => _onOptions?.Invoke());
        RebindButton(_exitButton, () => _onExit?.Invoke());

        RebindButton(_languageSettingsButton, () => _onLanguageSettings?.Invoke());
        if (_musicSettingsSlider == null)
            RebindButton(_musicSettingsButton, () => _onMusicVolumeChanged?.Invoke(GameAudioSettings.MusicVolume01 < 0.5f ? 1f : 0f));
        else
            RebindButton(_musicSettingsButton, null);
        if (_soundSettingsSlider == null)
            RebindButton(_soundSettingsButton, () => _onSoundVolumeChanged?.Invoke(GameAudioSettings.SfxVolume01 < 0.5f ? 1f : 0f));
        else
            RebindButton(_soundSettingsButton, null);
        RebindButton(_backSettingsButton, () => _onSettingsBack?.Invoke());
        RebindSlider(_musicSettingsSlider, value => _onMusicVolumeChanged?.Invoke(value));
        RebindSlider(_soundSettingsSlider, value => _onSoundVolumeChanged?.Invoke(value));

        ApplyLocalizedButtonSprites();
        RefreshSettingsSubButtonLabels();
        SetButtonsInteractable(canContinue, newGameEnabled: true, exitEnabled: true, optionsEnabled: true);
    }

    public void ApplyLocalizedButtonSpritesPublic() => ApplyLocalizedButtonSprites();

    public void SetSettingsSubPanelVisible(bool visible)
    {
        if (_settingsSubPanel != null)
            _settingsSubPanel.SetActive(visible);
    }

    public void SetMainButtonsVisible(bool visible)
    {
        SetButtonVisible(_continueButton, visible);
        SetButtonVisible(_newGameButton, visible);
        SetButtonVisible(_optionsButton, visible);
        SetButtonVisible(_exitButton, visible);
    }

    public void RefreshSettingsSubButtonLabels()
    {
        GameAudioSettings.EnsureLoaded();
        bool en = IsEnglishLanguage();
        float music = GameAudioSettings.MusicVolume01;
        float sfx = GameAudioSettings.SfxVolume01;

        if (_musicSettingsSlider != null)
            _musicSettingsSlider.SetValueWithoutNotify(music);
        if (_soundSettingsSlider != null)
            _soundSettingsSlider.SetValueWithoutNotify(sfx);

        if (_languageSettingsLabel != null)
        {
            bool englishUi = GameFlowController.Instance != null && GameFlowController.Instance.IsUiEnglishLocale;
            _languageSettingsLabel.text = en
                ? (englishUi ? "Language: English" : "Language: Russian")
                : (englishUi ? "Язык: English" : "Язык: Русский");
        }

        if (_musicSettingsLabel != null)
        {
            int percent = Mathf.RoundToInt(music * 100f);
            _musicSettingsLabel.text = en
                ? $"Music: {percent}%"
                : $"Музыка: {percent}%";
        }

        if (_soundSettingsLabel != null)
        {
            int percent = Mathf.RoundToInt(sfx * 100f);
            _soundSettingsLabel.text = en
                ? $"Sound: {percent}%"
                : $"Звук: {percent}%";
        }
    }

    private void Awake()
    {
        if (_settingsSubPanel != null)
            _settingsSubPanel.SetActive(false);
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

    private static void RebindSlider(Slider slider, Action<float> callback)
    {
        if (slider == null) return;
        slider.onValueChanged.RemoveAllListeners();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        if (callback != null) slider.onValueChanged.AddListener(value => callback(value));
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        button.gameObject.SetActive(visible);
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
        ApplyLocalizedButtonSprite(_languageSettingsButtonSprites, useEnglish);
        ApplyLocalizedButtonSprite(_musicSettingsButtonSprites, useEnglish);
        ApplyLocalizedButtonSprite(_soundSettingsButtonSprites, useEnglish);
        ApplyLocalizedButtonSprite(_backSettingsButtonSprites, useEnglish);
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

