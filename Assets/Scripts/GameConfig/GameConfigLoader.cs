using System.Collections.Generic;
using UnityEngine;

public static class GameConfig
{
    private const string Path = "GameConfig";
    private static GameConfigData _data;

    public static GameConfigData Data
    {
        get
        {
            if (_data == null)
                Load();
            return _data;
        }
    }

    public static void Load()
    {
        TextAsset asset = Resources.Load<TextAsset>(Path);
        if (asset == null)
        {
            Debug.LogError("[Config] GameConfig.json not found in Resources.");
            _data = new GameConfigData();
            return;
        }

        _data = JsonUtility.FromJson<GameConfigData>(asset.text);
        if (_data == null)
        {
            Debug.LogError("[Config] Failed to parse GameConfig.json");
            _data = new GameConfigData();
            return;
        }

        if (_data.story == null) _data.story = new StoryConfig();
        if (_data.radio == null) _data.radio = new RadioConfig();
        if (_data.story.intro == null) _data.story.intro = new IntroConfig();
        if (_data.story.tutorial == null) _data.story.tutorial = new TutorialConfig();
        if (_data.story.steps == null) _data.story.steps = new StoryStepData[0];
        if (_data.radio.events == null) _data.radio.events = new RadioEventData[0];

        Debug.Log($"[Config] Loaded. Story: {_data.story.steps.Length} steps, start={_data.story.startTrigger}. Radio: {_data.radio.events.Length} events.");
    }

    public static IntroConfig Intro => Data.story.intro ?? new IntroConfig();
    public static TutorialConfig Tutorial => Data.story.tutorial ?? new TutorialConfig();
    public static IReadOnlyList<StoryStepData> StorySteps => Data.story.steps ?? System.Array.Empty<StoryStepData>();
    public static IReadOnlyList<RadioEventData> RadioEvents => Data.radio.events ?? System.Array.Empty<RadioEventData>();
    public static string RadioStaticPath => Data.radio.staticClipPath ?? "";
    public static bool StoryAutoStart => string.Equals(Data.story.startTrigger, "auto", System.StringComparison.OrdinalIgnoreCase);
    public static bool StoryStartOnClientInteract => string.Equals(Data.story.startTrigger, "client_interact", System.StringComparison.OrdinalIgnoreCase);
    public static float StoryStartDelay => Data.story.startDelaySeconds;
}
