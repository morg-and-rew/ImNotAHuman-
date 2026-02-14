using UnityEngine;

public struct KeyBinding
{
    public KeyCode MainKey;
    public KeyCode Modifier;

    public KeyBinding(KeyCode main, KeyCode modifier = KeyCode.None)
    {
        MainKey = main;
        Modifier = modifier;
    }

    public bool IsPressed()
    {
        if (Modifier != KeyCode.None && !Input.GetKey(Modifier))
            return false;

        return Input.GetKey(MainKey);
    }

    public bool IsPressedDown()
    {
        if (Modifier != KeyCode.None && !Input.GetKey(Modifier))
            return false;

        return Input.GetKeyDown(MainKey);
    }
}
