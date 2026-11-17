using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One complete "version" of the look: toon material settings, main light and post-processing.
/// Edited from Tools > MarmartFX > 控制面板 (ported from HadesStyle).
/// </summary>
public enum MarmartFXStyleShader
{
    HadesToon = 0,
    ToyPlastic = 2,
    HandDrawn = 10,
    OilPaint = 11,
    Watercolor = 12,
    Scribble = 13,
    RetroFilm = 14,
    Pixel = 15,
    RetroPoster = 16,
    NeonArcade = 17,
}

[CreateAssetMenu(menuName = "MarmartFX/Style Preset", fileName = "MarmartFX_Style")]
public class MarmartFXStylePreset : ScriptableObject
{
    public string displayName = "Version";
    [TextArea(2, 4)] public string note;
    [HideInInspector] public MarmartFXStyleShader style = MarmartFXStyleShader.HadesToon;

    public static string ShaderName(MarmartFXStyleShader s)
    {
        switch (s)
        {
            case MarmartFXStyleShader.ToyPlastic: return "MarmartFX/ToyPlastic";
            case MarmartFXStyleShader.HandDrawn: return "MarmartFX/Art/HandDrawn";
            case MarmartFXStyleShader.OilPaint: return "MarmartFX/Art/OilPaint";
            case MarmartFXStyleShader.Watercolor: return "MarmartFX/Art/Watercolor";
            case MarmartFXStyleShader.Scribble: return "MarmartFX/Art/Scribble";
            case MarmartFXStyleShader.RetroFilm: return "MarmartFX/Art/RetroFilm";
            case MarmartFXStyleShader.Pixel: return "MarmartFX/Art/Pixel";
            case MarmartFXStyleShader.RetroPoster: return "MarmartFX/Art/RetroPoster";
            case MarmartFXStyleShader.NeonArcade: return "MarmartFX/Art/NeonArcade";
            default: return "MarmartFX/StylizedToonLit";
        }
    }

    [Header("材质 · 明暗分段 (Shading Bands)")]
    public Color shadowColor = new Color(0.66f, 0.52f, 0.78f);
    public Color deepShadowColor = new Color(0.45f, 0.34f, 0.62f);
    [Range(-1, 1)] public float shadeThreshold = 0.05f;
    [Range(-1, 1)] public float deepShadeThreshold = -0.45f;
    [Range(0.001f, 0.3f)] public float shadeSoftness = 0.02f;
    [Range(0, 1)] public float receiveShadowStrength = 1f;
    [Range(0, 2)] public float ambientStrength = 0.8f;
    [Range(0, 2)] public float pointLightBands = 1f;

    [Header("材质 · 高光 (Specular)")]
    [ColorUsage(false, true)] public Color specularColor = new Color(1f, 0.95f, 0.85f);
    [Range(0, 1)] public float specularSize = 0.06f;
    [Range(0.001f, 0.2f)] public float specularSoftness = 0.01f;

    [Header("材质 · 边缘光 (Rim Light)")]
    [ColorUsage(false, true)] public Color rimColor = new Color(1.6f, 0.75f, 0.35f);
    [Range(0, 1)] public float rimWidth = 0.62f;
    [Range(0.001f, 0.3f)] public float rimSoftness = 0.03f;
    [Range(0, 1)] public float rimOnlyOnLitSide = 0.5f;

    [Header("材质 · 描边 (Mesh Outline)")]
    public Color meshOutlineColor = new Color(0.07f, 0.03f, 0.06f);
    [Range(0, 10)] public float meshOutlineWidth = 2.5f;
    [Tooltip("0 = 纯描边色，1 = 用物体自身颜色加深做描边")]
    [Range(0, 1)] public float meshOutlineAlbedoMix = 0f;

    [Header("塑料包装专属 (Toy Plastic)")]
    [Range(2, 6)] public float toyRampSteps = 3f;
    [Range(0, 1)] public float toyGloss = 0.75f;
    [Range(0, 2)] public float toyReflection = 0.6f;
    public Color toySecondSpecColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("艺术风格专属 (Art Style)")]
    [Tooltip("图案大小：手绘线间距 / 油画笔触密度 / 水彩纸纹 / 像素格大小 / 霓虹网格密度等")]
    [Min(0.1f)] public float artScale = 6f;
    [Range(0, 1)] public float artStrength = 0.8f;
    public Color artColor = new Color(0.12f, 0.1f, 0.18f);
    [Tooltip("每个风格含义不同：线宽 / 笔触扰动 / 晕染 / 留白 / 褪色 / 网点 / 底色亮度")]
    [Range(0, 1)] public float artParam = 0.3f;
    [Tooltip("手绘抖动每秒重画几次，0 = 不动")]
    [Range(0, 24)] public float artBoilFps = 0f;

    [Header("主光源 (Main Light)")]
    public bool overrideMainLight = true;
    public Color mainLightColor = new Color(1f, 0.88f, 0.72f);
    [Range(0, 5)] public float mainLightIntensity = 1.3f;

    [Header("后处理 · 颜色 (Color Grading)")]
    [Range(-2, 2)] public float exposure = 0.15f;
    [Range(-100, 100)] public float contrast = 22f;
    [Range(-100, 100)] public float saturation = 28f;
    public Color colorFilter = new Color(1f, 0.96f, 0.92f);
    public Color splitShadows = new Color(0.36f, 0.27f, 0.6f);
    public Color splitHighlights = new Color(1f, 0.74f, 0.48f);
    [Range(-100, 100)] public float splitBalance = -15f;

    [Header("后处理 · 泛光 (Bloom)")]
    [Range(0, 3)] public float bloomThreshold = 0.95f;
    [Range(0, 5)] public float bloomIntensity = 0.9f;
    [Range(0, 1)] public float bloomScatter = 0.65f;
    public Color bloomTint = new Color(1f, 0.82f, 0.6f);

    [Header("后处理 · 暗角 (Vignette)")]
    public Color vignetteColor = new Color(0.12f, 0.03f, 0.1f);
    [Range(0, 1)] public float vignetteIntensity = 0.38f;
    [Range(0.01f, 1)] public float vignetteSmoothness = 0.45f;

    [Header("后处理 · 墨线与绘画感 (Ink & Paint)")]
    [Range(0, 1)] public float inkIntensity = 1f;
    public Color inkColor = new Color(0.07f, 0.03f, 0.06f);
    [Range(0.5f, 4)] public float inkThickness = 1.2f;
    [Range(0.001f, 1)] public float inkDepthThreshold = 0.06f;
    [Range(0.05f, 2)] public float inkNormalThreshold = 0.5f;
    [Min(1)] public float inkFadeDistance = 80f;
    [Range(0, 16)] public int posterizeSteps = 0;
    public Color shadowHue = new Color(0.32f, 0.18f, 0.48f);
    [Range(0, 1)] public float shadowHueStrength = 0.3f;

    [Header("后处理 · 风格滤镜 (Style Filters)")]
    [Tooltip("像素化块大小，0 = 关")]
    [Range(0, 16)] public int pixelSize = 0;
    [Tooltip("油画笔触半径，0 = 关（越大越慢）")]
    [Range(0, 6)] public int oilBrushRadius = 0;
    [Range(0, 1)] public float watercolorBleed = 0f;
    [Tooltip("线条抖动（像素）")]
    [Range(0, 6)] public float lineWobble = 0f;
    [Range(0, 24)] public float wobbleFps = 8f;
    [Range(2, 200)] public float wobbleScale = 40f;
    [Range(0, 1)] public float paperTexture = 0f;
    [Range(1, 40)] public float paperScale = 6f;
    [Range(0, 0.3f)] public float filmGrain = 0f;
    [Range(0, 1)] public float scanlines = 0f;
    [Tooltip("套色错位 / 色差（像素）")]
    [Range(0, 8)] public float misregistration = 0f;
    [Tooltip("色阶抖动（配合色阶数使用）")]
    [Range(0, 1)] public float ditherStrength = 0f;

    public void ApplyToMaterial(Material m)
    {
        if (m == null) return;
        var target = Shader.Find(ShaderName(style));
        if (target != null && m.shader != target)
        {
            int queue = m.renderQueue;
            m.shader = target;
            m.renderQueue = queue;
        }
        m.SetColor("_ShadowColor", shadowColor);
        m.SetColor("_DeepShadowColor", deepShadowColor);
        m.SetFloat("_ShadeThreshold", shadeThreshold);
        m.SetFloat("_DeepShadeThreshold", deepShadeThreshold);
        m.SetFloat("_ShadeSoftness", shadeSoftness);
        m.SetFloat("_ReceiveShadowStrength", receiveShadowStrength);
        m.SetFloat("_AmbientStrength", ambientStrength);
        m.SetFloat("_AddLightBands", pointLightBands);
        m.SetColor("_SpecularColor", specularColor);
        m.SetFloat("_SpecularSize", specularSize);
        m.SetFloat("_SpecularSoftness", specularSoftness);
        m.SetColor("_RimColor", rimColor);
        m.SetFloat("_RimThreshold", rimWidth);
        m.SetFloat("_RimSoftness", rimSoftness);
        m.SetFloat("_RimLightAlign", rimOnlyOnLitSide);
        m.SetColor("_OutlineColor", meshOutlineColor);
        m.SetFloat("_OutlineWidth", meshOutlineWidth);
        m.SetFloat("_OutlineAlbedoMix", meshOutlineAlbedoMix);

        m.SetFloat("_ToyRampSteps", toyRampSteps);
        m.SetFloat("_ToyGloss", toyGloss);
        m.SetFloat("_ToyReflection", toyReflection);
        m.SetColor("_ToySecondSpecColor", toySecondSpecColor);

        m.SetFloat("_ArtScale", artScale);
        m.SetFloat("_ArtStrength", artStrength);
        m.SetColor("_ArtColor", artColor);
        m.SetFloat("_ArtParam", artParam);
        m.SetFloat("_ArtFps", artBoilFps);
    }

    /// <summary>Field-name prefix of the style-specific section ("toy", "art"), or null.</summary>
    public static string StylePrefix(MarmartFXStyleShader s)
    {
        if (s == MarmartFXStyleShader.ToyPlastic) return "toy";
        if ((int)s >= 10) return "art";
        return null;
    }

    public void ApplyToLight(Light l)
    {
        if (l == null || !overrideMainLight) return;
        l.color = mainLightColor;
        l.intensity = mainLightIntensity;
    }

    public void ApplyToProfile(VolumeProfile p)
    {
        if (p == null) return;
        if (p.TryGet(out Tonemapping tone)) tone.mode.Override(TonemappingMode.Neutral);
        if (p.TryGet(out ColorAdjustments ca))
        {
            ca.postExposure.Override(exposure);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(colorFilter);
        }
        if (p.TryGet(out SplitToning st))
        {
            st.shadows.Override(splitShadows);
            st.highlights.Override(splitHighlights);
            st.balance.Override(splitBalance);
        }
        if (p.TryGet(out Bloom b))
        {
            b.threshold.Override(bloomThreshold);
            b.intensity.Override(bloomIntensity);
            b.scatter.Override(bloomScatter);
            b.tint.Override(bloomTint);
        }
        if (p.TryGet(out Vignette v))
        {
            v.color.Override(vignetteColor);
            v.intensity.Override(vignetteIntensity);
            v.smoothness.Override(vignetteSmoothness);
        }
        if (p.TryGet(out MarmartFXPostFX fx))
        {
            fx.intensity.Override(1f);
            fx.outlineColor.Override(new Color(inkColor.r, inkColor.g, inkColor.b, inkIntensity));
            fx.outlineThickness.Override(inkThickness);
            fx.depthThreshold.Override(inkDepthThreshold);
            fx.normalThreshold.Override(inkNormalThreshold);
            fx.fadeDistance.Override(inkFadeDistance);
            fx.posterizeSteps.Override(posterizeSteps);
            fx.shadowHue.Override(shadowHue);
            fx.shadowHueStrength.Override(shadowHueStrength);
            fx.pixelSize.Override(pixelSize);
            fx.kuwaharaRadius.Override(oilBrushRadius);
            fx.watercolorBleed.Override(watercolorBleed);
            fx.lineWobble.Override(lineWobble);
            fx.wobbleFps.Override(wobbleFps);
            fx.wobbleScale.Override(wobbleScale);
            fx.paperStrength.Override(paperTexture);
            fx.paperScale.Override(paperScale);
            fx.filmGrain.Override(filmGrain);
            fx.scanlines.Override(scanlines);
            fx.misregistration.Override(misregistration);
            fx.ditherStrength.Override(ditherStrength);
        }
    }

    /// <summary>Copies only the shadow tones / ambient from the defaults (keeps every other tweak).</summary>
    public void RefreshShadowDefaults()
    {
        var d = CreateInstance<MarmartFXStylePreset>();
        shadowColor = d.shadowColor;
        deepShadowColor = d.deepShadowColor;
        ambientStrength = d.ambientStrength;
        DestroyImmediate(d);
    }

    /// <summary>Restores every parameter to the default look of version A.</summary>
    public void ResetToDefaults() => ResetToVersion(0);

    /// <summary>
    /// Factory defaults. 0 = A (Hades), 1 = Toy Plastic, 2 = Hand Drawn, 3 = Oil Paint, 4 = Watercolor,
    /// 5 = Scribble, 6 = Retro Film, 7 = Pixel, 8 = Retro Poster, 9 = Neon Arcade.
    /// </summary>
    public void ResetToVersion(int version)
    {
        var fresh = CreateInstance<MarmartFXStylePreset>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
        DestroyImmediate(fresh);

        switch (version)
        {
            default:
                style = MarmartFXStyleShader.HadesToon;
                displayName = "A · 冥火暖金";
                note = "暖橙边缘光 + 紫色阴影 + 金色泛光。";
                break;

            case 1: // Shiny packaging under fluorescent store lights
                style = MarmartFXStyleShader.ToyPlastic;
                displayName = "塑料包装";
                note = "阶梯明暗 + 塑料反光 + 冷白日光灯，商品像亮晶晶的玩具和包装盒。";
                shadowColor = new Color(0.62f, 0.68f, 0.88f);
                deepShadowColor = new Color(0.45f, 0.5f, 0.75f);
                shadeThreshold = 0.1f;
                ambientStrength = 0.75f;
                specularColor = new Color(1.6f, 1.6f, 1.6f);
                specularSize = 0.09f;
                specularSoftness = 0.01f;
                rimColor = new Color(0.9f, 0.95f, 1f);
                rimWidth = 0.76f;
                rimSoftness = 0.04f;
                rimOnlyOnLitSide = 0f;
                meshOutlineColor = new Color(0.08f, 0.09f, 0.15f);
                meshOutlineWidth = 1.5f;
                meshOutlineAlbedoMix = 0.4f;
                mainLightColor = new Color(0.94f, 0.98f, 1f);
                mainLightIntensity = 1.35f;
                exposure = 0.2f;
                contrast = 15f;
                saturation = 25f;
                colorFilter = new Color(0.97f, 1f, 1f);
                splitShadows = new Color(0.45f, 0.55f, 0.75f);
                splitHighlights = new Color(1f, 0.97f, 0.9f);
                splitBalance = 0f;
                bloomThreshold = 1.0f;
                bloomIntensity = 0.8f;
                bloomScatter = 0.6f;
                bloomTint = Color.white;
                vignetteColor = new Color(0.05f, 0.08f, 0.15f);
                vignetteIntensity = 0.2f;
                vignetteSmoothness = 0.5f;
                inkIntensity = 0f;
                shadowHue = new Color(0.3f, 0.4f, 0.7f);
                shadowHueStrength = 0.15f;
                break;

            case 2: // Hand drawn
                style = MarmartFXStyleShader.HandDrawn;
                displayName = "手绘";
                note = "铅笔交叉排线 + 会抖动的手绘线条 + 纸张纹理，像会动的速写本。";
                shadowColor = new Color(0.75f, 0.72f, 0.85f);
                deepShadowColor = new Color(0.6f, 0.56f, 0.75f);
                shadeSoftness = 0.01f;
                ambientStrength = 0.8f;
                specularColor = new Color(1f, 1f, 1f);
                specularSize = 0.06f;
                rimColor = new Color(0.35f, 0.35f, 0.35f);
                rimWidth = 0.8f;
                rimOnlyOnLitSide = 0f;
                meshOutlineColor = new Color(0.12f, 0.1f, 0.18f);
                meshOutlineWidth = 2.5f;
                artScale = 6f; artStrength = 0.85f; artColor = new Color(0.12f, 0.1f, 0.18f); artParam = 0.3f; artBoilFps = 6f;
                mainLightColor = new Color(1f, 0.98f, 0.94f);
                mainLightIntensity = 1.3f;
                exposure = 0.35f; contrast = 5f; saturation = 10f; colorFilter = Color.white;
                splitShadows = new Color(0.55f, 0.5f, 0.7f); splitHighlights = new Color(1f, 0.97f, 0.9f); splitBalance = 0f;
                bloomThreshold = 1.1f; bloomIntensity = 0.3f; bloomScatter = 0.5f; bloomTint = Color.white;
                vignetteColor = new Color(0.3f, 0.25f, 0.2f); vignetteIntensity = 0.15f; vignetteSmoothness = 0.6f;
                inkIntensity = 1f; inkColor = new Color(0.12f, 0.1f, 0.18f); inkThickness = 1.3f;
                shadowHue = new Color(0.4f, 0.35f, 0.6f); shadowHueStrength = 0.1f;
                lineWobble = 1.5f; wobbleFps = 6f; wobbleScale = 30f; paperTexture = 0.5f; paperScale = 4f;
                break;

            case 3: // Oil paint
                style = MarmartFXStyleShader.OilPaint;
                displayName = "油画";
                note = "厚涂笔触滤镜 + 暖光冷影 + 画布纹理，整个超市像一幅油画。";
                shadowColor = new Color(0.55f, 0.6f, 0.8f);
                deepShadowColor = new Color(0.35f, 0.38f, 0.6f);
                ambientStrength = 0.7f;
                specularColor = new Color(1.2f, 1.1f, 0.9f);
                specularSize = 0.12f;
                specularSoftness = 0.02f;
                rimColor = new Color(0.8f, 0.6f, 0.4f);
                rimWidth = 0.7f;
                rimSoftness = 0.08f;
                meshOutlineWidth = 0f;
                artScale = 4f; artStrength = 0.6f; artColor = new Color(1f, 0.95f, 0.85f); artParam = 0.35f; artBoilFps = 0f;
                mainLightColor = new Color(1f, 0.9f, 0.75f);
                mainLightIntensity = 1.35f;
                exposure = 0.1f; contrast = 18f; saturation = 20f; colorFilter = Color.white;
                splitShadows = new Color(0.3f, 0.4f, 0.6f); splitHighlights = new Color(1f, 0.8f, 0.55f); splitBalance = -5f;
                bloomThreshold = 0.9f; bloomIntensity = 0.6f; bloomScatter = 0.7f; bloomTint = new Color(1f, 0.85f, 0.65f);
                vignetteColor = new Color(0.15f, 0.08f, 0.05f); vignetteIntensity = 0.35f; vignetteSmoothness = 0.5f;
                inkIntensity = 0.35f; inkColor = new Color(0.15f, 0.1f, 0.08f); inkThickness = 1f;
                shadowHue = new Color(0.25f, 0.3f, 0.55f); shadowHueStrength = 0.25f;
                oilBrushRadius = 4; paperTexture = 0.25f; paperScale = 3f;
                break;

            case 4: // Watercolor
                style = MarmartFXStyleShader.Watercolor;
                displayName = "水彩";
                note = "颜料晕染 + 边缘积色 + 水彩纸纹理，明亮通透。";
                shadowColor = new Color(0.72f, 0.75f, 0.92f);
                deepShadowColor = new Color(0.55f, 0.58f, 0.8f);
                ambientStrength = 0.9f;
                specularSize = 0f;
                rimColor = new Color(0.3f, 0.3f, 0.35f);
                rimWidth = 0.8f;
                rimSoftness = 0.1f;
                meshOutlineWidth = 0f;
                artScale = 14f; artStrength = 0.7f; artColor = new Color(0.6f, 0.55f, 0.72f); artParam = 0.4f; artBoilFps = 0f;
                mainLightColor = new Color(1f, 0.98f, 0.94f);
                mainLightIntensity = 1.25f;
                exposure = 0.45f; contrast = -10f; saturation = 5f; colorFilter = Color.white;
                splitShadows = new Color(0.5f, 0.55f, 0.75f); splitHighlights = new Color(1f, 0.95f, 0.88f); splitBalance = 0f;
                bloomThreshold = 1.0f; bloomIntensity = 0.4f; bloomScatter = 0.7f; bloomTint = Color.white;
                vignetteColor = new Color(0.6f, 0.5f, 0.45f); vignetteIntensity = 0.2f; vignetteSmoothness = 0.8f;
                inkIntensity = 0.55f; inkColor = new Color(0.3f, 0.25f, 0.4f); inkThickness = 1f;
                shadowHue = new Color(0.5f, 0.5f, 0.8f); shadowHueStrength = 0.3f;
                watercolorBleed = 0.6f; lineWobble = 1f; wobbleFps = 0f; wobbleScale = 25f; paperTexture = 0.6f; paperScale = 5f;
                break;

            case 5: // Scribble
                style = MarmartFXStyleShader.Scribble;
                displayName = "乱涂";
                note = "蜡笔涂色留白 + 乱七八糟的阴影笔触 + 每秒抖 8 次的粗线，最适合四人混乱场面。";
                shadowColor = new Color(0.7f, 0.65f, 0.85f);
                deepShadowColor = new Color(0.55f, 0.5f, 0.75f);
                shadeSoftness = 0.01f;
                ambientStrength = 0.8f;
                specularSize = 0.05f;
                rimColor = new Color(0.6f, 0.6f, 0.6f);
                rimWidth = 0.8f;
                rimOnlyOnLitSide = 0f;
                meshOutlineColor = new Color(0.08f, 0.05f, 0.12f);
                meshOutlineWidth = 3f;
                meshOutlineAlbedoMix = 0.3f;
                artScale = 4f; artStrength = 0.8f; artColor = new Color(0.15f, 0.1f, 0.25f); artParam = 0.5f; artBoilFps = 8f;
                mainLightColor = new Color(1f, 0.98f, 0.95f);
                mainLightIntensity = 1.3f;
                exposure = 0.3f; contrast = 10f; saturation = 40f; colorFilter = Color.white;
                splitShadows = new Color(0.5f, 0.45f, 0.75f); splitHighlights = new Color(1f, 0.95f, 0.85f); splitBalance = 0f;
                bloomThreshold = 1.1f; bloomIntensity = 0.3f; bloomScatter = 0.5f; bloomTint = Color.white;
                vignetteColor = new Color(0.2f, 0.1f, 0.2f); vignetteIntensity = 0.1f; vignetteSmoothness = 0.6f;
                inkIntensity = 1f; inkColor = new Color(0.08f, 0.05f, 0.12f); inkThickness = 1.8f;
                shadowHue = new Color(0.4f, 0.3f, 0.7f); shadowHueStrength = 0.15f;
                lineWobble = 2.5f; wobbleFps = 8f; wobbleScale = 20f; paperTexture = 0.35f; paperScale = 4f;
                break;

            case 6: // Retro film
                style = MarmartFXStyleShader.RetroFilm;
                displayName = "复古胶片";
                note = "褪色暖调 + 胶片颗粒 + 重暗角 + 轻微色差，像 70 年代的老照片。";
                shadowColor = new Color(0.72f, 0.65f, 0.7f);
                deepShadowColor = new Color(0.55f, 0.5f, 0.58f);
                shadeSoftness = 0.08f;
                ambientStrength = 0.85f;
                specularColor = new Color(1f, 0.9f, 0.75f);
                specularSize = 0.08f;
                specularSoftness = 0.05f;
                rimColor = new Color(0.9f, 0.6f, 0.35f);
                rimWidth = 0.72f;
                rimSoftness = 0.1f;
                meshOutlineWidth = 0f;
                artScale = 1f; artStrength = 0.5f; artColor = new Color(1f, 0.92f, 0.78f); artParam = 0.5f; artBoilFps = 0f;
                mainLightColor = new Color(1f, 0.85f, 0.65f);
                mainLightIntensity = 1.3f;
                exposure = 0.05f; contrast = -12f; saturation = -15f; colorFilter = new Color(1f, 0.93f, 0.82f);
                splitShadows = new Color(0.35f, 0.45f, 0.5f); splitHighlights = new Color(1f, 0.8f, 0.55f); splitBalance = -10f;
                bloomThreshold = 0.8f; bloomIntensity = 1.0f; bloomScatter = 0.8f; bloomTint = new Color(1f, 0.8f, 0.6f);
                vignetteColor = new Color(0.12f, 0.06f, 0.02f); vignetteIntensity = 0.5f; vignetteSmoothness = 0.5f;
                inkIntensity = 0.25f; inkColor = new Color(0.2f, 0.12f, 0.08f); inkThickness = 1f;
                shadowHue = new Color(0.3f, 0.35f, 0.4f); shadowHueStrength = 0.25f;
                filmGrain = 0.12f; misregistration = 0.8f;
                break;

            case 7: // Pixel
                style = MarmartFXStyleShader.Pixel;
                displayName = "像素";
                note = "4 像素一格 + 有限色阶 + 网点抖动，像老式游戏机。";
                shadowColor = new Color(0.6f, 0.55f, 0.8f);
                deepShadowColor = new Color(0.4f, 0.35f, 0.65f);
                ambientStrength = 0.7f;
                specularColor = Color.white;
                specularSize = 0.08f;
                rimColor = new Color(0.5f, 0.5f, 0.6f);
                rimWidth = 0.78f;
                rimSoftness = 0.005f;
                meshOutlineWidth = 0f;
                artScale = 4f; artStrength = 0.8f; artColor = Color.white; artParam = 0.5f; artBoilFps = 0f;
                mainLightColor = new Color(1f, 0.97f, 0.92f);
                mainLightIntensity = 1.3f;
                exposure = 0.15f; contrast = 15f; saturation = 30f; colorFilter = Color.white;
                splitShadows = new Color(0.4f, 0.4f, 0.7f); splitHighlights = new Color(1f, 0.95f, 0.85f); splitBalance = 0f;
                bloomThreshold = 1.5f; bloomIntensity = 0f; bloomScatter = 0.5f; bloomTint = Color.white;
                vignetteColor = Color.black; vignetteIntensity = 0f; vignetteSmoothness = 0.5f;
                inkIntensity = 1f; inkColor = new Color(0.08f, 0.05f, 0.12f); inkThickness = 1f; inkDepthThreshold = 0.05f;
                shadowHue = new Color(0.3f, 0.2f, 0.5f); shadowHueStrength = 0.2f;
                pixelSize = 4; posterizeSteps = 6; ditherStrength = 0.7f;
                break;

            case 8: // Retro poster
                style = MarmartFXStyleShader.RetroPoster;
                displayName = "复古海报";
                note = "有限色板 + 米黄纸张 + 套色错位 + 粗描边，像丝网印刷的老海报。";
                shadowColor = new Color(0.55f, 0.6f, 0.75f);
                deepShadowColor = new Color(0.3f, 0.35f, 0.55f);
                shadeSoftness = 0.003f;
                ambientStrength = 0.6f;
                specularSize = 0f;
                rimColor = new Color(1f, 0.55f, 0.25f);
                rimWidth = 0.75f;
                rimSoftness = 0.005f;
                rimOnlyOnLitSide = 0.6f;
                meshOutlineColor = new Color(0.1f, 0.12f, 0.25f);
                meshOutlineWidth = 3f;
                artScale = 3f; artStrength = 0.8f; artColor = new Color(1f, 0.94f, 0.82f); artParam = 0.5f; artBoilFps = 0f;
                mainLightColor = new Color(1f, 0.95f, 0.85f);
                mainLightIntensity = 1.35f;
                exposure = 0.15f; contrast = 25f; saturation = 15f; colorFilter = new Color(1f, 0.95f, 0.85f);
                splitShadows = new Color(0.2f, 0.35f, 0.5f); splitHighlights = new Color(1f, 0.75f, 0.45f); splitBalance = -20f;
                bloomThreshold = 1.2f; bloomIntensity = 0.2f; bloomScatter = 0.5f; bloomTint = Color.white;
                vignetteColor = new Color(0.25f, 0.15f, 0.08f); vignetteIntensity = 0.3f; vignetteSmoothness = 0.5f;
                inkIntensity = 1f; inkColor = new Color(0.1f, 0.12f, 0.25f); inkThickness = 1.6f;
                shadowHue = new Color(0.2f, 0.3f, 0.5f); shadowHueStrength = 0.2f;
                posterizeSteps = 4; misregistration = 2.5f; paperTexture = 0.45f; paperScale = 3f; filmGrain = 0.04f;
                break;

            case 9: // Neon arcade
                style = MarmartFXStyleShader.NeonArcade;
                displayName = "霓虹街机";
                note = "暗色表面 + 发光边缘 + 霓虹网格 + 扫描线和超强泛光，像深夜街机厅。";
                shadowColor = new Color(0.4f, 0.3f, 0.7f);
                deepShadowColor = new Color(0.25f, 0.15f, 0.5f);
                ambientStrength = 0.4f;
                specularColor = new Color(2f, 2f, 2f);
                specularSize = 0.06f;
                rimColor = new Color(0.3f, 1.5f, 2.5f);
                rimWidth = 0.6f;
                rimSoftness = 0.05f;
                rimOnlyOnLitSide = 0f;
                meshOutlineColor = new Color(0.1f, 0.9f, 1f);
                meshOutlineWidth = 2f;
                meshOutlineAlbedoMix = 0.5f;
                artScale = 1f; artStrength = 0.7f; artColor = new Color(0.2f, 0.9f, 1f); artParam = 0.3f; artBoilFps = 0f;
                mainLightColor = new Color(0.7f, 0.6f, 1f);
                mainLightIntensity = 0.8f;
                exposure = 0f; contrast = 25f; saturation = 50f; colorFilter = new Color(0.9f, 0.9f, 1f);
                splitShadows = new Color(0.3f, 0.15f, 0.6f); splitHighlights = new Color(0.3f, 0.9f, 1f); splitBalance = 0f;
                bloomThreshold = 0.6f; bloomIntensity = 2.5f; bloomScatter = 0.75f; bloomTint = new Color(0.8f, 0.6f, 1f);
                vignetteColor = new Color(0.05f, 0f, 0.1f); vignetteIntensity = 0.45f; vignetteSmoothness = 0.5f;
                inkIntensity = 0.8f; inkColor = new Color(0.9f, 0.25f, 1f); inkThickness = 1f;
                shadowHue = new Color(0.3f, 0.1f, 0.6f); shadowHueStrength = 0.5f;
                scanlines = 0.35f; misregistration = 1.5f; filmGrain = 0.03f;
                break;
        }
    }
}
