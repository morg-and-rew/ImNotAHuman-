using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Image _image;

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
        if (_image != null && _iconSprite != null)
            _image.sprite = _iconSprite;
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }
}
