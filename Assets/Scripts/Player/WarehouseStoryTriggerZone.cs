using UnityEngine;

/// <summary>
/// Триггерная зона на складе: когда игрок зашёл в неё и вышел — вызывается NotifyExitZonePassed(zoneId).
/// Используется для сценария «после стука зайти на склад, выйти из зоны» → телепорт к клиенту и диалог Client_Day1.5.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class WarehouseStoryTriggerZone : MonoBehaviour
{
    public const string ZoneIdExited = "warehouse_story_zone_exited";

    [SerializeField] private string _zoneId = ZoneIdExited;

    private bool _playerInside;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerView>(out _)) return;
        _playerInside = true;
        // #region agent log
        AgentDebugLog.Log("WarehouseStoryTriggerZone.cs:OnTriggerEnter", "player entered zone", "{\"zoneId\":\"" + _zoneId + "\"}", "H2");
        // #endregion
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<PlayerView>(out _)) return;
        if (!_playerInside) return;
        _playerInside = false;

        // #region agent log
        AgentDebugLog.Log("WarehouseStoryTriggerZone.cs:OnTriggerExit", "player exited zone, notifying", "{\"zoneId\":\"" + _zoneId + "\"}", "H2");
        // #endregion
        GameFlowController flow = GameFlowController.Instance;
        if (flow != null)
            flow.NotifyExitZonePassed(_zoneId);
    }
}
