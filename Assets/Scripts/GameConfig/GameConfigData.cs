using System;

[Serializable]
public class GameConfigData
{
    public StoryConfig story = new StoryConfig();
    public RadioConfig radio = new RadioConfig();
}

[Serializable]
public class StoryConfig
{
    public string startTrigger = "auto";
    public float startDelaySeconds = 2f;
    public IntroConfig intro = new IntroConfig();
    public TutorialConfig tutorial = new TutorialConfig();
    public StoryStepData[] steps = new StoryStepData[0];
}

[Serializable]
public class IntroConfig
{
    public string quoteKey = "intro.quote.marc_aurelius";
    public float fadeDuration = 10f;
    public string monologueConversation = "Player_IntroMonologue";
}

[Serializable]
public class TutorialConfig
{
    public string pressSpaceKey = "tutorial.press_space";
    public string doorWarehouseKey = "tutorial.door_warehouse";
    public string returnPressFKey = "tutorial.return_press_f";
    public string routerHintKey = "tutorial.router_hint";
    public string phoneHintKey = "tutorial.phone_hint";
    public string phoneCallProviderKey = "tutorial.phone_call_provider";
    public string phonePutKey = "tutorial.phone_put";
    public string radioUseKey = "tutorial.radio_use";
    public string radioBeforeClientKey = "tutorial.radio_before_client";
    public string meetClientKey = "tutorial.meet_client";
    public string goWarehouseKey = "tutorial.go_warehouse";
    public string returnToClientKey = "tutorial.return_to_client";
    public string warehousePickKey = "tutorial.warehouse_pick";
    public string warehouseReturnKey = "tutorial.warehouse_return";
    public string windowLookKey = "tutorial.window_look";
    public string emptyKey = "tutorial.empty";
    public string routerConversation = "Hero_AfterRouterReboot";
    public string providerNumber = "123456";
    public string providerBeepsConversation = "Phone_CallProvider_Beeps";
    public string providerAfterConversation = "Hero_AfterProviderCall";
}

[Serializable]
public class RadioConfig
{
    public string staticClipPath = "Radio/Static";
    public RadioEventData[] events = new RadioEventData[0];
}

[Serializable]
public class RadioEventData
{
    public string eventId;
    public string conversationTitle;
    public int priority;
    public string audioPath;
    public string playerReplicaConversation;
    public bool requireExitZoneBeforeVideo;
    public string exitZoneId;
    public string postVideoConversation;
}

[Serializable]
public class StoryStepData
{
    public string stepId;
    public string stepType;
    public string conversationTitle;
    public string hintText;
    public string triggerId;
    public bool optional;
    public bool autoTravel;
    public bool removePackageAfterDialogue;
    public bool showDeliveryNote;
    public int deliveryNoteNumber;
    public string deliveryNoteLuaCondition;
    public string skipIfLuaConditionFalse;
    public bool hideDeliveryNote;
    public bool expireRadioOnEnter;
    public string[] activateRadioEventIds;
    public bool showRadioHintOnEnter;
    public string computerVideoKind;
    public float fadeToBlackDuration;
}
