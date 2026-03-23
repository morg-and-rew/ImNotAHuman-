using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class Day1SaveData
{
    public bool ChoseToGivePackage5577;
    public bool GotPhoneNumberFromGuy;
    public string SavedPhoneNumber;
    public int NeutralChoicesCount;
    public int MysticalChoicesCount;
    public int SkepticalChoicesCount;
}

[Serializable]
public sealed class GameSaveData
{
    public Day1SaveData Day1;
}

/// <summary>
/// JSON-сохранение прогресса. Файл всегда пишется при окончании дня.
/// Режим старта: с 1-го дня или сразу со 2-го (с выбранным слотом дня 1). Слоты позволяют тестировать день 2 с разными исходами дня 1 без перепрохождения.
/// </summary>
public sealed class GameSaveSystem : MonoBehaviour
{
    private static GameSaveSystem _instance;
    private static bool? _loadFromSaveAtStartOverride;

    [Header("Save Settings")]
    [Tooltip("Имя основного файла сохранения в Application.persistentDataPath. Слоты 1–3 пишутся как save_slot1.json, save_slot2.json, save_slot3.json.")]
    [SerializeField] private string _fileName = "save.json";

    [Header("Start Mode (для тестирования)")]
    [Tooltip("Старт с 1-го дня (интро + все диалоги) или сразу со 2-го дня с загруженным состоянием дня 1.")]
    [SerializeField] private bool _loadFromSaveAtStart = false;

    [Tooltip("С какого слота загружать при старте: 0 = основной файл (save.json), 1–3 = слот 1–3. Имеет смысл только при включённом Load From Save At Start.")]
    [SerializeField] [Range(0, 3)] private int _loadSlot = 0;

    [Header("При сохранении в конце дня 1")]
    [Tooltip("Дополнительно записать текущий исход дня 1 в этот слот (0 = не записывать). Удобно: прошёл день 1 «мистический» → выбери 1, прошёл «скептический» → выбери 2, потом при старте выбирай слот и смотри день 2.")]
    [SerializeField] [Range(0, 3)] private int _alsoSaveToSlot = 0;

    /// <summary>При старте игры загружать сохранение и продолжать со 2-го дня (галочка в инспекторе).</summary>
    public static bool LoadFromSaveAtStart =>
        _loadFromSaveAtStartOverride ?? (_instance != null && _instance._loadFromSaveAtStart);

    public static bool HasLoadFromSaveAtStartOverride => _loadFromSaveAtStartOverride.HasValue;

    public static void SetLoadFromSaveAtStartOverride(bool value) => _loadFromSaveAtStartOverride = value;

    public static void ClearLoadFromSaveAtStartOverride() => _loadFromSaveAtStartOverride = null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static string GetSavePath(int slot = 0)
    {
        string fileName;
        if (slot >= 1 && slot <= 3)
            fileName = $"save_slot{slot}.json";
        else
            fileName = (_instance != null && !string.IsNullOrWhiteSpace(_instance._fileName))
                ? _instance._fileName
                : "save.json";

        return Path.Combine(Application.persistentDataPath, fileName);
    }

    /// <summary>Сохранить данные первого дня. Всегда пишется в основной файл; при _alsoSaveToSlot > 0 — также в выбранный слот.</summary>
    public static void SaveDay1(Day1SaveData day1)
    {
        if (day1 == null)
            return;

        var data = new GameSaveData { Day1 = day1 };

        try
        {
            string json = JsonUtility.ToJson(data, true);
            string mainPath = GetSavePath(0);
            File.WriteAllText(mainPath, json);

            int alsoSlot = _instance != null ? _instance._alsoSaveToSlot : 0;
            if (alsoSlot >= 1 && alsoSlot <= 3)
            {
                string slotPath = GetSavePath(alsoSlot);
                File.WriteAllText(slotPath, json);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game data: {e}");
        }
    }

    /// <summary>Загрузить данные первого дня из настроенного слота (при старте). Если файла нет или он повреждён, вернёт null.</summary>
    public static Day1SaveData LoadDay1()
    {
        int slot = (_instance != null) ? _instance._loadSlot : 0;
        return LoadDay1FromSlot(slot);
    }

    /// <summary>Загрузить данные первого дня из указанного слота (0 = основной файл, 1–3 = слоты).</summary>
    public static Day1SaveData LoadDay1FromSlot(int slot)
    {
        string path = GetSavePath(slot);
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            return data?.Day1;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load game data from '{path}': {e}");
            return null;
        }
    }
}

