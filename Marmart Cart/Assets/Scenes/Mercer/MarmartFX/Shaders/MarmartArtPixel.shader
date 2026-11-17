// MarmartFX: ported from HadesStyle (art shader) for Unity 6 / URP 17.
Shader "MarmartFX/Art/Pixel"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Shading)]
        _ShadowColor ("Shadow Tint", Color) = (0.75, 0.65, 0.9, 1)
        _DeepShadowColor ("Deep Shadow Tint", Color) = (0.55, 0.45, 0.75, 1)
        _ShadeThreshold ("Light / Shadow Threshold", Range(-1, 1)) = 0
        _DeepShadeThreshold ("Shadow / Deep Shadow Threshold", Range(-1, 1)) = -0.4
        _ShadeSoftness ("Band Edge Softness", Range(0.001, 0.5)) = 0.1
        _ReceiveShadowStrength ("Received Shadow Strength", Range(0, 1)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.8
        _AddLightBands ("Point Light Strength", Range(0, 2)) = 1

        [Header(Specular and Rim)]
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize ("Specular Size", Range(0, 1)) = 0.1
        _SpecularSoftness ("Specular Softness", Range(0.001, 0.2)) = 0.03
        [HDR] _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimThreshold ("Rim Width", Range(0, 1)) = 0.7
        _RimSoftness ("Rim Softness", Range(0.001, 0.3)) = 0.05
        _RimLightAlign ("Rim Only On Lit Side", Range(0, 1)) = 0.3

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.1, 0.05, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 2
        _OutlineAlbedoMix ("Outline Uses Albedo Color", Range(0, 1)) = 0

        [Header(Art Style)]
        _ArtScale ("Pattern Scale", Float) = 4
        _ArtStrength ("Pattern Strength", Range(0, 1)) = 0.8
        _ArtColor ("Pattern Color", Color) = (1, 1, 1, 1)
        _ArtParam ("Style Param", Range(0, 1)) = 0.5
        _ArtFps ("Boil FPS", Range(0, 24)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "MarmartFXInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HSVert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "MarmartFXLighting.hlsl"
            #include "MarmartFXArt.hlsl"

            // Pixel: 3-level lighting with ordered (Bayer) dither on a pixel grid (pair with post pixelate).
            half4 Frag(HSVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 albedo = HS_SampleBase(i.uv).rgb;
                half3 N = normalize(i.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light ml = HS_MainLight(i.positionWS);
                half NdotL = dot(N, ml.direction);
                half shadow = HS_ReceivedShadow(ml);
                float2 sp = i.positionCS.xy;

                float2 cell = floor(sp / max((float)_ArtScale, 1.0));
                half b = HS_Bayer4(cell);
                half h = saturate(NdotL * 0.5h + 0.5h) * lerp(0.35h, 1.0h, shadow);
                half q = floor(h * 3.0h + lerp(0.5h, b, _ArtStrength)) / 3.0h;
                half3 color = albedo * lerp(_ShadowColor.rgb, half3(1.1h, 1.1h, 1.1h), q) * ml.color;
                color += step(0.5h, HS_Spec(N, V, ml.direction) * shadow) * _SpecularColor.rgb;
                color += SampleSH(N) * albedo * _AmbientStrength;
                color += HS_AdditionalLights(i.positionWS, i.positionCS, N, albedo);
                color += HS_Rim(N, V, ml.direction) * _RimColor.rgb;
                color += HS_Emission(i.uv);
                return half4(MixFog(color, i.fogFactor), 1);
            }
            ENDHLSL
        }

        UsePass "MarmartFX/StylizedToonLit/OUTLINE"
        UsePass "MarmartFX/StylizedToonLit/SHADOWCASTER"
        UsePass "MarmartFX/StylizedToonLit/DEPTHONLY"
        UsePass "MarmartFX/StylizedToonLit/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
