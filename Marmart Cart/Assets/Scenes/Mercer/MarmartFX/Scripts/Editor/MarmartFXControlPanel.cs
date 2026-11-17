using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// MarmartFX 控制面板 (same layout as the old Hades Style panel): one tab per style version
/// (A, Toy Plastic, 8 art styles), each with its own material / main light / post / full-screen
/// filter parameters, plus the shared supermarket lighting. Changes preview live in FX_TestScene.
/// Open with Tools > MarmartFX > 控制面板 (Ctrl+Shift+M).
/// Only touches files in Assets/Scenes/Mercer/MarmartFX and the FX_TestScene.
/// </summary>
[InitializeOnLoad]
public class MarmartFXControlPanel : EditorWindow
{
    public const string PresetPath = MarmartFXSetup.Root + "/Settings/MarmartFX_Preset.asset";
    const string MaterialFolder = MarmartFXSetup.Root + "/Materials/ToyPlastic";
    const string VolumePath = MarmartFXSetup.Root + "/Settings/MarmartFX_ToyPlastic_Volume.asset";
    const string OpenMarker = MarmartFXSetup.Root + "/AUTO_PANEL.txt";

    static readonly string[] SectionNames = { "当前版本参数", "超市灯光", "特效 VFX", "工具" };
    static readonly string[] VFXTabs = { "撞货架掉落", "加速轨迹", "玩家高亮", "吃到新车", "玩家颜色", "商品泡泡" };
    static readonly string[] VFXProps = { "spill", "trail", "highlight", "eat", "playerColors", "bubble" };

    int m_Section;
    Vector2 m_Scroll;
    SerializedObject m_StyleSO;
    SerializedObject m_LightSO;
    SerializedObject m_VFXSO;
    int m_VFXTab;
    List<Material> m_Materials;

    static MarmartFXControlPanel()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(OpenMarker) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(OpenMarker);
            if (File.Exists(OpenMarker + ".meta")) File.Delete(OpenMarker + ".meta");
            GetOrCreatePreset();
            Open();
        };
    }

    [MenuItem("Tools/MarmartFX/控制面板 Control Panel %#m", priority = 0)]
    public static void Open()
    {
        var w = GetWindow<MarmartFXControlPanel>();
        w.titleContent = new GUIContent("MarmartFX 控制面板");
        w.minSize = new Vector2(420, 480);
        w.Show();
    }

    public static MarmartFXPreset GetOrCreatePreset()
    {
        var p = AssetDatabase.LoadAssetAtPath<MarmartFXPreset>(PresetPath);
        if (p != null) return p;
        string folder = Path.GetDirectoryName(PresetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(MarmartFXSetup.Root, "Settings");
        p = CreateInstance<MarmartFXPreset>();
        AssetDatabase.CreateAsset(p, PresetPath);
        AssetDatabase.SaveAssets();
        return p;
    }

    static bool InTestScene => EditorSceneManager.GetActiveScene().path == MarmartFXSetup.ScenePath;

    void OnEnable() => m_Materials = null;

    void OnGUI()
    {
        var lightPreset = GetOrCreatePreset();
        int active = MarmartFXSetup.ActiveStyleIndex;
        var styles = Enumerable.Range(0, MarmartFXSetup.StyleFiles.Length).Select(MarmartFXSetup.LoadStyle).ToArray();

        EditorGUILayout.Space(6);
        if (!InTestScene)
        {
            EditorGUILayout.HelpBox("当前不是 FX_TestScene。材质和调色仍可调，但灯光只会作用在 FX_TestScene 里。", MessageType.Warning);
            if (GUILayout.Button("打开 FX_TestScene", GUILayout.Height(26)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MarmartFXSetup.ScenePath);
        }

        // ---- Version tabs ----
        var names = styles.Select((s, i) => (i == active ? "● " : "") + s.displayName).ToArray();
        int rows = (names.Length + 1) / 2;
        int newTab = GUILayout.SelectionGrid(active, names, 2, GUILayout.Height(rows * 26));
        if (newTab != active)
        {
            GUI.FocusControl(null);
            MarmartFXSetup.ActiveStyleIndex = newTab;
            active = newTab;
            m_StyleSO = null;
            ApplyStyle(styles[active]);
        }

        var style = styles[active];
        var swap = FindSwap();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Shader: " + MarmartFXStylePreset.ShaderName(style.style), EditorStyles.miniLabel);
            if (GUILayout.Button("恢复默认值", GUILayout.Width(100), GUILayout.Height(22)) &&
                EditorUtility.DisplayDialog("恢复默认值", $"把「{style.displayName}」的所有参数恢复成出厂默认值？", "恢复", "取消"))
            {
                Undo.RecordObject(style, "Reset MarmartFX Style");
                string keepName = style.displayName;
                style.ResetToVersion(active);
                style.displayName = keepName;
                EditorUtility.SetDirty(style);
                m_StyleSO = null;
                ApplyStyle(style);
            }
        }
        if (swap != null)
        {
            string label = swap.useToyPlastic ? "● 风格材质 (点击看原版材质)" : "○ 原版材质 (点击看风格材质)";
            if (GUILayout.Button(label, GUILayout.Height(22))) ToggleLook(swap);
        }
        EditorGUILayout.HelpBox("点上面的分页即切换场景里的版本，调参立即生效。全屏滤镜（墨线 / 像素 / 油画等）由 FX_Renderer 上的 MarmartFX PostFX 渲染。Play 时按 F8 切换原版材质。", MessageType.None);

        EditorGUILayout.Space(2);
        m_Section = GUILayout.Toolbar(m_Section, SectionNames, GUILayout.Height(24));

        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
        if (m_Section == 0) DrawStyle(style);
        else if (m_Section == 1) DrawLighting(lightPreset);
        else if (m_Section == 2) DrawVFX();
        else DrawTools(style);
        EditorGUILayout.EndScrollView();
    }

    void DrawStyle(MarmartFXStylePreset style)
    {
        if (m_StyleSO == null || m_StyleSO.targetObject != style) m_StyleSO = new SerializedObject(style);
        m_StyleSO.Update();
        string own = MarmartFXStylePreset.StylePrefix(style.style);
        string[] prefixes = { "toy", "art" };

        EditorGUI.BeginChangeCheck();
        var prop = m_StyleSO.GetIterator();
        bool enter = true;
        while (prop.NextVisible(enter))
        {
            enter = false;
            if (prop.name == "m_Script") continue;
            string pn = prop.name;
            if (prefixes.Any(p => pn.StartsWith(p) && p != own)) continue;
            EditorGUILayout.PropertyField(prop, true);
        }
        if (EditorGUI.EndChangeCheck())
        {
            m_StyleSO.ApplyModifiedProperties();
            ApplyStyle(style);
        }
    }

    void DrawLighting(MarmartFXPreset preset)
    {
        if (m_LightSO == null || m_LightSO.targetObject != preset) m_LightSO = new SerializedObject(preset);
        m_LightSO.Update();
        EditorGUILayout.HelpBox("所有版本共用的超市灯光。主光的颜色和强度在每个版本的参数里（主光源 Main Light）。", MessageType.None);
        EditorGUI.BeginChangeCheck();
        var prop = m_LightSO.GetIterator();
        bool enter = true;
        while (prop.NextVisible(enter))
        {
            enter = false;
            if (prop.name == "m_Script") continue;
            EditorGUILayout.PropertyField(prop, true);
        }
        if (EditorGUI.EndChangeCheck())
        {
            m_LightSO.ApplyModifiedProperties();
            ApplyLighting(preset);
        }
        if (GUILayout.Button("灯光恢复默认值", GUILayout.Height(22)))
        {
            Undo.RecordObject(preset, "Reset MarmartFX Lighting");
            int keep = preset.activeStyle;
            preset.ResetToDefaults();
            preset.activeStyle = keep;
            EditorUtility.SetDirty(preset);
            m_LightSO = null;
            ApplyLighting(preset);
        }
    }

    void DrawVFX()
    {
        var settings = MarmartFXSetup.GetOrCreateVFXSettings();
        if (m_VFXSO == null || m_VFXSO.targetObject != settings) m_VFXSO = new SerializedObject(settings);
        m_VFXSO.Update();

        bool inScene = Object.FindObjectsByType<MarmartFXPlayerFX>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
        if (!inScene)
        {
            EditorGUILayout.HelpBox("FX_TestScene 里还没有特效物体。", MessageType.Info);
            if (InTestScene && GUILayout.Button("把特效加入 FX_TestScene", GUILayout.Height(26))) MarmartFXSetup.VFXMenu();
        }
        EditorGUILayout.HelpBox("参数保存在 MarmartFX/Settings/MarmartFX_VFX.asset，Play 模式里改也会保留。每种效果都可以单独开关、换样式。", MessageType.None);

        m_VFXTab = GUILayout.SelectionGrid(m_VFXTab, VFXTabs, 3, GUILayout.Height(48));
        EditorGUILayout.Space(4);
        var prop = m_VFXSO.FindProperty(VFXProps[m_VFXTab]);
        if (prop != null)
        {
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                for (int i = 0; i < prop.arraySize; i++)
                    EditorGUILayout.PropertyField(prop.GetArrayElementAtIndex(i), new GUIContent("玩家 " + (i + 1)));
            }
            else
            {
                var it = prop.Copy();
                var end = prop.GetEndProperty();
                bool enter = true;
                while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                {
                    enter = false;
                    EditorGUILayout.PropertyField(it, true);
                }
            }
        }
        if (m_VFXSO.ApplyModifiedProperties()) EditorUtility.SetDirty(settings);

        if (Application.isPlaying && m_VFXTab == 0 && GUILayout.Button("清空所有掉落物", GUILayout.Height(24)))
            foreach (var sp in Object.FindObjectsByType<MarmartFXShelfSpill>(FindObjectsSortMode.None)) sp.ClearAll();
    }

    void DrawTools(MarmartFXStylePreset style)
    {
        EditorGUILayout.Space(6);
        if (GUILayout.Button("全部重新应用 (当前版本 + 灯光)", GUILayout.Height(30)))
        {
            ApplyStyle(style);
            ApplyLighting(GetOrCreatePreset());
        }
        if (GUILayout.Button("重新生成风格材质 (新增道具后用)", GUILayout.Height(26)))
        {
            MarmartFXSetup.ApplyToyPlasticMenu();
            m_Materials = null;
            ApplyStyle(style);
        }
        if (GUILayout.Button("重建超市灯光", GUILayout.Height(26)))
        {
            MarmartFXSetup.LightingMenu();
            ApplyStyle(style);
            ApplyLighting(GetOrCreatePreset());
        }
        if (GUILayout.Button("设置 / 修复 FX_Renderer (全屏滤镜)", GUILayout.Height(26)))
            MarmartFXSetup.RendererMenu();
        EditorGUILayout.Space(8);
        if (GUILayout.Button("选中当前版本的预设文件", GUILayout.Height(22)))
        {
            Selection.activeObject = style;
            EditorGUIUtility.PingObject(style);
        }
    }

    // ------------------------------------------------------------------

    List<Material> Materials()
    {
        if (m_Materials == null || m_Materials.Any(m => m == null))
        {
            m_Materials = AssetDatabase.IsValidFolder(MaterialFolder)
                ? AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder })
                    .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(m => m != null).ToList()
                : new List<Material>();
        }
        return m_Materials;
    }

    static GameObject LightingRoot()
    {
        if (!InTestScene) return null;
        return EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .FirstOrDefault(g => g.name == MarmartFXPreset.LightingRootName);
    }

    void ApplyStyle(MarmartFXStylePreset style)
    {
        if (style == null) return;
        EditorUtility.SetDirty(style);

        foreach (var m in Materials())
        {
            style.ApplyToMaterial(m);
            EditorUtility.SetDirty(m);
        }

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (profile != null)
        {
            style.ApplyToProfile(profile);
            EditorUtility.SetDirty(profile);
            foreach (var c in profile.components) EditorUtility.SetDirty(c);
        }

        var root = LightingRoot();
        if (root != null)
        {
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Directional) continue;
                Undo.RecordObject(l, "MarmartFX Style");
                style.ApplyToLight(l);
            }
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(root.scene);
        }
        InternalEditorUtility.RepaintAllViews();
    }

    static void ApplyLighting(MarmartFXPreset preset)
    {
        EditorUtility.SetDirty(preset);
        var root = LightingRoot();
        if (root == null) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) Undo.RecordObject(t, "MarmartFX Lighting");
        foreach (var l in root.GetComponentsInChildren<Light>(true)) Undo.RecordObject(l, "MarmartFX Lighting");
        preset.ApplyLighting(root);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(root.scene);
        InternalEditorUtility.RepaintAllViews();
    }

    static MarmartFXMaterialSwap FindSwap() =>
        Object.FindObjectsByType<MarmartFXMaterialSwap>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    static void ToggleLook(MarmartFXMaterialSwap swap)
    {
        Undo.RecordObject(swap, "MarmartFX Toggle Look");
        swap.useToyPlastic = !swap.useToyPlastic;
        if (!Application.isPlaying)
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.gameObject.scene == swap.gameObject.scene).ToArray();
            foreach (var r in renderers) Undo.RecordObject(r, "MarmartFX Toggle Look");
            swap.ApplyAll(true);
            foreach (var r in renderers) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            EditorSceneManager.MarkSceneDirty(swap.gameObject.scene);
        }
        EditorUtility.SetDirty(swap);
    }

    void OnInspectorUpdate() => Repaint();
}
