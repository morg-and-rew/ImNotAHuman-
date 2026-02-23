using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class RouterInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private GameFlowController _flow;
    [SerializeField] private Canvas _hintCanvas;

    private bool _used;

    public Canvas hint => _hintCanvas;

    private void Awake()
    {
        LookAtCamera.Ensure(_hintCanvas != null ? _hintCanvas.gameObject : null);
    }

    public void Interact(IPlayerInput input)
    {
        if (_used) return;
        if (!_flow.IsStoryExpectingTrigger("router"))
            return;
        _used = true;

        if (_hintCanvas != null && _hintCanvas.gameObject != null)
            _hintCanvas.gameObject.SetActive(false);

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
