using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class PlayerInteractionController
{
    private readonly PlayerView _playerView;
    private readonly IPlayerInput _input;
    private readonly PlayerHands _hands;
    private readonly ClientInteraction _client;
    private readonly InteractionRaycastCache _raycastCache;
    private readonly IGameFlowController _flow;

    private IWorldInteractable _currentWorldInteractable;
    private PackageHoldable _currentPackageHint;

    public PlayerInteractionController(
        PlayerView playerView,
        IPlayerInput input,
        PlayerHands hands,
        ClientInteraction clientInteraction,
        InteractionRaycastCache raycastCache,
        IGameFlowController flow = null)
    {
        _playerView = playerView;
        _input = input;
        _hands = hands;
        _client = clientInteraction;
        _raycastCache = raycastCache;
        _flow = flow;
    }

    public void Tick()
    {
        IHoldable holdable = _raycastCache.GetHoldable();
        IWorldInteractable worldInteractable = _raycastCache.GetWorldInteractable();

        UpdateWorldHint(worldInteractable);
        UpdatePackageHint(holdable);

        if (!_input.InteractPressed)
            return;

        // Во время любого диалога (радио, Client_Day1.5.1 и т.д.) блокируем E: окно, радио, телефон, посылка, клиент через луч.
        if (DialogueManager.isConversationActive)
            return;

        if (_hands.HasItem)
        {
            TryDropItem();
            return;
        }

        if (holdable != null && IsHoldableAllowed(holdable))
        {
            TryPickItem(holdable);
            return;
        }

        if (worldInteractable != null)
            worldInteractable.Interact(_input);
    }

    private void UpdateWorldHint(IWorldInteractable interactable)
    {
        if (_currentWorldInteractable == interactable)
            return;

        if (_currentWorldInteractable?.hint != null)
            _currentWorldInteractable.hint.gameObject.SetActive(false);

        _currentWorldInteractable = interactable;

        if (_currentWorldInteractable?.hint != null)
            _currentWorldInteractable.hint.gameObject.SetActive(true);
    }

    private void UpdatePackageHint(IHoldable holdable)
    {
        PackageHoldable packageHint = (holdable is PackageHoldable pkg && IsHoldableAllowed(pkg)) ? pkg : null;
        if (_currentPackageHint == packageHint)
            return;
        if (_currentPackageHint?.HintCanvas != null)
            _currentPackageHint.HintCanvas.SetActive(false);
        _currentPackageHint = packageHint;
        if (_currentPackageHint?.HintCanvas != null)
            _currentPackageHint.HintCanvas.SetActive(true);
    }

    private bool IsHoldableAllowed(IHoldable holdable)
    {
        if (holdable is IHandPointProvider hp && hp.HandPointType == HandPointType.Phone)
            return _flow != null && _flow.IsPhonePickupAllowed();
        if (holdable is PhoneItemView && !GameStateService.PhoneUnlocked)
            return false;
        // Посылки: только на складе и только когда по сюжету нужно принести посылку (не при каждом заходе на склад).
        if (holdable is PackageHoldable)
            return GameStateService.IsWarehouse && _flow != null && _flow.IsPackagePickAllowedByStory;
        return GameStateService.IsWarehouse;
    }

    private void TryPickItem(IHoldable holdable)
    {
        if (_hands.HasItem || holdable == null)
            return;

        if (holdable is PackageHoldable package &&
            GameStateService.IsWarehouse &&
            GameStateService.RequiredPackageNumber > 0 &&
            _flow != null && !_flow.AcceptAnyPackageForReturn)
        {
            if (package.Number != GameStateService.RequiredPackageNumber)
            {
                _client?.PlayWrongPackageConversation();
                return;
            }
            if (GameStateService.WrongPackageDialogueActive)
            {
                DialogueManager.StopConversation();
                GameStateService.SetWrongPackageDialogue(false);
            }
        }

        Transform handPoint = ResolveHandPoint(holdable);
        if (_hands.TryTake(holdable, handPoint) && holdable is PackageHoldable)
            _flow?.ShowEmptyHintAfterPackagePick();
    }

    private Transform ResolveHandPoint(IHoldable holdable)
    {
        Transform defaultPoint = _playerView.HandPoint;
        if (holdable is IHandPointProvider provider)
        {
            return provider.HandPointType == HandPointType.Phone && _playerView.PhoneHandPoint != null
                ? _playerView.PhoneHandPoint
                : defaultPoint;
        }
        return defaultPoint;
    }

    private void TryDropItem()
    {
        if (GameStateService.PackageDropLocked && _hands.Current is PackageHoldable)
            return;
        _hands.DropCurrentItem(_playerView.DropPoint.position, Quaternion.identity);
    }
}
