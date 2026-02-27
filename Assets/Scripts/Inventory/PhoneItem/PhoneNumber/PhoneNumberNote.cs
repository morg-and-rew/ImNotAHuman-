using TMPro;
using UnityEngine;

public sealed class PhoneNumberNote : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    public void SetNumber(string number)
    {
        if (_text != null) _text.text = number;
    }

    public string GetNumber() => _text != null ? _text.text ?? "" : "";
}
