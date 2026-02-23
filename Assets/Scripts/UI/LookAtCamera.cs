using UnityEngine;

[ExecuteAlways]
public sealed class LookAtCamera : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private bool _onlyYAxis;

    public static void Ensure(GameObject target)
    {
        if (target == null) return;
        if (target.GetComponent<LookAtCamera>() == null)
            target.AddComponent<LookAtCamera>();
    }

    private void LateUpdate()
    {
        Camera cam = _camera != null ? _camera : (GameFlowController.Instance != null ? GameFlowController.Instance.PlayerCamera : null) ?? Camera.main;
        if (cam == null) return;

        if (_onlyYAxis)
        {
            Vector3 dir = cam.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            transform.LookAt(cam.transform);
        }
    }
}
