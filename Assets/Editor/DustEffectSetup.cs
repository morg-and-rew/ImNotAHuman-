using UnityEngine;
using UnityEditor;

/// <summary>
/// Эффект пыли в воздухе (видимые в свету пылинки).
/// Меню: Tools > Add Dust Effect to Scene  или  GameObject > Effects > Add Dust to Scene
/// </summary>
public static class DustEffectSetup
{
    const string PrefabPath = "Assets/Effects/Dust/DustEffect.prefab";
    const string MaterialPath = "Assets/Effects/Dust/DustParticle.mat";
    const string TexturePath = "Assets/Effects/Dust/DustParticleTexture.asset";

    [MenuItem("Tools/Add Dust Effect to Scene")]
    [MenuItem("GameObject/Effects/Add Dust to Scene")]
    public static void AddDustToScene()
    {
        var mat = GetOrCreateDustMaterial();
        if (mat == null) return;

        GameObject dustGo;
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            dustGo = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);
        }
        else
        {
            dustGo = CreateDustGameObject(mat);
            SaveAsPrefab(dustGo);
        }

        dustGo.name = "DustEffect";
        dustGo.transform.position = new Vector3(0f, 1.5f, 0f);
        Undo.RegisterCreatedObjectUndo(dustGo, "Add Dust Effect");
        Selection.activeGameObject = dustGo;
        EditorGUIUtility.PingObject(dustGo);
    }

    [MenuItem("Tools/Create Dust Effect Prefab Only")]
    public static void CreatePrefabOnly()
    {
        var mat = GetOrCreateDustMaterial();
        if (mat == null) return;
        var dustGo = CreateDustGameObject(mat);
        SaveAsPrefab(dustGo);
        Object.DestroyImmediate(dustGo);
        AssetDatabase.Refresh();
        Debug.Log("Dust effect prefab saved to " + PrefabPath);
    }

    static Material GetOrCreateDustMaterial()
    {
        EnsureDustFolderExists();
        var tex = GetOrCreateDustTexture();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                Debug.LogError("DustEffectSetup: Could not find URP Particles/Unlit shader.");
                return null;
            }
            mat = new Material(shader);
            mat.name = "DustParticle";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetColor("_BaseColor", new Color(1f, 0.98f, 0.95f, 0.4f));
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_ColorMode", 0);
        mat.renderQueue = 3000;
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void EnsureDustFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Effects")) AssetDatabase.CreateFolder("Assets", "Effects");
        if (!AssetDatabase.IsValidFolder("Assets/Effects/Dust")) AssetDatabase.CreateFolder("Assets/Effects", "Dust");
    }

    static Texture2D GetOrCreateDustTexture()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (tex != null) return tex;
        tex = CreateSoftDustTexture();
        if (tex != null)
        {
            AssetDatabase.CreateAsset(tex, TexturePath);
            AssetDatabase.SaveAssets();
        }
        return tex;
    }

    static Texture2D CreateSoftDustTexture()
    {
        int size = 64;
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - center) / center;
            float dy = (y - center) / center;
            float rSq = dx * dx + dy * dy;
            float d = Mathf.Exp(-rSq * 4f);
            float noise = (Mathf.PerlinNoise(x * 0.5f, y * 0.5f) - 0.4f) * 0.15f;
            d = Mathf.Clamp01(d + noise);
            pixels[y * size + x] = new Color(1f, 1f, 1f, d);
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.alphaIsTransparency = true;
        return tex;
    }

    static GameObject CreateDustGameObject(Material material)
    {
        var go = new GameObject("DustEffect");
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 9999f;
        main.loop = true;
        main.startLifetime = 6f;
        main.startSpeed = 0.006f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(1f, 0.98f, 0.95f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 180f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 4f, 10f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.005f, 0.005f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.002f, 0.006f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.005f, 0.005f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strengthX = 0.008f;
        noise.strengthY = 0.006f;
        noise.strengthZ = 0.008f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.3f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.separateAxes = true;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 1f),
            new Keyframe(0.85f, 1f),
            new Keyframe(1f, 0f)
        ));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            new Gradient
            {
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.6f, 0.2f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                },
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }
            }
        );

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sortingFudge = 0.5f;

        return go;
    }

    static void SaveAsPrefab(GameObject go)
    {
        EnsureDustFolderExists();
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
    }

    [MenuItem("Tools/Refresh Dust Texture (fix squares)")]
    public static void RefreshDustTexture()
    {
        EnsureDustFolderExists();
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) != null)
            AssetDatabase.DeleteAsset(TexturePath);
        var tex = CreateSoftDustTexture();
        if (tex != null)
        {
            AssetDatabase.CreateAsset(tex, TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null)
            {
                mat.SetTexture("_BaseMap", tex);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Dust texture recreated.");
        }
    }
}
