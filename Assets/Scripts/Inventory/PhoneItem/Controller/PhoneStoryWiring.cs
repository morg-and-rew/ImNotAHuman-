using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class PhoneStoryWiring
{
    private readonly PhoneCallService _callService;
    private readonly GameFlowController _flow;
    private readonly GameSoundController _gameSoundController;

    private const string SkepticNumber = "333111333";
    private const string SkepticCallConversation = "Phone_CallSkeptic_Number";
    private const string EmergencyNumber = "112";
    /// <summary> Сюжетный звонок в скорую (день 2, ветка «позвонить в скорую»). </summary>
    private const string EmergencyCallStoryConversation = "Phone_CallEmergency_911";
    /// <summary> Обычный звонок на 112 вне сюжетного бита. </summary>
    private const string EmergencyCallCasualConversation = "Phone_Call112_Casual";
    private const string LuaUnlockFunc = "UnlockSkepticPhone";
    private const string SkepticPhoneUnlockedLuaVar = "SkepticPhoneUnlocked";
    private const string UnlockPhoneFlagVar = "unlock_phone";
    private const string UnlockPhoneNumberVar = "unlock_phone_number";

    private enum CallMode { None, ProviderBeeps, ProviderAfter, SkepticCall, EmergencyCall }
    private CallMode _mode = CallMode.None;

    private bool _skepticUnlocked;

    public PhoneStoryWiring(PhoneCallService callService, GameFlowController flow, GameSoundController gameSoundController = null)
    {
        _callService = callService;
        _flow = flow;
        _gameSoundController = gameSoundController;

        string providerNum = GameConfig.Tutorial.providerNumber;
        _callService.Register(string.IsNullOrEmpty(providerNum) ? "156190" : providerNum, OnCallProvider);
        _callService.Register(EmergencyNumber, OnCallEmergency);

        Lua.RegisterFunction(LuaUnlockFunc, this,
            SymbolExtensions.GetMethodInfo(() => UnlockSkepticPhone()));

        // При Continue флаг может быть восстановлен раньше, чем зарегистрирована Lua-функция UnlockSkepticPhone.
        // Дублируем восстановление здесь: после инициализации wiring гарантированно активируем записку/номер.
        if (DialogueLua.GetVariable(SkepticPhoneUnlockedLuaVar).AsBool)
            UnlockSkepticPhone();
    }

    public void Dispose()
    {
        Lua.UnregisterFunction(LuaUnlockFunc);
        if (DialogueManager.instance != null)
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
    }

    public void UnlockSkepticPhone()
    {
        if (_skepticUnlocked) return;
        _skepticUnlocked = true;

        _callService.Register(SkepticNumber, OnCallSkeptic);
        DialogueLua.SetVariable(SkepticPhoneUnlockedLuaVar, true);
        _flow.ShowSkepticPhoneNote();

        // Fallback для сцен/билдов, где объект заметки в GameFlowController не назначен:
        // пробрасываем через общий PhoneUnlockDirector (он спавнит префаб записки у spawn point).
        DialogueLua.SetVariable(UnlockPhoneFlagVar, 1);
        DialogueLua.SetVariable(UnlockPhoneNumberVar, SkepticNumber);
        PhoneUnlockDirector unlockDirector = Object.FindFirstObjectByType<PhoneUnlockDirector>();
        unlockDirector?.TryUnlockFromDialogue();
    }

    private void OnCallProvider()
    {
        _flow.HidePhoneHint();

        _mode = CallMode.ProviderBeeps;
        _gameSoundController?.PlayPhoneBeeps();

        DialogueManager.instance.conversationEnded -= OnConversationEnded;
        DialogueManager.instance.conversationEnded += OnConversationEnded;

        string beeps = GameConfig.Tutorial.providerBeepsConversation;
        DialogueManager.StartConversation(string.IsNullOrEmpty(beeps) ? "Phone_CallProvider_Beeps" : beeps);
    }

    private void OnCallSkeptic()
    {
        if (!_skepticUnlocked) return;

        _mode = CallMode.SkepticCall;

        DialogueManager.instance.conversationEnded -= OnConversationEnded;
        DialogueManager.instance.conversationEnded += OnConversationEnded;

        DialogueManager.StartConversation(SkepticCallConversation);
    }

    private void OnCallEmergency()
    {
        _mode = CallMode.EmergencyCall;
        DialogueManager.instance.conversationEnded -= OnConversationEnded;
        DialogueManager.instance.conversationEnded += OnConversationEnded;
        bool story = _flow != null && _flow.IsAwaitingStoryEmergency112Call;
        string convo = story ? EmergencyCallStoryConversation : EmergencyCallCasualConversation;
        if (!story)
            _gameSoundController?.PlayPhoneBeeps();
        DialogueManager.StartConversation(convo);
    }

    private void OnConversationEnded(Transform actor)
    {
        if (_mode == CallMode.ProviderBeeps)
        {
            _gameSoundController?.StopPhoneSounds();
            _mode = CallMode.ProviderAfter;
            string after = GameConfig.Tutorial.providerAfterConversation;
            DialogueManager.StartConversation(string.IsNullOrEmpty(after) ? "Hero_AfterProviderCall" : after);
            return;
        }

        if (_mode == CallMode.ProviderAfter)
        {
            _gameSoundController?.PlayPhoneCallEnd();
            _mode = CallMode.None;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;

            _flow.MarkProviderCallDone();
            // Сюжетный шаг go_to_phone ждёт trigger provider_call; раньше он срабатывал только при опускании телефона —
            // при «залипшем» диалоге дроп был невозможен. Продвигаем сразу после звонка; повтор при дропе безопасен.
            _flow.NotifyTrigger("provider_call");
            _flow.ShowPhonePutHintOnce();
            return;
        }

        if (_mode == CallMode.SkepticCall)
        {
            _gameSoundController?.PlayPhoneCallEnd();
            _mode = CallMode.None;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
            return;
        }
        if (_mode == CallMode.EmergencyCall)
        {
            _gameSoundController?.StopPhoneSounds();
            _gameSoundController?.PlayPhoneCallEnd();
            _mode = CallMode.None;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
            return;
        }
    }
}
