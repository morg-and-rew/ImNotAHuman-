using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// При наведении: цвет текста и Image (белый чуть прозрачный), при желании — смена спрайта. Настраивается из CustomDialogueUI.
/// </summary>
public sealed class ResponseButtonHoverColors : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Graphic textGraphic;
    [SerializeField] private Image targetImage;

    private Color _normalTextColor;
    private Color _hoverTextColor;
    private Color _normalImageColor;
    private Color _hoverImageColor;
    private Sprite _normalSprite;
    private Sprite _hoverSprite;

    public void Setup(Graphic text, Image image, Color normalText, Color hoverText, Color hoverImageColor, Sprite hoverSprite)
    {
        textGraphic = text;
        targetImage = image;
        _normalTextColor = normalText;
        _hoverTextColor = hoverText;
        _normalImageColor = image != null ? image.color : Color.white;
        _hoverImageColor = hoverImageColor;
        _normalSprite = image != null ? image.sprite : null;
        _hoverSprite = hoverSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (textGraphic != null) textGraphic.color = _hoverTextColor;
        if (targetImage != null)
        {
            if (_hoverSprite != null) targetImage.sprite = _hoverSprite;
            targetImage.color = _hoverImageColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (textGraphic != null) textGraphic.color = _normalTextColor;
        if (targetImage != null)
        {
            if (_normalSprite != null) targetImage.sprite = _normalSprite;
            targetImage.color = _normalImageColor;
        }
    }
}
