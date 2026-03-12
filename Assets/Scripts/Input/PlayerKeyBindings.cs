using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerKeyBindings
{
    private readonly Dictionary<InputAction, KeyBinding> _bindings =
        new()
        {
            { InputAction.Forward,  new KeyBinding(KeyCode.W) },
            { InputAction.Backward, new KeyBinding(KeyCode.S) },
            { InputAction.Left,     new KeyBinding(KeyCode.A) },
            { InputAction.Right,    new KeyBinding(KeyCode.D) },

            { InputAction.Interact, new KeyBinding(KeyCode.E) },
            { InputAction.UseItem,  new KeyBinding(KeyCode.Mouse0) },
            { InputAction.DropItem, new KeyBinding(KeyCode.Mouse1) },
            { InputAction.Next, new KeyBinding(KeyCode.Space) },
            { InputAction.EndDialog, new KeyBinding(KeyCode.F) },
            { InputAction.RotateBox, new KeyBinding(KeyCode.Q) },
        };

    public KeyBinding Get(InputAction action)
        => _bindings[action];

    public bool TryRebind(
        InputAction action,
        KeyBinding newBinding,
        out string error)
    {
        error = null;

        if (!IsAllowed(newBinding, out error))
            return false;

        foreach (KeyValuePair<InputAction, KeyBinding> pair in _bindings)
        {
            if (pair.Key == action)
                continue;

            if (pair.Value.MainKey == newBinding.MainKey &&
                pair.Value.Modifier == newBinding.Modifier)
            {
                error = "Эта комбинация уже используется";
                return false;
            }
        }

        _bindings[action] = newBinding;
        return true;
    }

    public bool TryRebind(
    InputAction action,
    KeyBinding newBinding)
    {
        foreach (KeyValuePair<InputAction, KeyBinding> pair in _bindings)
        {
            if (pair.Key == action)
                continue;

            if (pair.Value.MainKey == newBinding.MainKey &&
                pair.Value.Modifier == newBinding.Modifier)
            {
                return false;
            }
        }

        _bindings[action] = newBinding;
        return true;
    }

    public bool IsAllowed(KeyBinding binding, out string error)
    {
        error = null;

        if (IsForbiddenKey(binding.MainKey))
        {
            error = "Недопустимая клавиша";
            return false;
        }

        if (binding.Modifier != KeyCode.None &&
            binding.Modifier != KeyCode.LeftShift &&
            binding.Modifier != KeyCode.RightShift)
        {
            error = "Разрешён только Shift как модификатор";
            return false;
        }

        return true;
    }

    private bool IsForbiddenKey(KeyCode key)
    {
        return
            key == KeyCode.None ||
            key == KeyCode.Escape ||
            key == KeyCode.LeftAlt || key == KeyCode.RightAlt ||
            key == KeyCode.LeftControl || key == KeyCode.RightControl ||
            key == KeyCode.LeftWindows || key == KeyCode.RightWindows ||
            key >= KeyCode.F1 && key <= KeyCode.F15;
    }
}
