using TMPro;
using UnityEngine;

public sealed class PackageItem : MonoBehaviour
{
    [SerializeField] private TMP_Text _numberText;
    [SerializeField] private PackageRegistry _registry;

    public int Number { get; private set; }

    private void Awake()
    {
        _registry.Register(this);
    }

    private void OnDestroy()
    {
        _registry.Unregister(this);
    }

    public void SetNumber(int number)
    {
        Number = number;

        if (_numberText != null)
            _numberText.text = number.ToString();
    }
}
