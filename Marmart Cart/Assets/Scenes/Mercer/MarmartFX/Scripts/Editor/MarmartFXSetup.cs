using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// MarmartFX test-scene setup. Everything it creates lives in Assets/Scenes/Mercer/MarmartFX,
/// and it only edits FX_TestScene (a copy of Gameplay Scene 4P). Original materials, prefabs,
/// scenes and project settings are only read, never written.
///
/// Menu: Tools > MarmartFX
/// One-shot auto run: drop AUTO_SETUP.txt into the MarmartFX folder.
/// </summary>
[InitializeOnLoad]
public static class MarmartFXSetup
{
    public const string Root = "Assets/Scenes/Mercer/MarmartFX";
    public const string ScenePath = Root + "/FX_TestScene.unity";
    const string MatFolder = Root + "/Materials/ToyPlastic";
    const string SettingsFolder = Root + "/Settings";
    const string VolumePath = SettingsFolder + "/MarmartFX_ToyPlastic_Volume.asset";
    const string ShaderName = "MarmartFX/ToyPlastic";
    const string MarkerPath = Root + "/AUTO_SETUP.txt";
    const string LightingRootName = "-------MarmartFX Lighting--------";
    const string SwapName = "MarmartFX Toy Plastic Swap";
    public const string StylesFolder = SettingsFolder + "/Styles";
    const string FXRendererPath = SettingsFolder + "/FX_Renderer.asset";
    const string PCRendererPath = "Assets/Settings/PC_Renderer.asset";

    /// <summary>Same order as the old HadesStyle panel: A, Toy Plastic, 8 art styles.</summary>
    public static readonly string[] StyleFiles =
    {
        "Version_A.asset", "Party_2_ToyPlastic.asset",
        "Art_HandDrawn.asset", "Art_OilPaint.asset", "Art_Watercolor.asset", "Art_Scribble.asset",
        "Art_RetroFilm.asset", "Art_Pixel.asset", "Art_RetroPoster.asset", "Art_NeonArcade.asset",
    };

    public static MarmartFXStylePreset LoadStyle(int i)
    {
        i = Mathf.Clamp(i, 0, StyleFiles.Length - 1);
        string path = StylesFolder + "/" + StyleFiles[i];
        var p = AssetDatabase.LoadAssetAtPath<MarmartFXStylePreset>(path);
        if (p == null)
        {
            EnsureFolder(StylesFolder);
            p = ScriptableObject.CreateInstance<MarmartFXStylePreset>();
            p.ResetToVersion(i);
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
        }
        return p;
    }

    public static int ActiveStyleIndex
    {
        get => Mathf.Clamp(MarmartFXControlPanel.GetOrCreatePreset().activeStyle, 0, StyleFiles.Length - 1);
        set
        {
            var p = MarmartFXControlPanel.GetOrCreatePreset();
            p.activeStyle = Mathf.Clamp(value, 0, StyleFiles.Length - 1);
            EditorUtility.SetDirty(p);
        }
    }

    public static MarmartFXStylePreset ActiveStyle => LoadStyle(ActiveStyleIndex);

    static readonly string[] PrefabSearchFolders = { "Assets/Prefabs", "Assets/Cart", "Assets/GameObject Pools" };

    static readonly HashSet<string> ConvertibleShaders = new HashSet<string>
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Complex Lit",
        "Universal Render Pipeline/Baked Lit",
        "Standard",
    };

    public const string VFXSettingsPath = SettingsFolder + "/MarmartFX_VFX.asset";
    const string VFXMarker = Root + "/AUTO_VFX.txt";
    const string VFXObjectName = "MarmartFX VFX (特效)";

    static MarmartFXSetup()
    {
        EditorApplication.delayCall += TryAutoRun;
        EditorApplication.delayCall += TryAutoVFX;
    }

    static void TryAutoVFX()
    {
        if (!File.Exists(VFXMarker)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoVFX;
            return;
        }
        if (EditorSceneManager.GetActiveScene().path != ScenePath) return;
        File.Delete(VFXMarker);
        if (File.Exists(VFXMarker + ".meta")) File.Delete(VFXMarker + ".meta");
        VFXMenu();
    }

    public static MarmartFXVFXSettings GetOrCreateVFXSettings()
    {
        var s = AssetDatabase.LoadAssetAtPath<MarmartFXVFXSettings>(VFXSettingsPath);
        if (s != null) return s;
        EnsureFolder(SettingsFolder);
        s = ScriptableObject.CreateInstance<MarmartFXVFXSettings>();
        AssetDatabase.CreateAsset(s, VFXSettingsPath);
        AssetDatabase.SaveAssets();
        return s;
    }

    [MenuItem("Tools/MarmartFX/设置特效 (撞货架 / 轨迹 / 高亮 / 吃车)")]
    public static void VFXMenu()
    {
        if (!EnsureTestScene()) return;
        SetupVFX();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static GameObject SetupVFX()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var settings = GetOrCreateVFXSettings();
        var go = FindRoot(scene, VFXObjectName);
        if (go == null)
        {
            go = new GameObject(VFXObjectName);
            Undo.RegisterCreatedObjectUndo(go, "MarmartFX VFX");
        }
        var spill = go.GetComponent<MarmartFXShelfSpill>() ?? go.AddComponent<MarmartFXShelfSpill>();
        spill.settings = settings;
        var fx = go.GetComponent<MarmartFXPlayerFX>() ?? go.AddComponent<MarmartFXPlayerFX>();
        fx.settings = settings;
        fx.additiveShader = Shader.Find("MarmartFX/VFX/Additive");
        fx.highlightShader = Shader.Find("MarmartFX/VFX/PlayerHighlight");
        EditorUtility.SetDirty(spill);
        EditorUtility.SetDirty(fx);
        Debug.Log("[MarmartFX] 特效已加入 FX_TestScene：撞货架掉商品、加速轨迹、玩家高亮、吃车特效。");
        return go;
    }

    static void TryAutoRun()
    {
        if (!File.Exists(MarkerPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoRun;
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("[MarmartFX] AUTO_SETUP.txt found, but FX_TestScene is not the active scene. Open it and use Tools > MarmartFX > 全部设置.");
            return;
        }
        File.Delete(MarkerPath);
        if (File.Exists(MarkerPath + ".meta")) File.Delete(MarkerPath + ".meta");
        SetupAll();
    }

    [MenuItem("Tools/MarmartFX/全部设置 (材质 + 超市灯光 + FX_Renderer)")]
    public static void SetupAll()
    {
        if (!EnsureTestScene()) return;
        ApplyToyPlastic();
        SetupSupermarketLighting();
        SetupRenderer();
        SetupVFX();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MarmartFX] 完成：塑料包装材质 + 超市灯光已应用到 FX_TestScene。Play 时按 F8 切换原版/塑料包装。");
    }

    static bool EnsureTestScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path == ScenePath) return true;
        EditorUtility.DisplayDialog("MarmartFX", "只能在 FX_TestScene 里运行（不会修改其他场景）。\n请先打开 " + ScenePath, "OK");
        return false;
    }

    // ------------------------------------------------------------------
    // Toy Plastic materials
    // ------------------------------------------------------------------

    [MenuItem("Tools/MarmartFX/只应用塑料包装材质")]
    public static void ApplyToyPlasticMenu()
    {
        if (!EnsureTestScene()) return;
        ApplyToyPlastic();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void ApplyToyPlastic()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[MarmartFX] 找不到 shader " + ShaderName + "，请检查 Console 里的 shader 编译错误。");
            return;
        }
        EnsureFolder(MatFolder);

        // 1. Collect source materials: scene renderers + prefabs that can be spawned at runtime (read only).
        var sources = new HashSet<Material>();
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                CollectMaterials(r, sources);

        // Originals already swapped out of the scene on a previous run (keeps F8 / 原版 toggle working).
        var existingSwap = FindRoot(scene, SwapName)?.GetComponent<MarmartFXMaterialSwap>();
        if (existingSwap != null)
            foreach (var m in existingSwap.sourceMaterials)
                if (m != null) sources.Add(m);

        var folders = PrefabSearchFolders.Where(AssetDatabase.IsValidFolder).ToArray();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                CollectMaterials(r, sources);
        }

        // 2. Create / refresh Toy Plastic copies.
        var map = new Dictionary<Material, Material>();
        foreach (var src in sources)
        {
            if (!IsConvertible(src)) continue;
            map[src] = GetOrCreateToy(src, shader);
        }
        AssetDatabase.SaveAssets();

        // 3. Assign in this scene (scene overrides only; prefab assets untouched).
        int swapped = 0;
        foreach (var go in scene.GetRootGameObjects())
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && map.TryGetValue(mats[i], out var toy))
                    {
                        mats[i] = toy;
                        changed = true;
                    }
                }
                if (changed)
                {
                    Undo.RecordObject(r, "MarmartFX Toy Plastic");
                    r.sharedMaterials = mats;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    swapped++;
                }
            }
        }

        // 4. Runtime swapper for spawned carts / items + F8 compare.
        var swapGo = FindRoot(scene, SwapName) ?? new GameObject(SwapName);
        var swap = swapGo.GetComponent<MarmartFXMaterialSwap>() ?? swapGo.AddComponent<MarmartFXMaterialSwap>();
        swap.sourceMaterials = map.Keys.ToList();
        swap.toyMaterials = map.Keys.Select(k => map[k]).ToList();
        swap.useToyPlastic = true;
        EditorUtility.SetDirty(swap);

        Debug.Log($"[MarmartFX] 塑料包装：生成/更新 {map.Count} 个材质，场景里替换了 {swapped} 个 Renderer。");
    }

    static void CollectMaterials(Renderer r, HashSet<Material> into)
    {
        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer) return;
        foreach (var m in r.sharedMaterials)
            if (m != null) into.Add(m);
    }

    static bool IsConvertible(Material m)
    {
        if (m == null || m.shader == null) return false;
        if (!ConvertibleShaders.Contains(m.shader.name)) return false;
        var path = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(path) || path.StartsWith(Root)) return false;
        // Skip transparent materials (glass, decals...)
        if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) return false;
        if (m.HasProperty("_Mode") && m.shader.name == "Standard" && m.GetFloat("_Mode") >= 2f) return false;
        return true;
    }

    static Material GetOrCreateToy(Material src, Shader shader)
    {
        string srcGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
        string safe = string.Join("_", src.name.Split(Path.GetInvalidFileNameChars()));
        string path = $"{MatFolder}/{safe}_Toy_{srcGuid.Substring(0, 6)}.mat";

        var toy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (toy == null)
        {
            toy = new Material(shader);
            AssetDatabase.CreateAsset(toy, path);
        }
        toy.shader = shader;

        // Base texture / colour from the original
        Texture baseTex = Prop<Texture>(src, "_BaseMap") ?? Prop<Texture>(src, "_MainTex");
        toy.SetTexture("_BaseMap", baseTex);
        string stName = src.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        if (src.HasProperty(stName))
        {
            toy.SetTextureScale("_BaseMap", src.GetTextureScale(stName));
            toy.SetTextureOffset("_BaseMap", src.GetTextureOffset(stName));
        }
        Color baseCol = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
        toy.SetColor("_BaseColor", baseCol);

        bool clipOn = (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) || src.IsKeywordEnabled("_ALPHATEST_ON");
        toy.SetFloat("_AlphaClip", clipOn ? 1 : 0);
        if (clipOn) toy.EnableKeyword("_ALPHATEST_ON"); else toy.DisableKeyword("_ALPHATEST_ON");
        if (src.HasProperty("_Cutoff")) toy.SetFloat("_Cutoff", src.GetFloat("_Cutoff"));

        bool emissive = src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor");
        toy.SetTexture("_EmissionMap", emissive ? Prop<Texture>(src, "_EmissionMap") : null);
        toy.SetColor("_EmissionColor", emissive ? src.GetColor("_EmissionColor") : Color.black);

        ApplyMercerToyPreset(toy);
        EditorUtility.SetDirty(toy);
        return toy;
    }

    static T Prop<T>(Material m, string name) where T : Texture
    {
        return m.HasProperty(name) ? m.GetTexture(name) as T : null;
    }

    /// <summary>Mercer's tuned values from HadesStyle preset "派对2 · 塑料包装".</summary>
    public static void ApplyMercerToyPreset(Material m)
    {
        ActiveStyle.ApplyToMaterial(m);
    }

    // ------------------------------------------------------------------
    // Supermarket lighting
    // ------------------------------------------------------------------

    [MenuItem("Tools/MarmartFX/只设置超市灯光")]
    public static void LightingMenu()
    {
        if (!EnsureTestScene()) return;
        SetupSupermarketLighting();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SetupSupermarketLighting()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Rebuild our own lighting root each run.
        var old = FindRoot(scene, LightingRootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(LightingRootName);

        // Turn off the copied scene's original directional light (only in this test scene).
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != scene || l.type != LightType.Directional) continue;
            if (l.transform.IsChildOf(root.transform)) continue;
            Undo.RecordObject(l.gameObject, "MarmartFX lighting");
            l.gameObject.SetActive(false);
        }

        // Level bounds
        Bounds b = ComputeLevelBounds(scene);

        // Main light: cool white "daylight fluorescent" from the preset (0.94,0.98,1) x1.35, steep from above.
        var sunGo = new GameObject("MarmartFX Main Light (冷白日光灯)");
        sunGo.transform.SetParent(root.transform);
        sunGo.transform.rotation = Quaternion.Euler(68f, -35f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.94f, 0.98f, 1f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.55f;
        sun.lightmapBakeType = LightmapBakeType.Realtime;
        RenderSettings.sun = sun;

        // Ceiling fluorescent grid: soft cool point lights over the whole store.
        var gridGo = new GameObject("Ceiling Fluorescent Grid (天花板灯阵)");
        gridGo.transform.SetParent(root.transform);
        float spacing = 9f;
        int nx = Mathf.Max(1, Mathf.CeilToInt(b.size.x / spacing));
        int nz = Mathf.Max(1, Mathf.CeilToInt(b.size.z / spacing));
        while (nx * nz > 120) { spacing *= 1.25f; nx = Mathf.CeilToInt(b.size.x / spacing); nz = Mathf.CeilToInt(b.size.z / spacing); }
        float y = b.min.y + 6.5f;
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            var p = new Vector3(b.min.x + (ix + 0.5f) * b.size.x / nx, y, b.min.z + (iz + 0.5f) * b.size.z / nz);
            var lg = new GameObject($"Tube {ix}_{iz}");
            lg.transform.SetParent(gridGo.transform);
            lg.transform.position = p;
            var pl = lg.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.9f, 0.96f, 1f);
            pl.intensity = 1.6f;
            pl.range = spacing * 1.6f;
            pl.shadows = LightShadows.None;
            pl.lightmapBakeType = LightmapBakeType.Realtime;
        }

        // Warm accent lights over produce / bakery areas would go here later; keep it cool & bright for now.

        // Bright, even supermarket ambient (no bake needed).
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.84f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.72f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.35f, 0.36f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;

        // Reflection probe so the shiny plastic / floor reflect the store.
        var probeGo = new GameObject("Store Reflection Probe");
        probeGo.transform.SetParent(root.transform);
        probeGo.transform.position = new Vector3(b.center.x, b.min.y + 3f, b.center.z);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.size = new Vector3(b.size.x + 4f, 12f, b.size.z + 4f);
        probe.boxProjection = true;
        probe.resolution = 256;
        probe.intensity = 1f;

        // Post-processing volume with Mercer's preset grading.
        var volGo = new GameObject("MarmartFX Post Volume (调色)");
        volGo.transform.SetParent(root.transform);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100;
        vol.sharedProfile = GetOrCreateVolumeProfile();

        // Cameras in this test scene need post processing on (scene copy only).
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.gameObject.scene != scene) continue;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) continue;
            Undo.RecordObject(data, "MarmartFX post");
            data.renderPostProcessing = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
        }

        // Use the control-panel preset values (defaults = Mercer's Toy Plastic preset).
        var preset = MarmartFXControlPanel.GetOrCreatePreset();
        preset.ApplyLighting(root);
        var style = ActiveStyle;
        style.ApplyToLight(sun);
        style.ApplyToProfile(vol.sharedProfile);
        EditorUtility.SetDirty(vol.sharedProfile);

        Debug.Log($"[MarmartFX] 超市灯光：主光 + {nx * nz} 盏天花板灯，范围 {b.size.x:0}x{b.size.z:0}m。");
    }

    static Bounds ComputeLevelBounds(Scene scene)
    {
        bool has = false;
        Bounds b = new Bounds();
        var levelRoot = FindRoot(scene, "Game Level");
        IEnumerable<Renderer> rs = levelRoot != null
            ? levelRoot.GetComponentsInChildren<Renderer>(false)
            : scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false));
        foreach (var r in rs)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.name.Contains("Ground Plane") || r.bounds.size.x > 500f) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (!has) b = new Bounds(Vector3.zero, new Vector3(60, 4, 60));
        return b;
    }

    static VolumeProfile GetOrCreateVolumeProfile()
    {
        EnsureFolder(SettingsFolder);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
        }

        var tone = GetOrAdd<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.Neutral);

        var ca = GetOrAdd<ColorAdjustments>(profile);
        ca.postExposure.Override(-0.08f);
        ca.contrast.Override(15f);
        ca.saturation.Override(25f);
        ca.colorFilter.Override(new Color(0.97f, 1f, 1f));

        var st = GetOrAdd<SplitToning>(profile);
        st.shadows.Override(new Color(0.45f, 0.55f, 0.75f));
        st.highlights.Override(new Color(1f, 0.97f, 0.9f));
        st.balance.Override(0f);

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.8f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(Color.white);

        var vig = GetOrAdd<Vignette>(profile);
        vig.color.Override(new Color(0.05f, 0.08f, 0.15f));
        vig.intensity.Override(0.2f);
        vig.smoothness.Override(0.5f);

        var fx = GetOrAdd<MarmartFXPostFX>(profile);
        fx.intensity.Override(0f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet<T>(out var c)) return c;
        c = p.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Separate renderer for FX_TestScene (PC_Renderer is never modified)
    // ------------------------------------------------------------------

    [MenuItem("Tools/MarmartFX/设置 FX_Renderer (全屏滤镜)")]
    public static void RendererMenu()
    {
        if (!EnsureTestScene()) return;
        SetupRenderer();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Copies PC_Renderer to MarmartFX/Settings/FX_Renderer, adds the MarmartFX PostFX feature to
    /// the copy only, appends the copy to the active URP asset's renderer list (default index is
    /// unchanged so every other scene renders exactly as before) and points FX_TestScene cameras at it.
    /// </summary>
    public static int SetupRenderer()
    {
        EnsureFolder(SettingsFolder);
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(FXRendererPath);
        if (data == null)
        {
            if (!AssetDatabase.CopyAsset(PCRendererPath, FXRendererPath))
            {
                Debug.LogError("[MarmartFX] 复制 PC_Renderer 失败。");
                return -1;
            }
            data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(FXRendererPath);
            data.name = "FX_Renderer";
        }

        if (!data.rendererFeatures.Any(f => f is MarmartFXPostFXFeature))
        {
            var feat = ScriptableObject.CreateInstance<MarmartFXPostFXFeature>();
            feat.name = "MarmartFX PostFX";
            feat.shader = Shader.Find("Hidden/MarmartFX/PostFX");
            AssetDatabase.AddObjectToAsset(feat, data);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out string _, out long localId);

            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        var rp = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        if (rp == null)
        {
            Debug.LogError("[MarmartFX] 当前没有使用 URP Asset。");
            return -1;
        }
        var rpso = new SerializedObject(rp);
        var rl = rpso.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < rl.arraySize; i++)
            if (rl.GetArrayElementAtIndex(i).objectReferenceValue == data) index = i;
        if (index < 0)
        {
            rl.arraySize++;
            index = rl.arraySize - 1;
            rl.GetArrayElementAtIndex(index).objectReferenceValue = data;
            rpso.ApplyModifiedProperties();
            EditorUtility.SetDirty(rp);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MarmartFX] 已把 FX_Renderer 加到 {rp.name} 的渲染器列表第 {index} 项（默认渲染器不变）。");
        }

        var scene = EditorSceneManager.GetActiveScene();
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.gameObject.scene != scene) continue;
            var cd = cam.GetUniversalAdditionalCameraData();
            if (cd == null) continue;
            Undo.RecordObject(cd, "MarmartFX renderer");
            cd.SetRenderer(index);
            cd.renderPostProcessing = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cd);
        }
        return index;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
