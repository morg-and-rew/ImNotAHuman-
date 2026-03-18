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
    [Header("Target (optional)")]
    [Tooltip("Если задан — подсветка строится по этому Renderer. Полезно, когда на этом объекте нет рендера (только логика/коллайдер).")]
    [SerializeField] private Renderer _targetRenderer;
    [Tooltip("Если задан, скрипт ищет Renderer в детях этого объекта. Работает как более удобная альтернатива _targetRenderer.")]
    [SerializeField] private Transform _targetRoot;

    private GameObject _outlineObject;
    private Material _outlineMaterial;
    private bool _highlighted;

    private void Start()
    {
        CacheOutline();
    }

    private void Awake()
    {
        // На всякий случай: в режиме редактирования/рантайме, чтобы Unity не применял статические оптимизации
        // к объекту обводки (из-за них обводка "ломается", когда на исходном объекте включен Static).
        // Сам outline отключать нельзя, нужен рендер outline-объекта.
        EnsureOutlineObjectNotStatic();
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

        // Главное: outline должен рендериться, но при этом ему нельзя быть Static,
        // иначе Unity может объединять/переупаковывать меши и outline начинает "съезжать".
        EnsureOutlineObjectNotStatic();
    }

    private Renderer FindRenderer()
    {
        if (_targetRenderer != null && _targetRenderer.gameObject != null)
        {
            // На outline может быть Static, а на target — тоже. Мы отдельно контролируем Static для outline proxy.
            return _targetRenderer;
        }

        if (_targetRoot != null)
        {
            // Ищем в заранее указанном корне, а не "в текущем объекте".
            var mrTarget = _targetRoot.GetComponent<MeshRenderer>();
            if (mrTarget != null && !mrTarget.gameObject.name.StartsWith("Outline_")) return mrTarget;
            var smrTarget = _targetRoot.GetComponent<SkinnedMeshRenderer>();
            if (smrTarget != null && !smrTarget.gameObject.name.StartsWith("Outline_")) return smrTarget;

            foreach (var r in _targetRoot.GetComponentsInChildren<MeshRenderer>(true))
                if (r != null && !r.gameObject.name.StartsWith("Outline_")) return r;
            foreach (var r in _targetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r != null && !r.gameObject.name.StartsWith("Outline_")) return r;
        }

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

    private void EnsureOutlineObjectNotStatic()
    {
        if (_outlineObject == null) return;

        // Runtime-safe: отключаем флаг "Static" (это влияет на batching/оптимизации).
        _outlineObject.isStatic = false;

#if UNITY_EDITOR
        // В редакторе дополнительно снимаем static editor flags (ContributeGI, LightmapStatic и т.п.)
        // чтобы избежать поведения в билде/лайтмапах.
        UnityEditor.GameObjectUtility.SetStaticEditorFlags(_outlineObject,
            (UnityEditor.StaticEditorFlags)0);
#endif
    }
}
