using UnityEngine;

public interface IWorldInteractable
{
    public void Interact(IPlayerInput input);

    Canvas hint { get; }
}
