using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Подсветка контуром при наведении. Берём рендер (на этом объекте или в детях) и делаем обводку его меша — дочерним объектом этого рендера, в его системе координат.
/// </summary>
public sealed class InteractableOutline : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Color _outlineColor = new Color(0.5f, 0.2f, 0.8f, 1f);
    [SerializeField, Range(0.005f, 0.05f)] private float _shellScale = 0.015f;
    [SerializeField] private Shader _outlineShader;

    private GameObject _outlineObject;
    private Material _outlineMaterial;
    private bool _highlighted;

    private void Start()
    {
        CacheOutline();
    }

    private void OnDestroy()
    {
        if (_outlineMaterial != null)
        {
            if (Application.isPlaying) Destroy(_outlineMaterial);
            else DestroyImmediate(_outlineMaterial);
        }
    }

    private void CacheOutline()
    {
        Renderer r = FindRenderer();
        if (r == null)
        {
            Debug.LogWarning("[InteractableOutline] Не найден MeshRenderer или SkinnedMeshRenderer на этом объекте или в детях.", this);
            return;
        }

        Mesh mesh = GetMeshFrom(r);
        if (mesh == null)
        {
            Debug.LogWarning("[InteractableOutline] У рендера нет меша.", this);
            return;
        }

        Shader shader = _outlineShader != null ? _outlineShader : Shader.Find("Custom/InteractableOutlineShell");
        if (shader == null)
        {
            Debug.LogWarning("[InteractableOutline] Shader Custom/InteractableOutlineShell не найден. Добавь в Always Included Shaders или укажи в Inspector.", this);
            return;
        }

        _outlineMaterial = new Material(shader);
        _outlineMaterial.SetColor("_OutlineColor", _outlineColor);

        float scale = 1f + _shellScale;
        Vector3 outlineScale = new Vector3(scale, scale, scale);

        // Обводка — дочерний объект самого рендера, чтобы была в одной системе координат с мешем (не улетала)
        _outlineObject = new GameObject("Outline_" + r.gameObject.name);
        _outlineObject.transform.SetParent(r.transform, false);
        _outlineObject.transform.localPosition = Vector3.zero;
        _outlineObject.transform.localRotation = Quaternion.identity;
        _outlineObject.transform.localScale = outlineScale;
        _outlineObject.layer = r.gameObject.layer;

        var mf = _outlineObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = _outlineObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _outlineMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        _outlineObject.SetActive(false);
    }

    private Renderer FindRenderer()
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr != null && !mr.gameObject.name.StartsWith("Outline_")) return mr;
        var smr = GetComponent<SkinnedMeshRenderer>();
        if (smr != null && !smr.gameObject.name.StartsWith("Outline_")) return smr;
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            if (r != null && !r.gameObject.name.StartsWith("Outline_")) return r;
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r != null && !r.gameObject.name.StartsWith("Outline_")) return r;
        return null;
    }

    private static Mesh GetMeshFrom(Renderer r)
    {
        if (r is MeshRenderer mr)
        {
            var mf = mr.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }
        if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
        return null;
    }

    public void SetHighlight(bool on)
    {
        if (_highlighted == on) return;
        _highlighted = on;
        if (_outlineObject != null)
            _outlineObject.SetActive(on);
    }
}
