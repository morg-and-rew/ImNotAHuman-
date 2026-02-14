using UnityEngine;
using UnityEngine.UI;

public sealed class PhoneDigitButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private char _digit = '0';

    public Button Button => _button;
    public char Digit => _digit;

    private void Reset()
    {
        _button = GetComponent<Button>();
    }
}
