using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PackageItem : MonoBehaviour
{
    [SerializeField] private Text _numberText;
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

    public void NotifyTakenFromWarehouse()
    {
        _registry?.NotifyPackageTaken(this);
    }

    public void SetNumber(int number)
    {
        Number = number;

        if (_numberText != null)
            _numberText.text = number.ToString();
    }
}
