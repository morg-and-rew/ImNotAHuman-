using UnityEngine;
using UnityEditor;

/// <summary>
/// Свет из окна: луч рисуется аддитивно поверх сцены и пыли — пылинки визуально проходят сквозь свет и подсвечиваются.
/// Меню: GameObject > Effects > Add Light Shaft (Window). Для антуража добавь также пыль (Add Dust to Scene).
/// </summary>
public static class LightShaftEffectSetup
{
    const string PrefabPath = "Assets/Effects/LightShafts/LightShaft.prefab";
    const string WindowLightPrefabPath = "Assets/Effects/LightShafts/WindowLight.prefab";
    const string MaterialPath = "Assets/Effects/LightShafts/LightShaft.mat";
    const string TexturePath = "Assets/Effects/LightShafts/LightShaftGradient.asset";
    const string FogMaterialPath = "Assets/Effects/LightShafts/LightShaftFog.mat";
    const string FogTexturePath = "Assets/Effects/LightShafts/LightShaftFogTex.asset";
    const string WindowLightParticlesPrefabPath = "Assets/Effects/LightShafts/WindowLightParticles.prefab";
    const string FogParticleMatPath = "Assets/Effects/LightShafts/FogParticle.mat";
    const string FogParticleTexPath = "Assets/Effects/LightShafts/FogParticleTex.asset";
    const string RayParticleMatPath = "Assets/Effects/LightShafts/RayParticle.mat";
    const string RayParticleTexPath = "Assets/Effects/LightShafts/RayParticleTex.asset";

    [MenuItem("Tools/Add Window Light (Particles — туман + лучи)")]
    [MenuItem("GameObject/Effects/Add Window Light (Particles — туман + лучи)")]
    public static void AddWindowLightParticlesToScene()
    {
        EnsureFolderExists();
        var fogMat = GetOrCreateFogParticleMaterial();
        var rayMat = GetOrCreateRayParticleMaterial();
        if (fogMat == null || rayMat == null) return;
        GameObject go;
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WindowLightParticlesPrefabPath);
        if (existing != null)
            go = (GameObject)PrefabUtility.InstantiatePrefab(existing);
        else
        {
            go = CreateWindowLightParticlesObject(fogMat, rayMat);
            PrefabUtility.SaveAsPrefabAsset(go, WindowLightParticlesPrefabPath);
        }
        go.name = "WindowLight_Particles";
        go.transform.position = new Vector3(0.361f, 0.407f, 2.444f);
        go.transform.rotation = Quaternion.Euler(63.73f, 275.45f, -90f);
        go.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(go, "Add Window Light (Particles)");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    [MenuItem("Tools/Add Window Light (Point)")]
    [MenuItem("GameObject/Effects/Add Window Light (Point)")]
    public static void AddWindowLightToScene()
    {
        EnsureFolderExists();
        var shaftMat = GetOrCreateLightShaftMaterial();
        var fogMat = GetOrCreateFogMaterial();
        if (shaftMat == null || fogMat == null) return;
        GameObject lightGo;
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowLightPrefabPath);
        if (existingPrefab != null)
        {
            lightGo = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);
        }
        else
        {
            lightGo = CreateWindowLightObject(shaftMat, fogMat, usePointLight: true);
            PrefabUtility.SaveAsPrefabAsset(lightGo, WindowLightPrefabPath);
        }
        lightGo.name = "WindowLight";
        lightGo.transform.position = new Vector3(0.361f, 0.407f, 2.444f);
        lightGo.transform.rotation = Quaternion.Euler(63.73f, 275.45f, -90f);
        lightGo.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(lightGo, "Add Window Light");
        Selection.activeGameObject = lightGo;
        EditorGUIUtility.PingObject(lightGo);
    }

    [MenuItem("Tools/Add Window Light (Spot)")]
    [MenuItem("GameObject/Effects/Add Window Light (Spot)")]
    public static void AddWindowLightSpotToScene()
    {
        EnsureFolderExists();
        var shaftMat = GetOrCreateLightShaftMaterial();
        var fogMat = GetOrCreateFogMaterial();
        if (shaftMat == null || fogMat == null) return;
        var lightGo = CreateWindowLightObject(shaftMat, fogMat, usePointLight: false);
        lightGo.name = "WindowLight_Spot";
        lightGo.transform.position = new Vector3(0.361f, 0.407f, 2.444f);
        lightGo.transform.rotation = Quaternion.Euler(63.73f, 275.45f, -90f);
        lightGo.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(lightGo, "Add Window Light (Spot)");
        Selection.activeGameObject = lightGo;
        EditorGUIUtility.PingObject(lightGo);
    }

    [MenuItem("Tools/Add Light Shaft (Window)")]
    [MenuItem("GameObject/Effects/Add Light Shaft (Window)")]
    public static void AddLightShaftToScene()
    {
        AddLightShaftToSceneInternal();
    }

    /// <summary>
    /// Пыль + луч из окна: пылинки проходят сквозь свет, антураж в одну кнопку.
    /// </summary>
    [MenuItem("GameObject/Effects/Add Dust + Light Shaft (atmospheric)")]
    public static void AddDustAndLightShaft()
    {
        DustEffectSetup.AddDustToScene();
        AddLightShaftToSceneInternal();
    }

    static void AddLightShaftToSceneInternal()
    {
        EnsureFolderExists();
        var mat = GetOrCreateLightShaftMaterial();
        if (mat == null) return;

        GameObject shaftGo;
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            shaftGo = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);
        }
        else
        {
            shaftGo = CreateLightShaftObject(mat);
            SaveAsPrefab(shaftGo);
        }

        shaftGo.name = "LightShaft_Window";
        // Позиция/поворот под окно: луч идёт в комнату, как на твоей сцене
        shaftGo.transform.position = new Vector3(0.361f, 0.407f, 2.444f);
        shaftGo.transform.rotation = Quaternion.Euler(63.73f, 95.45f, -90f);
        shaftGo.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
        Undo.RegisterCreatedObjectUndo(shaftGo, "Add Light Shaft");
        Selection.activeGameObject = shaftGo;
        EditorGUIUtility.PingObject(shaftGo);
    }

    static void EnsureFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Effects")) AssetDatabase.CreateFolder("Assets", "Effects");
        if (!AssetDatabase.IsValidFolder("Assets/Effects/LightShafts")) AssetDatabase.CreateFolder("Assets/Effects", "LightShafts");
    }

    static Material GetOrCreateLightShaftMaterial()
    {
        var tex = GetOrCreateGradientTexture();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                Debug.LogError("LightShaftEffectSetup: Could not find URP Unlit shader.");
                return null;
            }
            mat = new Material(shader);
            mat.name = "LightShaft";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 2);   // Additive — свет «добавляется» поверх пыли, пылинки подсвечиваются
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetFloat("_Cull", 0f); // Cull Off — видно с обеих сторон, не пропадает при смене угла камеры
        mat.renderQueue = 3010;
        mat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.72f, 0.45f)); // лучи хорошо видны, не только точка света
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Texture2D GetOrCreateGradientTexture()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (tex != null) return tex;
        tex = CreateGradientTexture();
        if (tex != null)
        {
            AssetDatabase.CreateAsset(tex, TexturePath);
            AssetDatabase.SaveAssets();
        }
        return tex;
    }

    /// <summary>
    /// Свет по всей поверхности окна: полосы как раньше, но «тени» от ламелей светлые — окно целиком освещено.
    /// </summary>
    static Texture2D CreateGradientTexture()
    {
        int w = 256;
        int h = 256;
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        const int stripePeriod = 14;
        const int lightRows = 8;
        const float soft = 1.5f;
        const float minInShadow = 0.6f;
        const float maxInGap = 1f;
        const float maxAtWindow = 0.75f; // плотнее луч — видно сам луч, а не только пятно

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float t = x / (float)(w - 1);
            float depthFade = Mathf.Lerp(maxAtWindow, 0f, t * t);

            int phase = y % stripePeriod;
            float inLight;
            if (phase < soft)
                inLight = Mathf.SmoothStep(0f, 1f, phase / soft);
            else if (phase < lightRows)
                inLight = 1f;
            else if (phase < lightRows + soft)
                inLight = 1f - Mathf.SmoothStep(0f, 1f, (phase - lightRows) / soft);
            else
                inLight = 0f;
            float alpha = depthFade * Mathf.Lerp(minInShadow, maxInGap, inLight);
            pixels[y * w + x] = new Color(1f, 0.98f, 0.95f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.alphaIsTransparency = true;
        return tex;
    }

    static Material GetOrCreateFogMaterial()
    {
        var tex = GetOrCreateFogTexture();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) return null;
            mat = new Material(shader);
            mat.name = "LightShaftFog";
            AssetDatabase.CreateAsset(mat, FogMaterialPath);
        }
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 2);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_Cull", 0f);
        mat.renderQueue = 3008;
        mat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.8f, 0.4f)); // туман сквозь лучи хорошо виден
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Texture2D GetOrCreateFogTexture()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FogTexturePath);
        if (tex != null) return tex;
        tex = CreateFogTexture();
        if (tex != null)
        {
            AssetDatabase.CreateAsset(tex, FogTexturePath);
            AssetDatabase.SaveAssets();
        }
        return tex;
    }

    static Texture2D CreateFogTexture()
    {
        int w = 256;
        int h = 256;
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float t = x / (float)(w - 1);
            float alpha = Mathf.Lerp(0.55f, 0f, t * t * t); // плотнее туман — видно сквозь лучи
            float v = (y / (float)(h - 1) - 0.5f) * 2f;
            alpha *= 1f - 0.12f * v * v;
            pixels[y * w + x] = new Color(1f, 0.98f, 0.95f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.alphaIsTransparency = true;
        return tex;
    }

    static Material GetOrCreateFogParticleMaterial()
    {
        var tex = GetOrCreateFogParticleTexture();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FogParticleMatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            mat = new Material(shader);
            mat.name = "FogParticle";
            AssetDatabase.CreateAsset(mat, FogParticleMatPath);
        }
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 2);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.SetColor("_BaseColor", new Color(1f, 0.94f, 0.82f, 0.28f));
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Texture2D GetOrCreateFogParticleTexture()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FogParticleTexPath);
        if (tex != null) return tex;
        tex = CreateSoftParticleTexture(64);
        if (tex != null) { AssetDatabase.CreateAsset(tex, FogParticleTexPath); AssetDatabase.SaveAssets(); }
        return tex;
    }

    static Texture2D CreateSoftParticleTexture(int size)
    {
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - c) / c, dy = (y - c) / c;
            float a = Mathf.Exp(-(dx * dx + dy * dy) * 4f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.alphaIsTransparency = true;
        return tex;
    }

    static Material GetOrCreateRayParticleMaterial()
    {
        var tex = GetOrCreateRayParticleTexture();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RayParticleMatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            mat = new Material(shader);
            mat.name = "RayParticle";
            AssetDatabase.CreateAsset(mat, RayParticleMatPath);
        }
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 2);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0);
        mat.renderQueue = 3010;
        mat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.72f, 0.32f));
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Texture2D GetOrCreateRayParticleTexture()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RayParticleTexPath);
        if (tex != null) return tex;
        tex = CreateRayStripeTexture();
        if (tex != null) { AssetDatabase.CreateAsset(tex, RayParticleTexPath); AssetDatabase.SaveAssets(); }
        return tex;
    }

    static Texture2D CreateRayStripeTexture()
    {
        int w = 32;
        int h = 128;
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        float centerX = (w - 1) * 0.5f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float t = Mathf.Abs((x - centerX) / centerX);
            float alpha = Mathf.Exp(-t * t * 4f);
            pixels[y * w + x] = new Color(1f, 0.98f, 0.95f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.alphaIsTransparency = true;
        return tex;
    }

    static GameObject CreateWindowLightParticlesObject(Material fogParticleMat, Material rayParticleMat)
    {
        var root = new GameObject("WindowLight_Particles");
        root.AddComponent<DayCycleSunLight>();

        var light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.9f, 0.72f);
        light.intensity = 0.95f;
        light.range = 12f;
        light.shadows = LightShadows.Soft;

        var fogGo = new GameObject("FogParticles");
        fogGo.transform.SetParent(root.transform, false);
        CreateFogParticleSystem(fogGo, fogParticleMat);

        var rayGo = new GameObject("RayParticles");
        rayGo.transform.SetParent(root.transform, false);
        CreateRayParticleSystem(rayGo, rayParticleMat);

        return root;
    }

    static void CreateFogParticleSystem(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 9999f;
        main.loop = true;
        main.startLifetime = 8f;
        main.startSpeed = 0.008f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new Color(1f, 0.94f, 0.82f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 600;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 35f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(5f, 7f, 4f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.005f, 0.02f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strengthX = 0.008f;
        noise.strengthY = 0.006f;
        noise.strengthZ = 0.008f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.separateAxes = true;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0.2f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
    }

    static void CreateRayParticleSystem(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 9999f;
        main.loop = true;
        main.startLifetime = 10f;
        main.startSpeed = 0.005f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new Color(1f, 0.9f, 0.72f, 0.28f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 350;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4.5f, 6.5f, 0.3f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.z = new ParticleSystem.MinMaxCurve(0.002f, 0.008f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
    }

    static GameObject CreateLightShaftObject(Material material)
    {
        var root = new GameObject("LightShaft_Window");
        root.AddComponent<DayCycleSunLight>();

        const float height = 7f;
        const float width = 5f;
        CreateShaftQuad(root.transform, material, new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(width, height, 0.1f));
        CreateShaftQuad(root.transform, material, new Vector3(0.15f, 0, 0), new Vector3(0, 0, 2f), new Vector3(width * 0.95f, height * 0.95f, 0.1f));
        CreateShaftQuad(root.transform, material, new Vector3(-0.1f, 0.05f, 0), new Vector3(0, 0, -2f), new Vector3(width * 0.9f, height * 0.9f, 0.1f));

        return root;
    }

    static GameObject CreateWindowLightObject(Material shaftMaterial, Material fogMaterial, bool usePointLight = true)
    {
        var root = new GameObject("WindowLight");
        root.AddComponent<DayCycleSunLight>();

        var light = root.AddComponent<Light>();
        if (usePointLight)
        {
            light.type = LightType.Point;
            light.color = new Color(1f, 0.9f, 0.72f);
            light.intensity = 4f;
            light.range = 12f;
            light.shadows = LightShadows.Soft;
        }
        else
        {
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.9f, 0.72f);
            light.intensity = 2f;
            light.range = 14f;
            light.spotAngle = 150f;
            light.innerSpotAngle = 120f;
            light.shadows = LightShadows.Soft;
        }

        const float height = 7f;
        const float width = 5f;
        CreateShaftQuad(root.transform, shaftMaterial, new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(width, height, 0.1f));
        CreateShaftQuad(root.transform, shaftMaterial, new Vector3(0.15f, 0, 0), new Vector3(0, 0, 2f), new Vector3(width * 0.95f, height * 0.95f, 0.1f));
        CreateShaftQuad(root.transform, shaftMaterial, new Vector3(-0.1f, 0.05f, 0), new Vector3(0, 0, -2f), new Vector3(width * 0.9f, height * 0.9f, 0.1f));

        CreateShaftQuad(root.transform, fogMaterial, new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0), new Vector3(width * 1.15f, height * 1.15f, 0.1f));
        CreateShaftQuad(root.transform, fogMaterial, new Vector3(0.1f, 0, -0.08f), new Vector3(0, 0, 3f), new Vector3(width * 1.05f, height * 1.05f, 0.1f));

        return root;
    }

    static void CreateShaftQuad(Transform parent, Material material, Vector3 localPos, Vector3 localEuler, Vector3 scale)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ShaftQuad";
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPos;
        quad.transform.localEulerAngles = localEuler;
        quad.transform.localScale = scale;
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var col = quad.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    static void SaveAsPrefab(GameObject go)
    {
        EnsureFolderExists();
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
    }

    [MenuItem("Tools/Refresh Light Shaft texture (blinds)")]
    public static void RefreshLightShaftTexture()
    {
        EnsureFolderExists();
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) != null)
            AssetDatabase.DeleteAsset(TexturePath);
        var tex = CreateGradientTexture();
        if (tex != null)
        {
            AssetDatabase.CreateAsset(tex, TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) { mat.SetTexture("_BaseMap", tex); EditorUtility.SetDirty(mat); }
            AssetDatabase.SaveAssets();
            Debug.Log("Light shaft texture recreated (blinds style). Re-add Light Shaft to scene to see.");
        }
    }

    [MenuItem("Tools/Сделать лучи и туман заметнее (применить к материалам)")]
    public static void MakeRaysAndFogMoreVisible()
    {
        EnsureFolderExists();
        var shaftMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var fogMat = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (shaftMat != null)
        {
            shaftMat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.72f, 0.45f));
            EditorUtility.SetDirty(shaftMat);
        }
        if (fogMat != null)
        {
            fogMat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.8f, 0.4f));
            EditorUtility.SetDirty(fogMat);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Готово. Лучи и туман должны быть заметнее. Если используешь Day Cycle Sun Light — перезапусти сцену (Play), там альфа цветов тоже увеличена.");
    }

    [MenuItem("Tools/Включить освещение в Scene view (сделать сейчас)")]
    public static void EnableSceneViewLighting()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.sceneLighting = true;
            sv.Repaint();
            Debug.Log("Освещение в окне Scene включено. Если не видно — переключись на окно Scene и подожди кадр.");
        }
        else
            EditorUtility.DisplayDialog("Scene view", "Сначала открой окно Scene (вкладка Scene).", "Ок");
    }

    [MenuItem("Tools/Подсказка: как включить освещение в Scene view")]
    public static void ShowLightingHint()
    {
        EditorUtility.DisplayDialog(
            "Как включить свет в окне Scene",
            "1. Кликни по окну Scene (чтобы оно было в фокусе).\n\n" +
            "2. Вверху окна Scene найди выпадающий список с надписью «Shaded» или «Режим отрисовки» (рядом с кнопками перемещения/поворота/масштаба).\n\n" +
            "3. Нажми на этот список — откроется меню с пунктами вроде Shaded, Wireframe и галочками.\n\n" +
            "4. Включи галочку «Lighting» (или «Scene Lighting»). Если есть «Skybox» — тоже можно включить.\n\n" +
            "После этого в Scene будет виден свет от Window Light.\n\n" +
            "Если такого списка нет: нажми на три точки (⋮) вверху справа в окне Scene и ищи пункт про освещение (Lighting) там.",
            "Понятно");
    }
}
