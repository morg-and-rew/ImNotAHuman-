using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PackageRegistry : MonoBehaviour
{
    public static PackageRegistry Instance { get; private set; }

    private readonly List<PackageItem> _packages = new();
    private readonly HashSet<PackageItem> _takenFromWarehouse = new();
    private readonly HashSet<int> _takenNumbers = new();
    private readonly Dictionary<string, PackageSaveEntry> _removedTakenEntries = new();
    [SerializeField] private int _minNumber = 100;
    [SerializeField] private int _maxNumber = 999;
    [Tooltip("Сюжетные номера — есть на сцене с начала, но клиенты их не называют. Только по сюжету.")]
    [SerializeField] private int[] _storyReservedNumbers = { 8335, 5577, 5574 };
    private bool _restoredFromSave;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(GenerateAfterPackagesRegistered());
    }

    private IEnumerator GenerateAfterPackagesRegistered()
    {
        yield return null;
        if (_restoredFromSave)
            yield break;
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
        if (item != null)
        {
            bool wasTaken = _takenFromWarehouse.Contains(item) || (item.Number > 0 && _takenNumbers.Contains(item.Number));
            if (wasTaken && !string.IsNullOrEmpty(item.SaveId))
            {
                _removedTakenEntries[item.SaveId] = new PackageSaveEntry
                {
                    Id = item.SaveId,
                    Number = item.Number,
                    Position = item.transform.position,
                    Rotation = item.transform.rotation,
                    Active = false,
                    Taken = true
                };
            }
        }
        _packages.Remove(item);
        // Не удаляем из "взятых": если объект уничтожен, этот факт должен сохраниться.
        Destroy(item);
    }

    /// <summary>Вызвать, когда посылку взяли со склада (больше не предлагать в случайных заказах).</summary>
    public void NotifyPackageTaken(PackageItem item)
    {
        if (item != null)
        {
            _takenFromWarehouse.Add(item);
            if (item.Number > 0)
                _takenNumbers.Add(item.Number);
        }
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

    /// <summary>Номера для случайных заказов — только посылки, которые сейчас на складе, без сюжетных.</summary>
    public List<int> GetNumbersForRandomDelivery()
    {
        var reserved = _storyReservedNumbers != null && _storyReservedNumbers.Length > 0
            ? new HashSet<int>(_storyReservedNumbers)
            : null;
        var result = new List<int>();
        for (int i = 0; i < _packages.Count; i++)
        {
            PackageItem p = _packages[i];
            if (p == null || p.Number <= 0) continue;
            if (_takenFromWarehouse.Contains(p)) continue;
            if (_takenNumbers.Contains(p.Number)) continue;
            if (reserved != null && reserved.Contains(p.Number)) continue;
            result.Add(p.Number);
        }
        return result;
    }

    public int DebugCount => _packages.Count;

    public List<PackageSaveEntry> CaptureSaveEntries()
    {
        var list = new List<PackageSaveEntry>(_packages.Count + _removedTakenEntries.Count);
        for (int i = 0; i < _packages.Count; i++)
        {
            PackageItem p = _packages[i];
            if (p == null) continue;
            list.Add(new PackageSaveEntry
            {
                Id = p.SaveId,
                Number = p.Number,
                Position = p.transform.position,
                Rotation = p.transform.rotation,
                Active = p.gameObject.activeSelf,
                Taken = _takenFromWarehouse.Contains(p) || (p.Number > 0 && _takenNumbers.Contains(p.Number))
            });
        }

        foreach (var kv in _removedTakenEntries)
        {
            if (kv.Value == null) continue;
            list.Add(new PackageSaveEntry
            {
                Id = kv.Value.Id,
                Number = kv.Value.Number,
                Position = kv.Value.Position,
                Rotation = kv.Value.Rotation,
                Active = kv.Value.Active,
                Taken = kv.Value.Taken
            });
        }
        return list;
    }

    public void RestoreFromSaveEntries(List<PackageSaveEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        var byId = new Dictionary<string, PackageSaveEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            PackageSaveEntry e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.Id)) continue;
            byId[e.Id] = e;
        }

        _takenFromWarehouse.Clear();
        _takenNumbers.Clear();

        for (int i = 0; i < _packages.Count; i++)
        {
            PackageItem p = _packages[i];
            if (p == null) continue;
            if (!byId.TryGetValue(p.SaveId, out PackageSaveEntry entry))
                continue;

            p.SetNumber(entry.Number);
            p.transform.position = entry.Position;
            p.transform.rotation = entry.Rotation;
            p.gameObject.SetActive(entry.Active);

            if (entry.Taken)
            {
                _takenFromWarehouse.Add(p);
                if (entry.Number > 0)
                    _takenNumbers.Add(entry.Number);
            }
        }

        // Защита на случай, если часть взятых уже уничтожена и их нет среди _packages.
        foreach (PackageSaveEntry e in entries.Where(x => x != null && x.Taken && x.Number > 0))
            _takenNumbers.Add(e.Number);

        _restoredFromSave = true;
    }
}
