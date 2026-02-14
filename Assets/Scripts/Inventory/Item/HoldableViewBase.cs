using UnityEngine;

public enum HoldableAvailability
{
    WarehouseOnly,
    Always
}

public abstract class HoldableViewBase : MonoBehaviour, IHoldable
{
    public abstract HoldableAvailability Availability { get; }

    public abstract void OnTaken(Transform handPoint);
    public abstract void OnDropped(Vector3 worldPos, Quaternion worldRot);
}
