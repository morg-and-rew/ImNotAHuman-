using System;
using UnityEngine;

public sealed class PlayerHands
{
    private IHoldable _current;

    public bool HasItem => _current != null;
    public IHoldable Current => _current;

    public event Action<IHoldable> Taken;
    public event Action<IHoldable> Dropped;

    public bool TryTake(IHoldable item, Transform handPoint)
    {
        if (item == null || _current != null || handPoint == null)
            return false;

        _current = item;
        _current.OnTaken(handPoint);

        Taken?.Invoke(item);
        return true;
    }

    public void DropCurrentItem(Vector3 dropPos, Quaternion dropRot)
    {
        if (_current == null) return;

        if (_current is PhoneItemView phone && phone.CanDrop != null && !phone.CanDrop())
            return;

        IHoldable dropped = _current;

        _current.OnDropped(dropPos, dropRot);
        _current = null;

        Dropped?.Invoke(dropped);
    }


    public void DestroyCurrentItem()
    {
        if (_current == null) return;

        IHoldable toDestroy = _current;

        _current = null;

        Dropped?.Invoke(toDestroy);

        if (toDestroy is MonoBehaviour mb)
            UnityEngine.Object.Destroy(mb.gameObject);
        else if (toDestroy is UnityEngine.Object uo)
            UnityEngine.Object.Destroy(uo);
    }
}

public static class HandsRegistry
{
    public static PlayerHands Hands { get; private set; }

    public static void Set(PlayerHands hands)
    {
        Hands = hands;
    }
}

public enum HandPointType
{
    Default,
    Phone
}
