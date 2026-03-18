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
            _data = new GameConfigData();
            return;
        }

        _data = JsonUtility.FromJson<GameConfigData>(asset.text);
        if (_data == null)
        {
            _data = new GameConfigData();
            return;
        }

        if (_data.story == null) _data.story = new StoryConfig();
        if (_data.radio == null) _data.radio = new RadioConfig();
        if (_data.story.intro == null) _data.story.intro = new IntroConfig();
        if (_data.story.tutorial == null) _data.story.tutorial = new TutorialConfig();
        ApplyTutorialDefaults(_data.story.tutorial);
        if (_data.story.steps == null) _data.story.steps = new StoryStepData[0];
        if (_data.radio.events == null) _data.radio.events = new RadioEventData[0];
    }

    private static void ApplyTutorialDefaults(TutorialConfig t)
    {
        if (t == null) return;
        var d = new TutorialConfig();
        if (string.IsNullOrEmpty(t.pressSpaceKey)) t.pressSpaceKey = d.pressSpaceKey;
        if (string.IsNullOrEmpty(t.doorWarehouseKey)) t.doorWarehouseKey = d.doorWarehouseKey;
        if (string.IsNullOrEmpty(t.pressFToWarehouseAfterDialogueKey)) t.pressFToWarehouseAfterDialogueKey = d.pressFToWarehouseAfterDialogueKey;
        if (string.IsNullOrEmpty(t.returnPressFKey)) t.returnPressFKey = d.returnPressFKey;
        if (string.IsNullOrEmpty(t.routerHintKey)) t.routerHintKey = d.routerHintKey;
        if (string.IsNullOrEmpty(t.phoneHintKey)) t.phoneHintKey = d.phoneHintKey;
        if (string.IsNullOrEmpty(t.phoneCallProviderKey)) t.phoneCallProviderKey = d.phoneCallProviderKey;
        if (string.IsNullOrEmpty(t.phonePutKey)) t.phonePutKey = d.phonePutKey;
        if (string.IsNullOrEmpty(t.radioUseKey)) t.radioUseKey = d.radioUseKey;
        if (string.IsNullOrEmpty(t.radioBeforeClientKey)) t.radioBeforeClientKey = d.radioBeforeClientKey;
        if (string.IsNullOrEmpty(t.meetClientKey)) t.meetClientKey = d.meetClientKey;
        if (string.IsNullOrEmpty(t.goWarehouseKey)) t.goWarehouseKey = d.goWarehouseKey;
        if (string.IsNullOrEmpty(t.returnToClientKey)) t.returnToClientKey = d.returnToClientKey;
        if (string.IsNullOrEmpty(t.warehousePickKey)) t.warehousePickKey = d.warehousePickKey;
        if (string.IsNullOrEmpty(t.warehouseReturnKey)) t.warehouseReturnKey = d.warehouseReturnKey;
        if (string.IsNullOrEmpty(t.windowLookKey)) t.windowLookKey = d.windowLookKey;
        if (string.IsNullOrEmpty(t.watchVideoKey)) t.watchVideoKey = d.watchVideoKey;
        if (string.IsNullOrEmpty(t.emptyKey)) t.emptyKey = d.emptyKey;
        if (string.IsNullOrEmpty(t.routerConversation)) t.routerConversation = d.routerConversation;
        if (string.IsNullOrEmpty(t.providerNumber)) t.providerNumber = d.providerNumber;
        if (string.IsNullOrEmpty(t.providerBeepsConversation)) t.providerBeepsConversation = d.providerBeepsConversation;
        if (string.IsNullOrEmpty(t.providerAfterConversation)) t.providerAfterConversation = d.providerAfterConversation;
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
