using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class InputRebindMenu : MonoBehaviour
{
    [System.Serializable]
    private class BindingField
    {
        public InputAction action;
        public TMP_InputField input;
    }

    [SerializeField] private List<BindingField> _fields;
    [SerializeField] private TMP_Text _errorText;

    private PlayerKeyBindings _bindings;

    public void Initialize(PlayerKeyBindings bindings)
    {
        _bindings = bindings;

        foreach (BindingField field in _fields)
        {
            KeyBinding current = _bindings.Get(field.action);
            if (current.Modifier != KeyCode.None)
                field.input.text = $"{current.MainKey}+SHIFT";
            else
                field.input.text = current.MainKey.ToString();
        }

        _errorText.gameObject.SetActive(false);
    }

    public void Apply()
    {
        _errorText.gameObject.SetActive(false);

        Dictionary<InputAction, KeyBinding> parsed = new();

        foreach (BindingField field in _fields)
        {
            string inputText = field.input.text.Trim();

            if (!KeyBindingParser.TryParse(inputText, out KeyBinding binding, out string parseError))
            {
                ShowError($"?????? ? {field.action}: {parseError}");
                return; 
            }

            parsed[field.action] = binding;
        }

        if (HasDuplicates(parsed))
        {
            ShowError("???? ? ?? ?? ?????????? ??? ????????? ?? ????????? ????????");
            return;
        }

        foreach (KeyValuePair<InputAction, KeyBinding> pair in parsed)
        {
            if (!_bindings.TryRebind(pair.Key, pair.Value, out string error))
            {
                ShowError($"?????? ??? ?????????? {pair.Key}: {error}");
                continue;
            }
        }
    }

    private bool HasDuplicates(Dictionary<InputAction, KeyBinding> data)
    {
        HashSet<string> used = new();

        foreach (KeyBinding binding in data.Values)
        {
            string keyString = binding.Modifier != KeyCode.None
                ? $"{binding.MainKey}+SHIFT"
                : binding.MainKey.ToString();

            if (!used.Add(keyString))
                return true;
        }

        return false;
    }

    private void ShowError(string message)
    {
        _errorText.text = message;
        _errorText.gameObject.SetActive(true);
    }
}
