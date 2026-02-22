using UnityEngine;
using static IGameFlowController;

/// <summary>
/// Универсальная зона телепорта: при входе/выходе игрока можно уведомлять сценарий (NotifyExitZonePassed).
/// Направление телепорта задаётся Destination (Warehouse / Client). GameFlowController собирает все включённые TravelZone и определяет, в какой зоне игрок.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class TravelZone : MonoBehaviour
{
    public const string ZoneIdWarehouseStoryExited = "warehouse_story_zone_exited";
    public const string ZoneIdWarehouseExitPassed = "warehouse_exit_passed";

    [SerializeField] private TravelTarget _destination = TravelTarget.Warehouse;
    [Tooltip("Если задан — при выходе игрока из зоны вызывается NotifyExitZonePassed с этим id (только если сценарий ждёт warehouse_story_zone_exited).")]
    [SerializeField] private string _notifyExitZoneIdOnExit = "";
    [Tooltip("Если задан — при входе игрока в зону вызывается NotifyExitZonePassed с этим id.")]
    [SerializeField] private string _notifyExitZoneIdOnEnter = "";

    public TravelTarget Destination => _destination;
    public bool PlayerInside { get; private set; }

    private void Awake()
    {
        EnsureTriggerReceivesEvents();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerView>() == null) return;
        PlayerInside = true;

        if (!string.IsNullOrEmpty(_notifyExitZoneIdOnEnter))
        {
            GameFlowController flow = GameFlowController.Instance;
            if (flow != null)
                flow.NotifyExitZonePassed(_notifyExitZoneIdOnEnter);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerView>() == null) return;
        if (!PlayerInside) return;
        PlayerInside = false;

        if (string.IsNullOrEmpty(_notifyExitZoneIdOnExit)) return;

        // warehouse_story_zone_exited — только когда сценарий реально ждёт выхода из зоны
        if (string.Equals(_notifyExitZoneIdOnExit, ZoneIdWarehouseStoryExited, System.StringComparison.OrdinalIgnoreCase))
        {
            GameFlowController flow = GameFlowController.Instance;
            if (flow != null && flow.IsWaitingForWarehouseStoryZoneExit())
            {
                flow.NotifyExitZonePassed(ZoneIdWarehouseStoryExited);
            }
            return;
        }

        GameFlowController f = GameFlowController.Instance;
        if (f != null)
            f.NotifyExitZonePassed(_notifyExitZoneIdOnExit);
    }

    private void EnsureTriggerReceivesEvents()
    {
        if (GetComponent<Rigidbody>() != null) return;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
