using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ExitCheckZone : MonoBehaviour
{
    [SerializeField] private string _zoneId = "warehouse_exit_passed";

    private void Awake()
    {
        if (GetComponent<Rigidbody>() != null) return;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerView>() == null) return;

        GameFlowController flow = GameFlowController.Instance;
        if (flow != null)
            flow.NotifyExitZonePassed(_zoneId);
    }
}
