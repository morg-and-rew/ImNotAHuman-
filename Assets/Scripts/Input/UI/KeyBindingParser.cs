using UnityEngine;

public static class KeyBindingParser
{
    public static bool TryParse(
        string input,
        out KeyBinding binding,
        out string error)
    {
        binding = default;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "?????? ????????";
            return false;
        }

        input = input.Trim().ToUpperInvariant();

        string[] parts = input.Split('+');

        if (parts.Length == 1)
        {
            if (!TryParseMainKey(parts[0], out KeyCode key))
            {
error = "???????????? ???????";
            return false;
        }

        binding = new KeyBinding(key);
            return true;
        }

        if (parts.Length == 2 && parts[1] == "SHIFT")
        {
            if (!TryParseMainKey(parts[0], out KeyCode key))
            {
error = "???????????? ???????";
            return false;
        }

        binding = new KeyBinding(key, KeyCode.LeftShift);
            return true;
        }

        error = "????????? ?????? ?????????? ? Shift";
        return false;
    }

    private static bool TryParseMainKey(string token, out KeyCode key)
    {
        key = KeyCode.None;

        if (token.Length != 1)
            return false;

        char c = token[0];

        if (c >= 'A' && c <= 'Z')
        {
            key = (KeyCode)c;
            return true;
        }

        if (c >= '0' && c <= '9')
        {
            key = KeyCode.Alpha0 + (c - '0');
            return true;
        }

        return false;
    }
}
