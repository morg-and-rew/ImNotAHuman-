using UnityEngine;

public sealed class PlayerInputPC : IPlayerInput
{
    private readonly PlayerKeyBindings _bindings;

    private bool _wasDropHeld;

    public PlayerInputPC(PlayerKeyBindings bindings)
    {
        _bindings = bindings;
    }

    public Vector2 MoveAxis
    {
        get
        {
            float x = 0f;
            float y = 0f;

            if (_bindings.Get(InputAction.Left).IsPressed()) x -= 1f;
            if (_bindings.Get(InputAction.Right).IsPressed()) x += 1f;
            if (_bindings.Get(InputAction.Forward).IsPressed()) y += 1f;
            if (_bindings.Get(InputAction.Backward).IsPressed()) y -= 1f;

            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }
    }


    public Vector2 LookDelta =>
        new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    public bool InteractPressed =>
        _bindings.Get(InputAction.Interact).IsPressedDown();

    public bool UseItemPressed =>
        _bindings.Get(InputAction.UseItem).IsPressedDown();

    public bool NextPressed =>
        _bindings.Get(InputAction.Next).IsPressedDown();

    public bool ConfirmPressed =>
        _bindings.Get(InputAction.EndDialog).IsPressedDown();


    public bool DropItemPressed =>
        _bindings.Get(InputAction.DropItem).IsPressed() && !_wasDropHeld;

    public bool IsDropHeld =>
        _bindings.Get(InputAction.DropItem).IsPressed();

    public bool DropItemReleased =>
        _wasDropHeld && !_bindings.Get(InputAction.DropItem).IsPressed();

    public void Update()
    {
        _wasDropHeld = _bindings.Get(InputAction.DropItem).IsPressed();
    }
}

public enum InputAction
{
    Forward,
    Backward,
    Left,
    Right,
    Interact,
    UseItem,
    DropItem,
    Next,
    EndDialog
}
