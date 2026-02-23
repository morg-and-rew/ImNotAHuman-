using UnityEngine;
using static IGameFlowController;

[RequireComponent(typeof(Collider))]
public sealed class TravelZone : MonoBehaviour
{
    public const string ZoneIdWarehouseStoryExited = "warehouse_story_zone_exited";
    public const string ZoneIdWarehouseExitPassed = "warehouse_exit_passed";

    [SerializeField] private TravelTarget _destination = TravelTarget.Warehouse;
    [SerializeField] private string _notifyExitZoneIdOnExit = "";
    [SerializeField] private string _notifyExitZoneIdOnEnter = "";
    [Header("Hint")]
    [SerializeField] private GameObject _hintCanvas;

    public TravelTarget Destination => _destination;
    public bool PlayerInside { get; private set; }

    private void Awake()
    {
        EnsureTriggerReceivesEvents();
    }

    private void Start()
    {
        if (_hintCanvas != null)
            _hintCanvas.SetActive(false);
        LookAtCamera.Ensure(_hintCanvas);
    }

    private void Update()
    {
        if (_hintCanvas == null) return;
        bool show = PlayerInside
            && GameFlowController.Instance != null
            && GameFlowController.Instance.ShouldShowDoorHintFor(_destination);
        _hintCanvas.SetActive(show);
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
