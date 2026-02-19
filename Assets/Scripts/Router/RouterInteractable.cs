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
        if (!_flow.IsStoryExpectingTrigger("router"))
        {
            Debug.Log("[Tutorial] Игрок нажал E у роутера, но текущий шаг не go_to_router — взаимодействие игнорируется");
            return;
        }
        _used = true;

        // Не Destroy — иначе может уничтожиться общий канвас с TutorialHintView. Только скрываем подсказку роутера.
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
        Debug.Log("[Tutorial] Диалог у роутера запущен → tutorial.router_hint скрыт");
        _flow.HideHint();
    }

    private void OnConversationEnded(Transform actor)
    {
        DialogueManager.instance.conversationStarted -= OnConversationStarted;
        DialogueManager.instance.conversationEnded -= OnConversationEnded;

        GameStateService.UnlockPhone();
        GameStateService.SetState(GameState.Phone);

        _flow.NotifyTrigger("router");
    }
}
