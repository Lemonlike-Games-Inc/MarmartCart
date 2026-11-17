#ifndef MARMARTFX_LIGHTING_INCLUDED
#define MARMARTFX_LIGHTING_INCLUDED

// Shared forward-pass plumbing for the party shaders (include after MarmartFXInput.hlsl).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct HSAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HSVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    half   fogFactor  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HSVaryings HSVert(HSAttributes v)
{
    HSVaryings o = (HSVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
    return o;
}

half4 HS_SampleBase(float2 uv)
{
    half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
    #if defined(_ALPHATEST_ON)
    clip(c.a - _Cutoff);
    #endif
    return c;
}

Light HS_MainLight(float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    return GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
}

half HS_ReceivedShadow(Light light)
{
    return lerp(1.0h, smoothstep(0.4h, 0.6h, light.shadowAttenuation), _ReceiveShadowStrength) * light.distanceAttenuation;
}

half HS_Step(half x, half threshold, half softness)
{
    return smoothstep(threshold - softness, threshold + softness, x);
}

half HS_Rim(half3 N, half3 V, half3 L)
{
    half rim = HS_Step(1.0h - saturate(dot(N, V)), _RimThreshold, _RimSoftness);
    half side = saturate(dot(N, L) * 0.5h + 0.5h);
    return rim * lerp(1.0h, side, _RimLightAlign);
}

half HS_Spec(half3 N, half3 V, half3 L)
{
    half3 H = SafeNormalize(L + V);
    half edge = 1.0h - _SpecularSize * _SpecularSize;
    return HS_Step(saturate(dot(N, H)), edge, _SpecularSoftness) * step(0.001h, _SpecularSize);
}

half3 HS_BandedLight(Light light, half3 N, half3 albedo)
{
    half energy = saturate(dot(N, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
    half bands = (smoothstep(0.02h, 0.04h, energy) + smoothstep(0.25h, 0.28h, energy)) * 0.5h;
    return albedo * light.color * bands * _AddLightBands;
}

// Point / spot (and extra directional) lights, banded.
half3 HS_AdditionalLights(float3 positionWS, float4 positionCS, half3 N, half3 albedo)
{
    half3 c = 0;
    // URP 17 Forward+ does not set _ADDITIONAL_LIGHTS, so also accept the Forward+/cluster paths.
    #if defined(_ADDITIONAL_LIGHTS) || defined(USE_FORWARD_PLUS) || defined(USE_CLUSTER_LIGHT_LOOP)
    half4 shadowMask = half4(1, 1, 1, 1);
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = N;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    uint pixelLightCount = GetAdditionalLightsCount();

    #if defined(USE_FORWARD_PLUS) || defined(USE_CLUSTER_LIGHT_LOOP)
    #if defined(URP_FP_DIRECTIONAL_LIGHTS_COUNT)
    for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light dirLight = GetAdditionalLight(dirIndex, positionWS, shadowMask);
        c += HS_BandedLight(dirLight, N, albedo);
    }
    #endif
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light addLight = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        c += HS_BandedLight(addLight, N, albedo);
    LIGHT_LOOP_END
    #endif
    return c;
}

half3 HS_Emission(float2 uv)
{
    return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
}

#endif
