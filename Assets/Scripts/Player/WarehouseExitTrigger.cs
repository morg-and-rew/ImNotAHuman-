using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class WarehouseExitTrigger : MonoBehaviour
{
    public bool PlayerInside { get; private set; }

    private void Awake()
    {
        EnsureTriggerReceivesEvents();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerView>() != null)
            PlayerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerView>() != null)
            PlayerInside = false;
    }

    /// <summary> С CharacterController у игрока триггеры срабатывают, если у зоны есть Rigidbody (kinematic). </summary>
    private void EnsureTriggerReceivesEvents()
    {
        if (GetComponent<Rigidbody>() != null) return;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
