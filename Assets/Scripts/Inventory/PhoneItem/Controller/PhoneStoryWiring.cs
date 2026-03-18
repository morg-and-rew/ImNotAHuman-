using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class PhoneStoryWiring
{
    private readonly PhoneCallService _callService;
    private readonly GameFlowController _flow;
    private readonly GameSoundController _gameSoundController;

    private const string SkepticNumber = "333111333";
    private const string SkepticCallConversation = "Phone_CallSkeptic_Number";
    private const string LuaUnlockFunc = "UnlockSkepticPhone";

    private enum CallMode { None, ProviderBeeps, ProviderAfter, SkepticCall }
    private CallMode _mode = CallMode.None;

    private bool _skepticUnlocked;

    public PhoneStoryWiring(PhoneCallService callService, GameFlowController flow, GameSoundController gameSoundController = null)
    {
        _callService = callService;
        _flow = flow;
        _gameSoundController = gameSoundController;

        string providerNum = GameConfig.Tutorial.providerNumber;
        _callService.Register(string.IsNullOrEmpty(providerNum) ? "156190" : providerNum, OnCallProvider);

        Lua.RegisterFunction(LuaUnlockFunc, this,
            SymbolExtensions.GetMethodInfo(() => UnlockSkepticPhone()));
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
        _flow.ShowSkepticPhoneNote();
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
    }
}
