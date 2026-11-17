#ifndef MARMARTFX_INPUT_INCLUDED
#define MARMARTFX_INPUT_INCLUDED

// Shared material inputs for every MarmartFX/* shader. All shaders use the exact same
// UnityPerMaterial layout so materials can switch between styles and stay SRP-Batcher friendly.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half4 _ShadowColor;
    half4 _DeepShadowColor;
    half _ShadeThreshold;
    half _DeepShadeThreshold;
    half _ShadeSoftness;
    half _ReceiveShadowStrength;
    half _AmbientStrength;
    half _AddLightBands;
    half4 _SpecularColor;
    half _SpecularSize;
    half _SpecularSoftness;
    half4 _RimColor;
    half _RimThreshold;
    half _RimSoftness;
    half _RimLightAlign;
    float4 _EmissionMap_ST;
    half4 _EmissionColor;
    half4 _OutlineColor;
    float _OutlineWidth;
    half _OutlineAlbedoMix;

    // Candy Pop
    half _CandySaturation;
    half4 _CandyTopTint;
    half4 _CandyBottomTint;
    half _CandyGradientHeight;
    half _CandyGradientStrength;
    half4 _CandySheenColor;
    half _CandySheenWidth;

    // Toy Plastic
    half _ToyRampSteps;
    half _ToyGloss;
    half _ToyReflection;
    half4 _ToySecondSpecColor;

    // Comic Halftone
    half _ComicDotScale;
    half4 _ComicDotColor;
    half _ComicDotStrength;
    half _ComicHatchStrength;
    half _ComicFlatten;

    // Art styles (hand drawn, oil, watercolor, scribble, retro, pixel, poster, neon)
    half _ArtScale;
    half _ArtStrength;
    half4 _ArtColor;
    half _ArtParam;
    half _ArtFps;
CBUFFER_END

TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

void ApplyAlphaClip(float2 uv)
{
    #if defined(_ALPHATEST_ON)
    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
    clip(a - _Cutoff);
    #endif
}

#endif
