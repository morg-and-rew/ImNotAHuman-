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

        if (!_input.InteractPressed)
            return;

        if (_hands.HasItem)
        {
            TryDropItem();
            return;
        }

        // #region agent log
        if (holdable is PackageHoldable pkgDenied && !IsHoldableAllowed(holdable))
            AgentDebugLog.Log("PlayerInteractionController.cs:Tick", "package denied", "{\"state\":\"" + GameStateService.CurrentState + "\",\"isWarehouse\":" + GameStateService.IsWarehouse.ToString().ToLowerInvariant() + ",\"requiredNum\":" + GameStateService.RequiredPackageNumber + ",\"packageNum\":" + pkgDenied.Number + "}", "H_pickup");
        // #endregion

        if (holdable != null && IsHoldableAllowed(holdable))
        {
            // #region agent log
            if (holdable is PackageHoldable pkg)
                AgentDebugLog.Log("PlayerInteractionController.cs:Tick", "pick attempt package", "{\"state\":\"" + GameStateService.CurrentState + "\",\"isWarehouse\":" + GameStateService.IsWarehouse.ToString().ToLowerInvariant() + ",\"requiredNum\":" + GameStateService.RequiredPackageNumber + ",\"packageNum\":" + pkg.Number + ",\"allowed\":" + IsHoldableAllowed(holdable).ToString().ToLowerInvariant() + "}", "H_pickup");
            // #endregion
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

    private bool IsHoldableAllowed(IHoldable holdable)
    {
        if (holdable is IHandPointProvider hp && hp.HandPointType == HandPointType.Phone)
            return _flow != null && _flow.IsPhonePickupAllowed();
        if (holdable is PhoneItemView && !GameStateService.PhoneUnlocked)
            return false;
        // Посылки — только по сюжету, когда задан номер требуемой посылки.
        if (holdable is PackageHoldable)
            return GameStateService.IsWarehouse && GameStateService.RequiredPackageNumber > 0;
        return GameStateService.IsWarehouse;
    }

    private void TryPickItem(IHoldable holdable)
    {
        if (_hands.HasItem || holdable == null)
            return;

        if (holdable is PackageHoldable package &&
            GameStateService.IsWarehouse &&
            GameStateService.RequiredPackageNumber > 0)
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
