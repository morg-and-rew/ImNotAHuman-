using UnityEngine;

public sealed class CursorController : MonoBehaviour
{
    [SerializeField] private Texture2D _defaultCursor;
    [SerializeField] private Texture2D _hoverCursor;

    private bool _isHover;

    private void Start()
    {
        SetDefault();
    }

    public void SetHover(bool hover)
    {
        if (_isHover == hover)
            return;

        _isHover = hover;

        if (_isHover)
            SetHoverCursor();
        else
            SetDefault();
    }

    public void EnterUIMode()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SetHover(false);
    }

    public void ExitUIMode()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        SetHover(false);
    }

    private void SetDefault()
    {
        if (_defaultCursor == null) return;
        Vector2 hotspot = new Vector2(_defaultCursor.width / 2f, _defaultCursor.height / 2f);
        Cursor.SetCursor(_defaultCursor, hotspot, CursorMode.Auto);
    }

    private void SetHoverCursor()
    {
        if (_hoverCursor == null) return;
        Vector2 hotspot = new Vector2(_hoverCursor.width / 2f, _hoverCursor.height / 2f);
        Cursor.SetCursor(_hoverCursor, hotspot, CursorMode.Auto);
    }
}
