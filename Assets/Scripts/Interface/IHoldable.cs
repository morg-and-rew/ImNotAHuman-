using UnityEngine;

public interface IHoldable
{
    void OnTaken(Transform handPoint);
    void OnDropped(Vector3 worldPos, Quaternion worldRot);
}
