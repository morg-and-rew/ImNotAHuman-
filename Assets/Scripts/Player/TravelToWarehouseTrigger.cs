using UnityEngine;

/// <summary>
/// Зона на основной локации: когда игрок внутри, он может нажать F для перехода на склад.
/// Учитывает CharacterController у игрока (поиск PlayerView через GetComponentInParent, Rigidbody на зоне).
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class TravelToWarehouseTrigger : MonoBehaviour
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
