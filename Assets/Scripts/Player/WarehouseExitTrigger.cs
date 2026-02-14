using UnityEngine;

public sealed class WarehouseExitTrigger : MonoBehaviour
{
    public bool PlayerInside { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
            PlayerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _))
            PlayerInside = false;
    }
}
