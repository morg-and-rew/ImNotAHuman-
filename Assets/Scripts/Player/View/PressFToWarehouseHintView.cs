using UnityEngine;
using UnityEngine.UI;
using PixelCrushers;

/// <summary>
/// Плашка «Нажми F» (иконка) на игроке — показывается, когда после диалога нужно нажать F для перехода на склад.
/// Вешай на дочерний объект игрока с Canvas (World Space) и Image для иконки. По нажатию F плашка скрывается извне (CustomDialogueUI / GameFlowController).
/// </summary>
public sealed class PressFToWarehouseHintView : MonoBehaviour
{
    public static PressFToWarehouseHintView Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [Tooltip("Иконка «Нажми F». Если не задана, используется Image.sprite на объекте.")]
    [SerializeField] private Sprite _iconSprite;
    [Tooltip("Иконка «Press F» для английского языка. Если не задана — используется обычная иконка.")]
    [SerializeField] private Sprite _iconSpriteEnglish;
    [SerializeField] private Image _image;
    [SerializeField] private string _languagePlayerPrefsKey = "Language";
    [SerializeField] private string _englishLanguageCode = "en";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (_root != null) _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show()
    {
        if (_root == null) return;
        if (_image != null)
        {
            Sprite icon = ResolveLocalizedIcon();
            if (icon != null)
                _image.sprite = icon;
        }
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private Sprite ResolveLocalizedIcon()
    {
        if (IsEnglishLanguage() && _iconSpriteEnglish != null)
            return _iconSpriteEnglish;
        return _iconSprite;
    }

    private bool IsEnglishLanguage()
    {
        if (GameFlowController.Instance != null)
            return GameFlowController.Instance.IsUiEnglishLocale;

        string lang = "";
        if (UILocalizationManager.instance != null)
            lang = UILocalizationManager.instance.currentLanguage ?? "";
        else if (!string.IsNullOrWhiteSpace(_languagePlayerPrefsKey))
            lang = PlayerPrefs.GetString(_languagePlayerPrefsKey, "");

        return GameFlowController.LocaleIndicatesEnglish(lang);
    }
}
