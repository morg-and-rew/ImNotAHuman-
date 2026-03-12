using UnityEngine;

public interface IPlayerInput
{
    Vector2 MoveAxis { get; }
    Vector2 LookDelta { get; }

    bool InteractPressed { get; }
    bool UseItemPressed { get; }
    bool DropItemPressed { get; }

    bool IsDropHeld { get; }
    bool DropItemReleased { get; }

    bool NextPressed {  get; }
    bool ConfirmPressed {  get; }

    /// <summary> Поворот коробки по часовой стрелке (Q). </summary>
    bool RotateBoxPressed { get; }

    void Update();
}