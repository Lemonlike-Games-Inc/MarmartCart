// MarmartFX: ported from HadesStyle ("painterly action-RPG" lit shader) for Unity 6 / URP 17.
// Hard-banded two-tone shading with tinted shadows, colored rim light,
// hard specular, banded point lights and an inverted-hull outline.
Shader "MarmartFX/StylizedToonLit"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Shading Bands)]
        _ShadowColor ("Shadow Tint", Color) = (0.45, 0.32, 0.55, 1)
        _DeepShadowColor ("Deep Shadow Tint", Color) = (0.18, 0.12, 0.28, 1)
        _ShadeThreshold ("Light / Shadow Threshold", Range(-1, 1)) = 0.05
        _DeepShadeThreshold ("Shadow / Deep Shadow Threshold", Range(-1, 1)) = -0.45
        _ShadeSoftness ("Band Edge Softness", Range(0.001, 0.3)) = 0.02
        _ReceiveShadowStrength ("Received Shadow Strength", Range(0, 1)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.6

        [Header(Additional Lights)]
        _AddLightBands ("Point Light Band Strength", Range(0, 2)) = 1

        [Header(Specular)]
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 0.95, 0.85, 1)
        _SpecularSize ("Specular Size", Range(0, 1)) = 0.06
        _SpecularSoftness ("Specular Softness", Range(0.001, 0.2)) = 0.01

        [Header(Rim Light)]
        [HDR] _RimColor ("Rim Color", Color) = (1.6, 0.75, 0.35, 1)
        _RimThreshold ("Rim Width", Range(0, 1)) = 0.62
        _RimSoftness ("Rim Softness", Range(0.001, 0.3)) = 0.03
        _RimLightAlign ("Rim Only On Lit Side", Range(0, 1)) = 0.5

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.07, 0.03, 0.06, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 2.5
        _OutlineAlbedoMix ("Outline Uses Albedo Color", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "MarmartFXInput.hlsl"
        ENDHLSL

        // ------------------------------------------------------------------
        // Main lit pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Band(half x, half threshold)
            {
                return smoothstep(threshold - _ShadeSoftness, threshold + _ShadeSoftness, x);
            }

            // Main / directional light: lit, shadow and deep-shadow tones.
            half3 ShadeDirectional(Light light, half3 N, half3 V, half3 albedo, out half litMask)
            {
                half NdotL = dot(N, light.direction);
                half shadow = lerp(1.0h, smoothstep(0.4h, 0.6h, light.shadowAttenuation), _ReceiveShadowStrength);

                half lit = Band(NdotL, _ShadeThreshold) * shadow * light.distanceAttenuation;
                half deep = Band(NdotL, _DeepShadeThreshold);

                half3 shadowTone = lerp(albedo * _DeepShadowColor.rgb, albedo * _ShadowColor.rgb, deep);
                half3 color = lerp(shadowTone, albedo, lit) * light.color;

                half3 H = SafeNormalize(light.direction + V);
                half NdotH = saturate(dot(N, H));
                half specEdge = 1.0h - _SpecularSize * _SpecularSize;
                half spec = smoothstep(specEdge - _SpecularSoftness, specEdge + _SpecularSoftness, NdotH) * lit * step(0.001h, _SpecularSize);
                color += spec * _SpecularColor.rgb * light.color;

                litMask = lit;
                return color;
            }

            // Point / spot lights: two hard bands instead of a smooth falloff.
            half3 ShadeAdditional(Light light, half3 N, half3 albedo)
            {
                half NdotL = saturate(dot(N, light.direction));
                half energy = NdotL * light.distanceAttenuation * light.shadowAttenuation;
                half bands = (smoothstep(0.02h, 0.04h, energy) + smoothstep(0.25h, 0.28h, energy)) * 0.5h;
                return albedo * light.color * bands * _AddLightBands;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(baseSample.a - _Cutoff);
                #endif
                half3 albedo = baseSample.rgb;

                half3 N = normalize(i.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half4 shadowMask = half4(1, 1, 1, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = shadowCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = shadowMask;

                // Ambient (flattened so it doesn't fight the bands).
                half3 ambient = SampleSH(N) * albedo * _AmbientStrength;

                Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);
                half litMask;
                half3 color = ShadeDirectional(mainLight, N, V, albedo, litMask) + ambient;

                #if defined(_ADDITIONAL_LIGHTS) || defined(USE_FORWARD_PLUS) || defined(USE_CLUSTER_LIGHT_LOOP)
                uint pixelLightCount = GetAdditionalLightsCount();

                #if defined(USE_FORWARD_PLUS) || defined(USE_CLUSTER_LIGHT_LOOP)
                #if defined(URP_FP_DIRECTIONAL_LIGHTS_COUNT)
                for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    Light dirLight = GetAdditionalLight(dirIndex, i.positionWS, shadowMask);
                    color += ShadeAdditional(dirLight, N, albedo);
                }
                #endif
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    color += ShadeAdditional(addLight, N, albedo);
                LIGHT_LOOP_END
                #endif

                // Colored rim light, biased toward the lit side.
                half NdotV = saturate(dot(N, V));
                half rim = smoothstep(_RimThreshold - _RimSoftness, _RimThreshold + _RimSoftness, 1.0h - NdotV);
                half rimSide = saturate(dot(N, mainLight.direction) * 0.5h + 0.5h);
                rim *= lerp(1.0h, rimSide, _RimLightAlign);
                color += rim * _RimColor.rgb;

                // Emission.
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Inverted-hull outline (constant screen-space width)
        // ------------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 posCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float2 nCS = normalize(mul((float3x3)UNITY_MATRIX_VP, nWS).xy + 1e-5);
                float aspect = _ScreenParams.y / _ScreenParams.x;
                nCS.x *= aspect;
                // Width in (roughly) pixels at 1080p, independent of distance.
                posCS.xy += nCS * (_OutlineWidth * 0.002) * posCS.w;

                o.positionCS = posCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(posCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(baseSample.a - _Cutoff);
                #endif
                if (_OutlineWidth <= 0.0) discard;
                half3 outline = lerp(_OutlineColor.rgb, baseSample.rgb * _OutlineColor.rgb * 3.0, _OutlineAlbedoMix);
                return half4(MixFog(outline, i.fogFactor), 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Shadow caster
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - posWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = posCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target
            {
                ApplyAlphaClip(i.uv);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Depth only
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 DepthFrag(Varyings i) : SV_Target
            {
                ApplyAlphaClip(i.uv);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Depth + normals (used by SSAO and the ink-outline post effect)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DNVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 DNFrag(Varyings i) : SV_Target
            {
                ApplyAlphaClip(i.uv);
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                float2 remapped = saturate(oct * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
                #else
                return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
