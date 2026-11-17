using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Supermarket lighting shared by every style version (ceiling grid, ambient, main light angle).
/// Material / main light colour / post settings live in each MarmartFXStylePreset.
/// Edited from Tools > MarmartFX > 控制面板. Lives only in Assets/Scenes/Mercer/MarmartFX.
/// </summary>
[CreateAssetMenu(menuName = "MarmartFX/Preset", fileName = "MarmartFX_Preset")]
public class MarmartFXPreset : ScriptableObject
{
    public const string LightingRootName = "-------MarmartFX Lighting--------";
    public const string MainLightPrefix = "MarmartFX Main Light";
    public const string GridPrefix = "Ceiling Fluorescent Grid";
    public const string ProbePrefix = "Store Reflection Probe";

    [HideInInspector] public int activeStyle = 1; // index into MarmartFXSetup.StyleFiles (1 = Toy Plastic)

    [Header("灯光 · 主光角度 (Main Light)")]
    [Range(10, 90)] public float mainLightPitch = 68f;
    [Range(-180, 180)] public float mainLightYaw = -35f;
    [Range(0, 1)] public float mainShadowStrength = 0.55f;

    [Header("灯光 · 天花板灯阵 (Ceiling Grid)")]
    public bool ceilingLightsOn = true;
    public Color ceilingColor = new Color(0.9f, 0.96f, 1f, 1);
    [Range(0, 6)] public float ceilingIntensity = 1.6f;
    [Range(1, 40)] public float ceilingRange = 14.4f;

    [Header("灯光 · 环境光 (Ambient)")]
    public Color ambientSky = new Color(0.78f, 0.84f, 0.95f, 1);
    public Color ambientEquator = new Color(0.62f, 0.66f, 0.72f, 1);
    public Color ambientGround = new Color(0.36f, 0.35f, 0.36f, 1);
    [Range(0, 2)] public float reflectionProbeIntensity = 1f;

    public void ResetToDefaults()
    {
        var fresh = CreateInstance<MarmartFXPreset>();
        string keepName = name;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
        name = keepName;
        if (Application.isPlaying) Destroy(fresh); else DestroyImmediate(fresh);
    }

    // ------------------------------------------------------------------

    /// <summary>Apply light values to the MarmartFX lighting root under the given root object.</summary>
    public void ApplyLighting(GameObject lightingRoot)
    {
        if (lightingRoot == null) return;
        foreach (Transform child in lightingRoot.transform)
        {
            if (child.name.StartsWith(MainLightPrefix))
            {
                child.rotation = Quaternion.Euler(mainLightPitch, mainLightYaw, 0f);
                var l = child.GetComponent<Light>();
                if (l != null)
                {
                    l.shadowStrength = mainShadowStrength;
                }
            }
            else if (child.name.StartsWith(GridPrefix))
            {
                child.gameObject.SetActive(ceilingLightsOn);
                foreach (var l in child.GetComponentsInChildren<Light>(true))
                {
                    l.color = ceilingColor;
                    l.intensity = ceilingIntensity;
                    l.range = ceilingRange;
                }
            }
            else if (child.name.StartsWith(ProbePrefix))
            {
                var p = child.GetComponent<ReflectionProbe>();
                if (p != null) p.intensity = reflectionProbeIntensity;
            }
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
    }
}
