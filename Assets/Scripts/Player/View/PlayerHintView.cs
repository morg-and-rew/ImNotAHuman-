using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHintView : MonoBehaviour
{
    public static PlayerHintView Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Image _image;

    private Sprite _raycastSprite;
    private Sprite _windowSprite;
    private Sprite _doorSprite;
    private Sprite _clientSprite;

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

    public void SetRaycastHint(Sprite sprite)
    {
        _raycastSprite = sprite;
    }

    public void SetWindowHint(Sprite sprite)
    {
        _windowSprite = sprite;
    }

    public void SetDoorHint(Sprite sprite)
    {
        _doorSprite = sprite;
    }

    public void SetClientHint(Sprite sprite)
    {
        _clientSprite = sprite;
    }

    private void LateUpdate()
    {
        Sprite showSprite = _raycastSprite ?? _windowSprite ?? _doorSprite ?? _clientSprite;

        if (_root != null)
            _root.SetActive(showSprite != null);

        if (_image != null)
        {
            _image.enabled = showSprite != null;
            if (showSprite != null)
                _image.sprite = showSprite;
        }
    }
}
