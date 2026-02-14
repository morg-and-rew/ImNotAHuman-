using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class RouterInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private GameFlowController _flow;
    [SerializeField] private Canvas _hintCanvas;

    private bool _used;

    public Canvas hint => _hintCanvas;

    public void Interact(IPlayerInput input)
    {
        if (_used) return;
        _used = true;

        _flow.HideHint();

        if (_hintCanvas != null && _hintCanvas.gameObject != null)
            Destroy(_hintCanvas.gameObject);

        DialogueManager.instance.conversationEnded += OnConversationEnded;
        string conv = GameConfig.Tutorial.routerConversation;
        DialogueManager.StartConversation(string.IsNullOrEmpty(conv) ? "Hero_AfterRouterReboot" : conv);
    }

    private void OnConversationEnded(Transform actor)
    {
        DialogueManager.instance.conversationEnded -= OnConversationEnded;

        GameStateService.UnlockPhone();
        GameStateService.SetState(GameState.Phone);

        _flow.NotifyTrigger("router");
    }
}
