using System.Collections.Generic;
using UnityEngine;

public sealed class InteractionRaycastCache
{
    private const float Distance = 2.4f;
    /// <summary> Настройка: начало луча сдвинуто вперёд от камеры (метры), чтобы луч стартовал уже за пределами CharacterController. Меняй здесь при необходимости. </summary>
    private const float OriginOffset = 0.75f;
    /// <summary> Радиус «толщины» луча — предметы чуть в стороне тоже попадают. </summary>
    private const float RayRadius = 0.12f;
    /// <summary> Количество колец по длине луча при отладочной отрисовке (0 = только линия и сфера). </summary>
    private const int DebugRings = 5;
    /// <summary> Углы вниз от направления камеры (градусы) — чтобы брать предметы ниже с разных поз. </summary>
    private static readonly float[] RayPitchDownDegrees = { 0f, 15f, 30f, 45f };

    private Camera _camera;
    private int _lastRefreshFrame = -1;
    private RaycastHit _hit;
    private bool _hasHit;

    private Collider _lastCollider;
    private IHoldable _cachedHoldable;
    private IWorldInteractable _cachedWorldInteractable;

    private Vector3 _debugOrigin;
    private readonly List<Vector3> _debugDirections = new List<Vector3>(8);

    private static readonly List<MonoBehaviour> _componentBuffer = new List<MonoBehaviour>(8);

    public void Refresh(Camera camera)
    {
        RefreshInternal(camera, allowSkipFrame: true);
    }

    /// <summary> Обновить луч и нарисовать отладку. Вызывать каждый кадр при включённой отладке — тогда луч пересчитывается каждый кадр. </summary>
    public void RefreshAndDrawDebug(Camera camera)
    {
        RefreshInternal(camera, allowSkipFrame: false);
        DrawDebugRays();
    }

    private void RefreshInternal(Camera camera, bool allowSkipFrame)
    {
        if (camera == null)
        {
            _hasHit = false;
            return;
        }

        if (allowSkipFrame)
        {
            if (Time.frameCount == _lastRefreshFrame)
                return;
            bool shouldCast = (Time.frameCount % 2) == 0;
            if (!shouldCast && _lastRefreshFrame >= 0)
                return;
        }

        _lastRefreshFrame = Time.frameCount;
        _camera = camera;

        Vector3 forward = camera.transform.forward;
        Vector3 right = camera.transform.right;
        Vector3 origin = camera.transform.position + forward * OriginOffset;

        _debugOrigin = origin;
        _debugDirections.Clear();

        _hasHit = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < RayPitchDownDegrees.Length; i++)
        {
            float angle = -RayPitchDownDegrees[i];
            Vector3 dir = Quaternion.AngleAxis(angle, right) * forward;
            _debugDirections.Add(dir);
            Ray ray = new Ray(origin, dir);
            if (Physics.SphereCast(ray, RayRadius, out RaycastHit h, Distance) && h.distance < bestDistance)
            {
                bestDistance = h.distance;
                _hit = h;
                _hasHit = true;
            }
        }

        if (!_hasHit)
        {
            _lastCollider = null;
            _cachedHoldable = null;
            _cachedWorldInteractable = null;
        }
    }

    /// <summary> Рисует луч в сцене (линия, кольца по длине, сфера в точке попадания). Видно в окне Scene при воспроизведении. </summary>
    public void DrawDebugRays()
    {
        Vector3 dir = _debugDirections.Count > 0 ? _debugDirections[0] : Vector3.forward;
        Vector3 endOrHitPoint = _hasHit ? _hit.point : _debugOrigin + dir * Distance;
        DrawSphereCastDebug(_debugOrigin, dir, Distance, _hasHit, endOrHitPoint);
    }

    private void DrawSphereCastDebug(Vector3 origin, Vector3 dir, float dist, bool hitAny, Vector3 endOrHitPoint)
    {
        const float duration = 2f;
        float radius = Mathf.Max(RayRadius, 0.05f);

        DrawWireSphere(origin, radius * 0.8f, Color.yellow, duration);
        Debug.DrawLine(origin, origin + dir * dist, hitAny ? Color.green : Color.red, duration);

        if (DebugRings > 0)
        {
            float step = dist / DebugRings;
            Color color = hitAny ? Color.green : Color.red;
            for (int i = 0; i <= DebugRings; i++)
            {
                Vector3 p = origin + dir * (step * i);
                DrawCircle(p, dir, radius, color, duration);
            }
        }

        DrawWireSphere(endOrHitPoint, radius, hitAny ? Color.cyan : Color.yellow, duration);
    }

    private static void DrawCircle(Vector3 center, Vector3 dir, float radius, Color color, float duration)
    {
        Vector3 n = dir.normalized;
        Vector3 a = Vector3.Cross(n, Vector3.up);
        if (a.sqrMagnitude < 0.0001f)
            a = Vector3.Cross(n, Vector3.right);
        a.Normalize();
        Vector3 b = Vector3.Cross(n, a).normalized;

        const int segments = 16;
        float angleStep = 360f / segments;
        Vector3 prev = center + a * radius;
        for (int i = 1; i <= segments; i++)
        {
            float ang = angleStep * i * Mathf.Deg2Rad;
            Vector3 next = center + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * radius;
            Debug.DrawLine(prev, next, color, duration);
            prev = next;
        }
    }

    private static void DrawWireSphere(Vector3 center, float radius, Color color, float duration)
    {
        DrawCircle(center, Vector3.forward, radius, color, duration);
        DrawCircle(center, Vector3.up, radius, color, duration);
        DrawCircle(center, Vector3.right, radius, color, duration);
    }

    /// <summary> Точка попадания луча (или конец луча, если нет попадания). Для отображения линии в сцене. </summary>
    public void GetDebugLine(out Vector3 origin, out Vector3 end)
    {
        origin = _debugOrigin;
        end = _hasHit ? _hit.point : (_debugDirections.Count > 0 ? _debugOrigin + _debugDirections[0] * Distance : _debugOrigin + Vector3.forward * Distance);
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
