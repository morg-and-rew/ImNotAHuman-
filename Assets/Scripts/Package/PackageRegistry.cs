using System.Collections.Generic;
using UnityEngine;

public sealed class PackageRegistry : MonoBehaviour
{
    private readonly List<PackageItem> _packages = new();
    [SerializeField] private int _minNumber = 100;
    [SerializeField] private int _maxNumber = 999;
    [Tooltip("Сюжетные номера — есть на сцене с начала, но клиенты их не называют. Только по сюжету.")]
    [SerializeField] private int[] _storyReservedNumbers = { 8335, 5577 };

    private void Start()
    {
        Generate();
    }

    private void Generate()
    {
        HashSet<int> usedNumbers = new();

        int storyCount = _storyReservedNumbers != null ? _storyReservedNumbers.Length : 0;
        for (int i = 0; i < storyCount && i < _packages.Count; i++)
        {
            int num = _storyReservedNumbers[i];
            if (num > 0 && !usedNumbers.Contains(num))
            {
                usedNumbers.Add(num);
                _packages[i].SetNumber(num);
            }
        }

        for (int i = storyCount; i < _packages.Count; i++)
        {
            PackageItem package = _packages[i];
            int number;
            do
            {
                number = Random.Range(_minNumber, _maxNumber + 1);
            }
            while (usedNumbers.Contains(number));

            usedNumbers.Add(number);
            package.SetNumber(number);
        }
    }

    /// <summary>Номера, которые клиенты не называют при случайных заказах. Только по сюжету.</summary>
    public IReadOnlyList<int> StoryReservedNumbers => _storyReservedNumbers ?? System.Array.Empty<int>();

    public void Register(PackageItem item)
    {
        if (item == null) return;
        if (!_packages.Contains(item))
            _packages.Add(item);
    }

    public void Unregister(PackageItem item)
    {
        _packages.Remove(item);
        Destroy(item);
    }

    public List<int> GetAllNumbers()
    {
        List<int> list = new List<int>(_packages.Count);
        for (int i = 0; i < _packages.Count; i++)
        {
            int n = _packages[i] != null ? _packages[i].Number : 0;
            if (n != 0) list.Add(n);
        }
        return list;
    }

    /// <summary>Номера для случайных заказов — без сюжетных (8335, 5577).</summary>
    public List<int> GetNumbersForRandomDelivery()
    {
        var all = GetAllNumbers();
        if (_storyReservedNumbers == null || _storyReservedNumbers.Length == 0)
            return all;
        var reserved = new HashSet<int>(_storyReservedNumbers);
        var result = new List<int>(all.Count);
        foreach (int n in all)
        {
            if (n > 0 && !reserved.Contains(n))
                result.Add(n);
        }
        return result;
    }

    public int DebugCount => _packages.Count;
}
