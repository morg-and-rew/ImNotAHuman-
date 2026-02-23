using System;
using UnityEngine;

public sealed class PhoneItemView : HoldableViewBase, IHandPointProvider, IWorldInteractable
{
    public override HoldableAvailability Availability => HoldableAvailability.Always;
    public HandPointType HandPointType => HandPointType.Phone;

    public event Action Taken;
    public event Action Dropped;

    [Header("Hint")]
    [SerializeField] private Sprite _hintSprite;
    public Sprite HintSprite => _hintSprite;
    public Func<bool> CanDrop { get; internal set; }

    [Header("Physics")]
    [SerializeField] private Collider _col;
    [SerializeField] private MeshRenderer _meshRenderer;

    [Header("Return point (where the phone lies)")]
    [SerializeField] private Transform _worldPoint; 

    [Header("Scale")]
    [SerializeField] private float _heldScaleMultiplier = 2f;

    private Vector3 _originalScale;
    private bool _isTaken;

    private void Awake()
    {
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        _originalScale = transform.localScale;

        if (_worldPoint != null)
        {
            transform.SetParent(_worldPoint, true);
            transform.position = _worldPoint.position;
            transform.rotation = _worldPoint.rotation;
            transform.localScale = _originalScale;
        }
    }

    private void Reset()
    {
        _col = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    public void Interact(IPlayerInput input)
    {
        if (_isTaken) return;

        OnTaken(null); 
    }

    public override void OnTaken(Transform handPoint)
    {
        if (handPoint == null) return;

        _isTaken = true;

        if (_col != null) _col.enabled = false;

        transform.SetParent(handPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (_meshRenderer != null) _meshRenderer.enabled = false;

        Taken?.Invoke();
    }

    public override void OnDropped(Vector3 worldPos, Quaternion worldRot)
    {
        _isTaken = false;

        if (_meshRenderer != null) _meshRenderer.enabled = true;

        ReturnToWorldPoint();

        Dropped?.Invoke();
    }

    public void ReturnToWorldPoint()
    {
        if (_worldPoint != null)
        {
            transform.SetParent(_worldPoint, true);
            transform.position = _worldPoint.position;
            transform.rotation = _worldPoint.rotation;
        }
        else
        {
            transform.SetParent(null, true);
        }

        transform.localScale = _originalScale;

        if (_col != null) _col.enabled = true;
    }
}
