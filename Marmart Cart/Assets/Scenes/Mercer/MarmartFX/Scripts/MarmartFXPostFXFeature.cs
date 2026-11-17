using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// MarmartFX full-screen style filters (ink outlines, pixelate, oil paint, watercolor, paper,
/// grain, scanlines, misregistration...). Unity 6 / URP 17 Render Graph version of the
/// HadesStyle PostFX feature. Driven by the MarmartFXPostFX volume override.
///
/// Only added to MarmartFX/Settings/FX_Renderer (used by FX_TestScene cameras), never to PC_Renderer.
/// </summary>
public class MarmartFXPostFXFeature : ScriptableRendererFeature
{
    public Shader shader;
    public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    [Tooltip("Objects on these layers are skipped by the style filter (no stylizing, no ink lines). Default: Cart.")]
    public LayerMask excludeLayers = 1 << 7;

    [Tooltip("Draw passes tagged LightMode=MarmartFXOverlay (player outline) after post-processing, so the style filters, color grading and tonemapping don't touch them.")]
    public bool drawOverlay = true;

    Material m_Material;
    Material m_MaskMaterial;
    MarmartFXPass m_Pass;
    OverlayPass m_Overlay;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find("Hidden/MarmartFX/PostFX");
        if (shader != null && m_Material == null)
            m_Material = CoreUtils.CreateEngineMaterial(shader);

        m_Pass = new MarmartFXPass { renderPassEvent = passEvent };
        var maskShader = Shader.Find("Hidden/MarmartFX/ExcludeMask");
        if (maskShader != null && m_MaskMaterial == null) m_MaskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
        m_Overlay = new OverlayPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camType = renderingData.cameraData.cameraType;
        if (camType == CameraType.Preview || camType == CameraType.Reflection) return;
        if (drawOverlay && m_Overlay != null) renderer.EnqueuePass(m_Overlay);
        if (m_Material == null) return;
        if (!renderingData.cameraData.postProcessEnabled) return;

        var settings = VolumeManager.instance.stack.GetComponent<MarmartFXPostFX>();
        if (settings == null || !settings.IsActive()) return;

        m_Pass.Setup(m_Material, settings);
        m_Pass.SetExclude(m_MaskMaterial, excludeLayers);
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        m_Material = null;
        CoreUtils.Destroy(m_MaskMaterial);
        m_MaskMaterial = null;
    }

    class MarmartFXPass : ScriptableRenderPass
    {
        static readonly int s_Intensity = Shader.PropertyToID("_Intensity");
        static readonly int s_OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int s_OutlineParams = Shader.PropertyToID("_OutlineParams");
        static readonly int s_PaintParams = Shader.PropertyToID("_PaintParams");
        static readonly int s_Style0 = Shader.PropertyToID("_StyleParams0");
        static readonly int s_Style1 = Shader.PropertyToID("_StyleParams1");
        static readonly int s_Style2 = Shader.PropertyToID("_StyleParams2");
        static readonly int s_ShadowHue = Shader.PropertyToID("_ShadowHue");

        static readonly int s_ExcludeMask = Shader.PropertyToID("_MarmartFXExcludeMask");
        static readonly int s_ExcludeOn = Shader.PropertyToID("_MarmartFXExcludeOn");
        static readonly ShaderTagId[] s_MaskTags =
        {
            new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit")
        };

        Material m_Material;
        Material m_MaskMaterial;
        LayerMask m_ExcludeLayers;

        class PassData
        {
            public TextureHandle source;
            public TextureHandle mask;
            public bool hasMask;
            public Material material;
        }

        class MaskPassData { public RendererListHandle list; }

        public void SetExclude(Material maskMaterial, LayerMask layers)
        {
            m_MaskMaterial = maskMaterial;
            m_ExcludeLayers = layers;
        }

        public MarmartFXPass()
        {
            profilingSampler = new ProfilingSampler("MarmartFX PostFX");
            requiresIntermediateTexture = true;
        }

        public void Setup(Material material, MarmartFXPostFX s)
        {
            m_Material = material;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);

            material.SetFloat(s_Intensity, s.intensity.value);
            material.SetColor(s_OutlineColor, s.outlineColor.value);
            material.SetVector(s_OutlineParams, new Vector4(
                s.outlineThickness.value, s.depthThreshold.value, s.normalThreshold.value, s.fadeDistance.value));
            material.SetVector(s_PaintParams, new Vector4(s.posterizeSteps.value, s.shadowHueStrength.value, 0f, 0f));
            material.SetColor(s_ShadowHue, s.shadowHue.value);
            material.SetVector(s_Style0, new Vector4(s.pixelSize.value, s.kuwaharaRadius.value, s.watercolorBleed.value, s.lineWobble.value));
            material.SetVector(s_Style1, new Vector4(s.wobbleFps.value, s.wobbleScale.value, s.paperStrength.value, s.paperScale.value));
            material.SetVector(s_Style2, new Vector4(s.filmGrain.value, s.scanlines.value, s.misregistration.value, s.ditherStrength.value));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null) return;
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_MarmartFXPostFX";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            // Exclude mask: objects on excludeLayers (carts) drawn as 1, depth-tested against the scene.
            TextureHandle mask = TextureHandle.nullHandle;
            bool hasMask = m_MaskMaterial != null && m_ExcludeLayers.value != 0 && resourceData.activeDepthTexture.IsValid();
            if (hasMask)
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                var maskDesc = desc;
                maskDesc.name = "_MarmartFXExcludeMask";
                maskDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.clear;
                mask = renderGraph.CreateTexture(maskDesc);

                var ds = RenderingUtils.CreateDrawingSettings(new System.Collections.Generic.List<ShaderTagId>(s_MaskTags), renderingData, cameraData, lightData, SortingCriteria.None);
                ds.overrideMaterial = m_MaskMaterial;
                ds.overrideMaterialPassIndex = 0;
                var filter = new FilteringSettings(RenderQueueRange.all, m_ExcludeLayers);
                var param = new RendererListParams(renderingData.cullResults, ds, filter);

                using (var mb = renderGraph.AddRasterRenderPass<MaskPassData>("MarmartFX Exclude Mask", out var md, profilingSampler))
                {
                    md.list = renderGraph.CreateRendererList(param);
                    mb.UseRendererList(md.list);
                    mb.SetRenderAttachment(mask, 0);
                    mb.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    mb.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.list));
                }
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("MarmartFX PostFX", out var data, profilingSampler))
            {
                data.source = source;
                data.material = m_Material;
                data.mask = mask;
                data.hasMask = hasMask;
                if (hasMask) builder.UseTexture(mask);
                builder.UseTexture(source);
                if (resourceData.cameraDepthTexture.IsValid()) builder.UseTexture(resourceData.cameraDepthTexture);
                if (resourceData.cameraNormalsTexture.IsValid()) builder.UseTexture(resourceData.cameraNormalsTexture);
                builder.SetRenderAttachment(destination, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                {
                    if (d.hasMask) d.material.SetTexture(s_ExcludeMask, d.mask);
                    d.material.SetFloat(s_ExcludeOn, d.hasMask ? 1f : 0f);
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                });
            }

            resourceData.cameraColor = destination;
        }
    }

    /// Draws renderers whose shader has a LightMode=MarmartFXOverlay pass on top of the final
    /// post-processed image, depth-tested (and stencil-tested) against the scene.
    class OverlayPass : ScriptableRenderPass
    {
        static readonly ShaderTagId s_Tag = new ShaderTagId("MarmartFXOverlay");
        static readonly ShaderTagId s_TagGlow = new ShaderTagId("MarmartFXOverlayGlow");

        class PassData { public RendererListHandle list; }

        public OverlayPass()
        {
            profilingSampler = new ProfilingSampler("MarmartFX Overlay");
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            if (!resourceData.activeColorTexture.IsValid() || !resourceData.activeDepthTexture.IsValid()) return;

            var drawSettings = RenderingUtils.CreateDrawingSettings(s_Tag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            drawSettings.SetShaderPassName(1, s_TagGlow);
            var filter = new FilteringSettings(RenderQueueRange.all);
            var param = new RendererListParams(renderingData.cullResults, drawSettings, filter);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("MarmartFX Overlay", out var data, profilingSampler))
            {
                data.list = renderGraph.CreateRendererList(param);
                builder.UseRendererList(data.list);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.list));
            }
        }
    }
}
