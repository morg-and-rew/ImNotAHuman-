using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class RouterInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private GameFlowController _flow;
    [SerializeField] private Sprite _hintSprite;

    private bool _used;

    public Sprite HintSprite => CanInteractNow() ? _hintSprite : null;

    public void Interact(IPlayerInput input)
    {
        if (!CanInteractNow())
            return;

        _used = true;

        DialogueManager.instance.conversationStarted += OnConversationStarted;
        DialogueManager.instance.conversationEnded += OnConversationEnded;
        string conv = GameConfig.Tutorial.routerConversation;
        DialogueManager.StartConversation(string.IsNullOrEmpty(conv) ? "Hero_AfterRouterReboot" : conv);
    }

    private bool CanInteractNow()
    {
        if (_used) return false;
        return _flow != null && _flow.IsStoryExpectingTrigger("router");
    }

    private void OnConversationStarted(Transform actor)
    {
        DialogueManager.instance.conversationStarted -= OnConversationStarted;
        _flow.HideHint();
    }

    private void OnConversationEnded(Transform actor)
    {
        DialogueManager.instance.conversationStarted -= OnConversationStarted;
        DialogueManager.instance.conversationEnded -= OnConversationEnded;

        GameStateService.UnlockPhone();
        _flow.NotifyTrigger("router");
    }
}
