using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// MarmartFX test-scene helper (lives only in Assets/Scenes/Mercer/MarmartFX).
/// Swaps original materials to their Toy Plastic versions on every renderer in the scene,
/// including carts / items spawned at runtime. Press the toggle key to compare with the
/// original look. Never edits the original material or prefab assets.
/// </summary>
[DisallowMultipleComponent]
public class MarmartFXMaterialSwap : MonoBehaviour
{
    [Tooltip("Original material (index matches Toy Plastic list).")]
    public List<Material> sourceMaterials = new List<Material>();
    [Tooltip("Toy Plastic version of each original material.")]
    public List<Material> toyMaterials = new List<Material>();

    [Header("Runtime")]
    public bool useToyPlastic = true;
    [Tooltip("这些图层上的物体保持原材质，不换成 ToyPlastic（默认 Cart 图层 = 所有小车）。")]
    public LayerMask excludeLayers = 1 << 7;
    [Tooltip("Press in Play mode to toggle original / Toy Plastic.")]
    public KeyCode toggleKey = KeyCode.F8;
    [Tooltip("How often (seconds) to look for newly spawned renderers.")]
    public float rescanInterval = 0.5f;
    [Tooltip("Turn on Post Processing for every camera in this scene (needed for the color grading volume).")]
    public bool forceCameraPostProcessing = true;

    private readonly Dictionary<Material, Material> toToy = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Material> toOriginal = new Dictionary<Material, Material>();
    private readonly HashSet<int> processed = new HashSet<int>();
    private float nextScan;
    private bool appliedState;

    private void Awake()
    {
        BuildMaps();
        appliedState = useToyPlastic;
    }

    private void OnEnable()
    {
        BuildMaps();
        ApplyAll(true);
    }

    private void BuildMaps()
    {
        toToy.Clear();
        toOriginal.Clear();
        int n = Mathf.Min(sourceMaterials.Count, toyMaterials.Count);
        for (int i = 0; i < n; i++)
        {
            Material s = sourceMaterials[i], t = toyMaterials[i];
            if (s == null || t == null) continue;
            toToy[s] = t;
            toOriginal[t] = s;
        }
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && IsTogglePressed())
        {
            useToyPlastic = !useToyPlastic;
        }

        if (appliedState != useToyPlastic)
        {
            appliedState = useToyPlastic;
            ApplyAll(true);
            return;
        }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
            ApplyAll(false);
        }
    }

    private bool IsTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && toggleKey == KeyCode.F8) return kb.f8Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleKey);
#else
        return false;
#endif
    }

    /// <summary>Swap materials on renderers. full = also revisit renderers already processed.</summary>
    public void ApplyAll(bool full)
    {
        if (toToy.Count == 0) BuildMaps();
        if (full) processed.Clear();
        var map = useToyPlastic ? toToy : toOriginal;

        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r == null || r.gameObject.scene != gameObject.scene) continue;
            int id = r.GetInstanceID();
            if (!full && processed.Contains(id)) continue;
            processed.Add(id);

            // Excluded layers (carts) always keep / get back their original materials.
            var useMap = ((1 << r.gameObject.layer) & excludeLayers.value) != 0 ? toOriginal : map;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && useMap.TryGetValue(mats[i], out var swap))
                {
                    mats[i] = swap;
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }

        if (forceCameraPostProcessing && Application.isPlaying)
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.gameObject.scene != gameObject.scene) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
            }
        }
    }
}
