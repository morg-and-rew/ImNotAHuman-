using UnityEngine;

public interface IWorldInteractable
{
    void Interact(IPlayerInput input);
    Sprite HintSprite { get; }
}
