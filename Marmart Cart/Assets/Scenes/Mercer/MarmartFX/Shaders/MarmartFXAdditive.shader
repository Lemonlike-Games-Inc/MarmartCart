// MarmartFX: unlit additive shader for trails, particles, shock rings and popups (URP).
Shader "MarmartFX/VFX/Additive"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Enum(Ribbon,0,SoftDot,1,Ring,2,Star,3,Plain,4)] _Shape ("Shape", Float) = 0
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.5
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.12
        _MainTex ("Texture (optional)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "MarmartFXAdditive"
            // Drawn after post-processing by MarmartFXPostFXFeature, so scene filters don't touch it.
            Tags { "LightMode" = "MarmartFXOverlay" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Shape;
                half _Softness;
                half _RingWidth;
                float4 _MainTex_ST;
            CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half a = 1;
                float2 c = i.uv * 2 - 1;
                float r = length(c);
                if (_Shape < 0.5)        // ribbon: soft across the width (uv.y)
                    a = 1 - smoothstep(1 - _Softness, 1, abs(i.uv.y * 2 - 1));
                else if (_Shape < 1.5)   // soft dot
                    a = 1 - smoothstep(1 - _Softness, 1, r);
                else if (_Shape < 2.5)   // ring
                    a = 1 - smoothstep(0, _RingWidth, abs(r - (1 - _RingWidth)));
                else if (_Shape < 3.5)   // 4-point star
                {
                    float star = min(abs(c.x), abs(c.y)) * 6 + r * 1.2;
                    a = saturate(1 - star) + (1 - smoothstep(0, 0.35, r)) * 0.6;
                }
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(i.uv, _MainTex));
                half4 col = _BaseColor * i.color * tex;
                col.a *= saturate(a);
                // No tonemapping after post: fold HDR overdrive into alpha instead of clipping to white,
                // so the player's hue stays fully saturated.
                half m = max(max(col.r, col.g), max(col.b, 1.0h));
                col.rgb /= m;
                col.a = saturate(col.a * m);
                return col;
            }
            ENDHLSL
        }
    }
}
