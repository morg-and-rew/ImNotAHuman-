using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ExitCheckZone : MonoBehaviour
{
    [SerializeField] private string _zoneId = "warehouse_exit_passed";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerView>(out _)) return;

        GameFlowController flow = GameFlowController.Instance;
        if (flow != null)
            flow.NotifyExitZonePassed(_zoneId);
    }
}
