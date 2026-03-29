using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Чёрный силуэт в мире (Quad + URP Unlit). Ближе к игроку; в пике — непрозрачный чёрный, при появлении/угасании — короткий прозрачный fade.
/// </summary>
public sealed class RuntimeDizzyVillainSilhouette
{
    public const float FadeInPhaseStart = 0.14f;
    public const float FadeInPhaseEnd = 0.5f;
    public const float FadeOutFallStart = 0.38f;
    public const float MaxAlpha = 1f;

    private const float WorldHorizontalDistance = 2.65f;
    private const float PlaceholderHeightWorld = 4.6f;
    private const float PlaceholderWidthWorld = 1.85f;
    private const int RendererSortingOrder = 5000;
    /// <summary> Ниже — только прозрачный fade; от порога — полностью непрозрачный чёрный. </summary>
    private const float OpaqueAlphaThreshold = 0.88f;

    private readonly GameObject _root;
    private readonly MeshRenderer _meshRenderer;
    private readonly Material _material;
    private readonly Texture2D _proceduralTexture;
    private readonly bool _tintWhiteForTexture;
    private readonly string _baseColorProperty;
    private bool _destroyed;
    private bool _usingOpaque;

    private RuntimeDizzyVillainSilhouette(
        GameObject root,
        MeshRenderer meshRenderer,
        Material material,
        Texture2D proceduralTexture,
        bool tintWhiteForTexture,
        string baseColorProperty)
    {
        _root = root;
        _meshRenderer = meshRenderer;
        _material = material;
        _proceduralTexture = proceduralTexture;
        _tintWhiteForTexture = tintWhiteForTexture;
        _baseColorProperty = baseColorProperty;
    }

    public static RuntimeDizzyVillainSilhouette TryCreate(
        Vector3 anchorWorldPos,
        Vector3 horizontalForward,
        Sprite customSprite,
        Camera worldCamera)
    {
        _ = worldCamera;

        Vector3 flat = new Vector3(horizontalForward.x, 0f, horizontalForward.z);
        if (flat.sqrMagnitude < 0.0001f)
            flat = Vector3.forward;
        flat.Normalize();

        Vector3 worldPos = anchorWorldPos + flat * WorldHorizontalDistance;

        float worldW = PlaceholderWidthWorld;
        float worldH = PlaceholderHeightWorld;
        if (customSprite != null)
        {
            float sh = Mathf.Max(1f, customSprite.rect.height);
            float sw = customSprite.rect.width;
            float scale = PlaceholderHeightWorld / sh;
            worldH = PlaceholderHeightWorld;
            worldW = sw * scale;
        }

        float worldHeightMeters = worldH;
        worldPos.y = anchorWorldPos.y + worldHeightMeters * 0.5f;

        GameObject root = new GameObject("TempDizzyVillainSilhouetteWorld");
        root.transform.position = worldPos;
        SetLayerRecursively(root, 0);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.name = "SilhouetteQuad";
        quad.transform.SetParent(root.transform, false);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localScale = new Vector3(worldW, worldH, 1f);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        SetLayerRecursively(quad, 0);

        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sortingOrder = RendererSortingOrder;
        mr.enabled = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[RuntimeDizzyVillainSilhouette] Не найден шейдер URP Unlit / Unlit/Color — силуэт не создан.");
#endif
            Object.Destroy(root);
            return null;
        }

        Material mat = new Material(shader);
        string colorProp = GetBaseColorPropertyName(mat);

        Texture2D procTex = null;
        bool tintWhite = false;

        if (customSprite != null && customSprite.texture != null)
        {
            mat.mainTexture = customSprite.texture;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", customSprite.texture);
            tintWhite = true;
        }
        else
        {
            procTex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "TempSilhouetteWhite",
                hideFlags = HideFlags.HideAndDontSave
            };
            procTex.SetPixel(0, 0, Color.white);
            procTex.Apply(false, true);
            mat.mainTexture = procTex;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", procTex);
        }

        ApplyTransparentMode(mat);
        mat.SetColor(colorProp, new Color(0f, 0f, 0f, 0f));

        mr.sharedMaterial = mat;

        return new RuntimeDizzyVillainSilhouette(root, mr, mat, procTex, tintWhite, colorProp);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    private static string GetBaseColorPropertyName(Material mat)
    {
        if (mat.HasProperty("_BaseColor"))
            return "_BaseColor";
        if (mat.HasProperty("_Color"))
            return "_Color";
        return "_BaseColor";
    }

    private static void ApplyTransparentMode(Material mat)
    {
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_AlphaClip"))
            mat.SetFloat("_AlphaClip", 0f);
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
    }

    private static void ApplyOpaqueMode(Material mat)
    {
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);
        mat.renderQueue = (int)RenderQueue.Geometry;
        mat.SetInt("_SrcBlend", (int)BlendMode.One);
        mat.SetInt("_DstBlend", (int)BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
    }

    public void FaceCamera(Vector3 cameraWorldPosition)
    {
        if (_destroyed || _root == null)
            return;

        Vector3 toCam = cameraWorldPosition - _root.transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f)
            return;
        _root.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
    }

    public static float EvaluateAlphaDizzy(float phaseLinear01)
    {
        float t = Mathf.Clamp01(phaseLinear01);
        if (t <= FadeInPhaseStart)
            return 0f;
        if (t >= FadeInPhaseEnd)
            return 1f;
        return Mathf.SmoothStep(0f, 1f, (t - FadeInPhaseStart) / (FadeInPhaseEnd - FadeInPhaseStart));
    }

    public static float EvaluateAlphaFall(float fallU01)
    {
        float u = Mathf.Clamp01(fallU01);
        if (u <= FadeOutFallStart)
            return 1f;
        float t = Mathf.Clamp01((u - FadeOutFallStart) / (1f - FadeOutFallStart));
        // Pow: медленнее в начале затухания, быстрее в конце — без «вязкого» SmoothStep у alpha→0.
        return Mathf.Pow(1f - t, 1.32f);
    }

    public void SetAlphaNormalized(float visible01)
    {
        if (_destroyed || _material == null || _meshRenderer == null)
            return;

        float a = Mathf.Clamp01(visible01) * MaxAlpha;
        if (a < 0.015f)
        {
            _meshRenderer.enabled = false;
            return;
        }

        _meshRenderer.enabled = true;

        if (a >= OpaqueAlphaThreshold)
        {
            if (!_usingOpaque)
            {
                ApplyOpaqueMode(_material);
                _usingOpaque = true;
            }

            if (_tintWhiteForTexture)
                _material.SetColor(_baseColorProperty, Color.white);
            else
                _material.SetColor(_baseColorProperty, Color.black);
        }
        else
        {
            if (_usingOpaque)
            {
                ApplyTransparentMode(_material);
                _usingOpaque = false;
            }

            if (_tintWhiteForTexture)
            {
                Color c = Color.white;
                c.a = a;
                _material.SetColor(_baseColorProperty, c);
            }
            else
            {
                Color c = Color.black;
                c.a = a;
                _material.SetColor(_baseColorProperty, c);
            }
        }
    }

    public void DestroySelf()
    {
        if (_destroyed)
            return;
        _destroyed = true;
        if (_proceduralTexture != null)
            Object.Destroy(_proceduralTexture);
        if (_material != null)
            Object.Destroy(_material);
        if (_root != null)
            Object.Destroy(_root);
    }
}
