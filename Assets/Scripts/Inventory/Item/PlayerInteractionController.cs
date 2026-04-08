using System;
using System.IO;
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
    private InteractableOutline _lastOutlineTarget;

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

        UpdateInteractionHint(holdable, worldInteractable);
        UpdateHoverOutline(holdable, worldInteractable);

        // Во время любого диалога блокируем E и Q.
        if (DialogueManager.isConversationActive)
            return;

        if (_hands.HasItem)
        {
            if (_input.InteractPressed)
                TryDropItem();
            return;
        }

        // Q — поворот коробки по часовой стрелке (только если коробка не в 0°).
        if (_input.RotateBoxPressed && holdable is PackageHoldable pkgRotate &&
            IsHoldableAllowed(pkgRotate) && !pkgRotate.CanPickupByRotation)
        {
            pkgRotate.RotateClockwise();
            return;
        }

        if (!_input.InteractPressed)
            return;

        if (holdable != null && IsHoldableAllowed(holdable))
        {
            TryPickItem(holdable);
            return;
        }

        if (worldInteractable != null)
            worldInteractable.Interact(_input);
    }

    private void UpdateInteractionHint(IHoldable holdable, IWorldInteractable worldInteractable)
    {
        bool holdingPhone = _hands.HasItem && _hands.Current is PhoneItemView;
        GameFlowController flowController = GameFlowController.Instance;
        bool canShowClientHint = flowController != null && flowController.ShouldShowClientInteractHint();
        bool preferClientHint = _client != null && _client.IsPlayerInside
            && _client.IsPlayerLookingAtClient(_playerView)
            && !holdingPhone
            && canShowClientHint
            && (!_client.IsActive || _client.IsWaitingForContinue);

        Sprite sprite = null;
        if (!preferClientHint)
        {
            if (worldInteractable != null && IsWorldInteractableFeedbackAllowed(worldInteractable))
                sprite = worldInteractable.HintSprite;
            else if (holdable is PackageHoldable pkg && IsHoldableAllowed(pkg) && !_hands.HasItem && IsPackageFrontAccessible(pkg))
            {
                // Для «не той» посылки: если коробка не повёрнута — показываем «Q — повернуть», иначе подсказку про диалог.
                if (IsWrongPackageForStory(pkg))
                    sprite = pkg.CanPickupByRotation ? pkg.HintSpriteWrongPackage : pkg.HintSpriteRotate;
                else
                    sprite = pkg.HintSprite;
            }
            else if (holdable is StoryCarryItem storyCarry && IsHoldableAllowed(storyCarry) && !_hands.HasItem)
                sprite = ResolveStoryCarryHintSprite(storyCarry);
        }

        if (PlayerHintView.Instance != null)
            PlayerHintView.Instance.SetRaycastHint(sprite);
    }

    private void UpdateHoverOutline(IHoldable holdable, IWorldInteractable worldInteractable)
    {
        InteractableOutline current = GetHighlightTarget(holdable, worldInteractable);
        if (_lastOutlineTarget == current)
            return;
        _lastOutlineTarget?.SetHighlight(false);
        _lastOutlineTarget = current;
        current?.SetHighlight(true);
    }

    private InteractableOutline GetHighlightTarget(IHoldable holdable, IWorldInteractable worldInteractable)
    {
        bool holdingPhone = _hands.HasItem && _hands.Current is PhoneItemView;
        GameFlowController flowController = GameFlowController.Instance;
        bool canShowClientHint = flowController != null && flowController.ShouldShowClientInteractHint();
        bool preferClientHint = _client != null && _client.IsPlayerInside
            && _client.IsPlayerLookingAtClient(_playerView)
            && !holdingPhone
            && canShowClientHint
            && (!_client.IsActive || _client.IsWaitingForContinue);
        if (preferClientHint)
            return null;
        if (worldInteractable is RouterInteractable router && router.HintSprite == null)
            return null;
        if (worldInteractable != null && IsWorldInteractableFeedbackAllowed(worldInteractable))
            return GetOutlineFrom((worldInteractable as MonoBehaviour));
        if (holdable is PackageHoldable pkg && IsHoldableAllowed(pkg) && !_hands.HasItem && IsPackageFrontAccessible(pkg))
            return GetOutlineFrom(pkg as MonoBehaviour);
        if (holdable is StoryCarryItem sc && IsHoldableAllowed(sc) && !_hands.HasItem)
            return GetOutlineFrom(sc as MonoBehaviour);
        if (holdable is PhoneItemView && IsHoldableAllowed(holdable))
            return GetOutlineFrom((holdable as MonoBehaviour));
        return null;
    }

    private static bool IsWorldInteractableFeedbackAllowed(IWorldInteractable worldInteractable)
    {
        if (worldInteractable is RadioInteractable radio)
            return radio.CanShowInteractionFeedback();
        if (worldInteractable is CandleInteractable candle)
            return candle.CanShowInteractionFeedback();
        return true;
    }

    /// <summary> Ищем InteractableOutline на том же объекте, родителе или в детях — у телефона коллайдер часто на корне, а outline на дочернем (55/model_0_1). </summary>
    private static InteractableOutline GetOutlineFrom(MonoBehaviour mb)
    {
        if (mb == null) return null;
        return mb.GetComponent<InteractableOutline>()
            ?? mb.GetComponentInParent<InteractableOutline>()
            ?? mb.GetComponentInChildren<InteractableOutline>();
    }

    private Sprite ResolveStoryCarryHintSprite(StoryCarryItem item)
    {
        if (item == null)
            return null;
        if (_flow != null && _flow.IsUiEnglishLocale && item.HintSpriteEnglish != null)
            return item.HintSpriteEnglish;
        return item.HintSprite;
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
        if (holdable is StoryCarryItem storyCarry)
            return GameStateService.IsWarehouse && _flow != null && _flow.IsStoryCarryItemPickupAllowed(storyCarry);
        return GameStateService.IsWarehouse;
    }

    /// <summary> Нужная посылка по сюжету задана, а эта коробка — не она (E → диалог, но только когда коробка уже стоит правильно). </summary>
    private bool IsWrongPackageForStory(PackageHoldable pkg)
    {
        if (pkg == null) return false;
        return GameStateService.IsWarehouse
            && GameStateService.RequiredPackageNumber > 0
            && _flow != null && !_flow.AcceptAnyPackageForReturn
            && pkg.Number != GameStateService.RequiredPackageNumber;
    }

    private bool IsPackageFrontAccessible(PackageHoldable pkg)
    {
        return pkg != null && pkg.IsInteractableFromFront(_playerView != null ? _playerView.transform : null);
    }

    private void TryPickItem(IHoldable holdable)
    {
        if (_hands.HasItem || holdable == null)
            return;

        if (holdable is PackageHoldable pkg)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[PlayerInteractionController] TryPickItem PackageHoldable. " +
                $"state={GameStateService.CurrentState} requiredPackage={GameStateService.RequiredPackageNumber} acceptAny={_flow != null && _flow.AcceptAnyPackageForReturn} " +
                $"allowedByStory={_flow != null && _flow.IsPackagePickAllowedByStory} canPickupByRotation={pkg.CanPickupByRotation} currentAngleY={pkg.transform.eulerAngles.y} " +
                $"isWrongPackageForStory={IsWrongPackageForStory(pkg)}");
#endif
            // Любая попытка взаимодействия (взять или показать «не та посылка»)
            // возможна только когда коробка уже стоит правильно (0° по Y).
            if (!pkg.CanPickupByRotation)
                return;

            // Брать/взаимодействовать с коробкой можно только с фронтальной стороны стеллажа.
            if (!IsPackageFrontAccessible(pkg))
                return;

            // Неправильная посылка по сюжету: E → только диалог, без подъёма.
            if (IsWrongPackageForStory(pkg))
            {
                _client?.PlayWrongPackageConversation();
                return;
            }

            // Перед реальным взятием гасим активный диалог про неправильную посылку (если ещё идёт).
            if (GameStateService.WrongPackageDialogueActive)
            {
                DialogueManager.StopConversation();
                GameStateService.SetWrongPackageDialogue(false);
            }
        }

        Transform handPoint = ResolveHandPoint(holdable);
        if (_hands.TryTake(holdable, handPoint) && holdable is PackageHoldable)
        {
            _flow?.NotifyTutorialActionCompleted(IGameFlowController.TutorialPendingAction.WarehousePick);
            _flow?.ShowEmptyHintAfterPackagePick();
        }
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
