// MarmartFX: player highlight (screen-space-width outline + additive fresnel glow) for URP.
// Drawn on runtime copies of the cart meshes, so the cart's own materials are untouched.
Shader "MarmartFX/VFX/PlayerHighlight"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 20)) = 3
        _OutlineBase ("Outline Base Offset (= mask expand)", Range(0, 40)) = 0
        _WobbleAmount ("Sketch Wobble (width jitter)", Range(0, 1)) = 0
        _WobbleFps ("Sketch Boil FPS", Range(1, 24)) = 8
        _WobbleFreq ("Sketch Noise Scale", Range(0.5, 30)) = 6
        _Misregister ("Sketch Offset (outside the lines)", Range(0, 5)) = 0
        _UseSmoothNormal ("Use Smoothed Normals (UV4)", Float) = 0
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1
        _GlowPower ("Glow Power", Range(0.5, 8)) = 2.5
        _FillIntensity ("Fill Intensity", Range(0, 1)) = 0.05
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse Speed", Float) = 3
        _Fade ("Fade", Range(0, 1)) = 1
        _StencilRef ("Stencil Ref", Int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp (NotEqual = silhouette only)", Int) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineBase;
            float _WobbleAmount;
            float _WobbleFps;
            float _WobbleFreq;
            float _Misregister;
            float _UseSmoothNormal;
            half4 _GlowColor;
            half _GlowIntensity;
            half _GlowPower;
            half _FillIntensity;
            half _PulseAmount;
            half _PulseSpeed;
            half _Fade;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float3 smoothNormalOS : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
        float MFXHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
        float MFXNoise(float3 x)
        {
            float3 i = floor(x); float3 f = frac(x); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(MFXHash(i), MFXHash(i + float3(1,0,0)), f.x), lerp(MFXHash(i + float3(0,1,0)), MFXHash(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(MFXHash(i + float3(0,0,1)), MFXHash(i + float3(1,0,1)), f.x), lerp(MFXHash(i + float3(0,1,1)), MFXHash(i + float3(1,1,1)), f.x), f.y), f.z);
        }
        half Pulse() { return lerp(1.0h, 0.5h + 0.5h * sin(_Time.y * _PulseSpeed), _PulseAmount); }
        ENDHLSL

        Pass
        {
            Name "Outline"
            // Drawn by MarmartFXPostFXFeature after post-processing, so filters/grading don't dull it.
            Tags { "LightMode" = "MarmartFXOverlay" }
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref [_StencilRef] ReadMask 240 WriteMask 0 Comp [_StencilComp] }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 posCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 nOS = (_UseSmoothNormal > 0.5 && dot(v.smoothNormalOS, v.smoothNormalOS) > 0.01) ? v.smoothNormalOS : v.normalOS;
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float2 nCS = normalize(mul((float3x3)UNITY_MATRIX_VP, nWS).xy + 1e-5);
                float aspect = _ScreenParams.y / _ScreenParams.x;
                nCS.x *= aspect;
                // Hand-drawn "boil": the noise only changes a few times per second, like redrawn frames.
                float frame = floor(_Time.y * _WobbleFps);
                float n = MFXNoise(v.positionOS.xyz * _WobbleFreq + frame * 7.31);
                float n2 = MFXNoise(v.positionOS.xyz * _WobbleFreq * 2.7 - frame * 3.17);
                float width = _OutlineWidth * max(0.15, 1.0 + _WobbleAmount * ((n * 0.7 + n2 * 0.3) * 2.0 - 1.0) * 1.2);
                posCS.xy += nCS * ((width + _OutlineBase) * 0.002) * posCS.w;
                // Whole stroke slips off the shape a little each frame ("colored outside the lines").
                float2 slip = float2(MFXHash(float3(frame, 1.7, 3.1)), MFXHash(float3(frame, 5.3, 9.7))) * 2.0 - 1.0;
                slip.x *= aspect;
                posCS.xy += slip * (_Misregister * 0.002) * posCS.w;
                o.positionCS = posCS;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                if (_OutlineWidth <= 0.0) discard;
                half4 c = _OutlineColor;
                c.a *= _Fade * saturate(0.75h + 0.25h * Pulse());
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "MarmartFXOverlayGlow" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0h - saturate(dot(N, V)), _GlowPower);
                half k = (rim * _GlowIntensity + _FillIntensity) * Pulse() * _Fade;
                return half4(_GlowColor.rgb * k, 0);
            }
            ENDHLSL
        }
    }
}
