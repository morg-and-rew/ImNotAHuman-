using System.Collections.Generic;
using UnityEngine;

public sealed class InteractionRaycastCache
{
    private const float Distance = 1.6f;

    private Camera _camera;
    private int _lastRefreshFrame = -1;
    private RaycastHit _hit;
    private bool _hasHit;

    private Collider _lastCollider;
    private IHoldable _cachedHoldable;
    private IWorldInteractable _cachedWorldInteractable;

    private static readonly List<MonoBehaviour> _componentBuffer = new List<MonoBehaviour>(8);

    public void Refresh(Camera camera)
    {
        if (camera == null)
        {
            _hasHit = false;
            return;
        }

        if (Time.frameCount == _lastRefreshFrame)
            return;

        bool shouldCast = (Time.frameCount % 2) == 0;
        if (!shouldCast && _lastRefreshFrame >= 0)
            return;

        _lastRefreshFrame = Time.frameCount;
        _camera = camera;

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        _hasHit = Physics.Raycast(ray, out _hit, Distance);

        if (!_hasHit)
        {
            _lastCollider = null;
            _cachedHoldable = null;
            _cachedWorldInteractable = null;
        }
    }

    public bool TryGetHit(out RaycastHit hit)
    {
        hit = _hit;
        return _hasHit;
    }

    public IHoldable GetHoldable()
    {
        if (!_hasHit || _hit.collider == null) return null;

        if (_hit.collider == _lastCollider && _cachedHoldable != null)
            return _cachedHoldable;

        if (_hit.collider != _lastCollider)
        {
            _lastCollider = _hit.collider;
            _cachedWorldInteractable = null;
        }

        _cachedHoldable = FindHoldableInParent(_hit.collider.transform);
        return _cachedHoldable;
    }

    public IWorldInteractable GetWorldInteractable()
    {
        if (!_hasHit || _hit.collider == null) return null;

        if (_hit.collider == _lastCollider && _cachedWorldInteractable != null)
            return _cachedWorldInteractable;

        if (_hit.collider != _lastCollider)
        {
            _lastCollider = _hit.collider;
            _cachedHoldable = null;
        }

        _cachedWorldInteractable = _hit.collider.GetComponentInParent<IWorldInteractable>();
        return _cachedWorldInteractable;
    }

    private static IHoldable FindHoldableInParent(Transform t)
    {
        while (t != null)
        {
            _componentBuffer.Clear();
            t.GetComponents(_componentBuffer);
            for (int i = 0; i < _componentBuffer.Count; i++)
            {
                if (_componentBuffer[i] is IHoldable h)
                    return h;
            }
            t = t.parent;
        }
        return null;
    }
}
