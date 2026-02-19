using UnityEngine;

public sealed class PackageHoldable : HoldableViewBase, IHandPointProvider
{
    public override HoldableAvailability Availability => HoldableAvailability.WarehouseOnly;
    public HandPointType HandPointType => HandPointType.Default;

    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Collider _col;
    [SerializeField] private PackageItem _packageItem;

    public int Number => _packageItem != null ? _packageItem.Number : 0;

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _packageItem = GetComponentInParent<PackageItem>();
    }

    private void Awake()
    {
        if (_packageItem == null)
            _packageItem = GetComponentInParent<PackageItem>();
    }

    public override void OnTaken(Transform handPoint)
    {
        if (handPoint == null) return;

        _packageItem?.NotifyTakenFromWarehouse();

        transform.SetParent(handPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        if (_col != null) _col.enabled = false;
    }

    public override void OnDropped(Vector3 worldPos, Quaternion worldRot)
    {
        transform.SetParent(null);
        transform.position = worldPos;
        transform.rotation = worldRot;

        if (_col != null) _col.enabled = true;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
