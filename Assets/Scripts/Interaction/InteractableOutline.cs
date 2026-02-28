using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подсветка контуром при наведении на интерактивный объект (телефон, радио, роутер, коробки).
/// Добавь компонент на корень объекта или на тот же GameObject, где висит IWorldInteractable / IHoldable.
/// </summary>
public sealed class InteractableOutline : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Color _outlineColor = new Color(0.5f, 0.2f, 0.8f, 1f);
    [SerializeField, Min(0.001f)] private float _outlineWidth = 0.03f;

    private readonly List<GameObject> _outlineObjects = new List<GameObject>();
    private Material _outlineMaterial;
    private bool _highlighted;

    private void Awake()
    {
        CacheOutlineRenderers();
    }

    private void OnDestroy()
    {
        if (_outlineMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(_outlineMaterial);
            else
                DestroyImmediate(_outlineMaterial);
        }
    }

    private void CacheOutlineRenderers()
    {
        Shader shader = Shader.Find("Custom/InteractableOutline");
        if (shader == null)
        {
            Debug.LogWarning("[InteractableOutline] Shader 'Custom/InteractableOutline' not found. Outline disabled.", this);
            return;
        }

        _outlineMaterial = new Material(shader);
        _outlineMaterial.SetColor("_OutlineColor", _outlineColor);
        _outlineMaterial.SetFloat("_OutlineWidth", _outlineWidth);

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer r in renderers)
        {
            if (r.gameObject.name.StartsWith("Outline_"))
                continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;

            GameObject outlineGo = new GameObject("Outline_" + r.gameObject.name);
            outlineGo.transform.SetParent(r.transform, false);
            outlineGo.transform.localPosition = Vector3.zero;
            outlineGo.transform.localRotation = Quaternion.identity;
            outlineGo.transform.localScale = Vector3.one;
            outlineGo.layer = r.gameObject.layer;

            MeshFilter outlineMf = outlineGo.AddComponent<MeshFilter>();
            outlineMf.sharedMesh = mf.sharedMesh;

            MeshRenderer outlineMr = outlineGo.AddComponent<MeshRenderer>();
            outlineMr.sharedMaterial = _outlineMaterial;
            outlineMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineMr.receiveShadows = false;

            outlineGo.SetActive(false);
            _outlineObjects.Add(outlineGo);
        }
    }

    /// <summary>
    /// Включить или выключить подсветку (вызывается из PlayerInteractionController при наведении).
    /// </summary>
    public void SetHighlight(bool on)
    {
        if (_highlighted == on)
            return;
        _highlighted = on;
        for (int i = 0; i < _outlineObjects.Count; i++)
        {
            if (_outlineObjects[i] != null)
                _outlineObjects[i].SetActive(on);
        }
    }
}
