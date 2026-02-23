using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class RouterInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private GameFlowController _flow;
    [SerializeField] private Sprite _hintSprite;

    private bool _used;

    public Sprite HintSprite => _hintSprite;

    public void Interact(IPlayerInput input)
    {
        if (_used) return;
        if (!_flow.IsStoryExpectingTrigger("router"))
            return;
        _used = true;

        DialogueManager.instance.conversationStarted += OnConversationStarted;
        DialogueManager.instance.conversationEnded += OnConversationEnded;
        string conv = GameConfig.Tutorial.routerConversation;
        DialogueManager.StartConversation(string.IsNullOrEmpty(conv) ? "Hero_AfterRouterReboot" : conv);
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
