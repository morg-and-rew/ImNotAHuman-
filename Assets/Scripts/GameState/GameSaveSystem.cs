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
/// Один флаг в инспекторе: false = игра стартует с нуля, true = при старте загружается последнее сохранение (чтобы смотреть ветки дня 2+).
/// </summary>
public sealed class GameSaveSystem : MonoBehaviour
{
    private static GameSaveSystem _instance;

    [Header("Save Settings")]
    [Tooltip("False = каждый запуск с нуля (прогресс всё равно сохраняется в JSON в конце дня). True = при старте загрузить последнее сохранение и продолжить с него (для просмотра разных веток дня 2 и т.д.).")]
    [SerializeField] private bool _loadFromSaveAtStart = false;

    [Tooltip("Имя файла сохранения в папке Application.persistentDataPath.")]
    [SerializeField] private string _fileName = "save.json";

    /// <summary>При старте игры загружать сохранение и продолжать с него (галочка в инспекторе).</summary>
    public static bool LoadFromSaveAtStart => _instance != null && _instance._loadFromSaveAtStart;

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

    private static string GetSavePath()
    {
        string fileName = (_instance != null && !string.IsNullOrWhiteSpace(_instance._fileName))
            ? _instance._fileName
            : "save.json";

        return Path.Combine(Application.persistentDataPath, fileName);
    }

    /// <summary>Сохранить данные первого дня. JSON всегда пишется на диск (флаг в инспекторе влияет только на загрузку при старте).</summary>
    public static void SaveDay1(Day1SaveData day1)
    {
        if (day1 == null)
            return;

        string path = GetSavePath();
        GameSaveData data = new GameSaveData
        {
            Day1 = day1
        };

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game data to '{path}': {e}");
        }
    }

    /// <summary>Загрузить данные первого дня. Если файла нет или он повреждён, вернёт null.</summary>
    public static Day1SaveData LoadDay1()
    {
        string path = GetSavePath();
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

