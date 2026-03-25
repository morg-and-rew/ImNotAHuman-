using UnityEngine;

[DisallowMultipleComponent]
public sealed class StoryCarryItem : HoldableViewBase, IHandPointProvider
{
    [SerializeField] private string _itemId = "";
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Collider _col;

    public string ItemId => _itemId;
    public override HoldableAvailability Availability => HoldableAvailability.WarehouseOnly;
    public HandPointType HandPointType => HandPointType.Default;

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
    }

    public override void OnTaken(Transform handPoint)
    {
        if (handPoint == null) return;
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
