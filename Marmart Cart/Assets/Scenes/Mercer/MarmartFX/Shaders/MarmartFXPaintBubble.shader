// MarmartFX: hand-drawn paint bubble for pickup loot (replaces the look of the game's fresnel sphere at runtime).
// Thick wobbly ink rim, faint tinted fill, a painted highlight crescent, and paint drips running off the
// bottom that keep regrowing. "Boils" a few times per second like redrawn frames.
// Drawn after post-processing (LightMode MarmartFXOverlay, see MarmartFXPostFXFeature).
Shader "MarmartFX/VFX/PaintBubble"
{
    Properties
    {
        _Color ("Paint Color", Color) = (0.35, 0.8, 1, 1)
        _RimColor ("Rim Color", Color) = (0.12, 0.35, 0.75, 1)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.18
        _RimWidth ("Rim Width", Range(0, 0.8)) = 0.28
        _RimJitter ("Rim Jitter", Range(0, 1)) = 0.5
        _HighlightSize ("Highlight Size", Range(0, 0.5)) = 0.16
        _Wobble ("Shape Wobble", Range(0, 0.3)) = 0.06
        _DripLength ("Drip Length", Range(0, 1.5)) = 0.45
        _DripCount ("Drip Columns", Range(3, 40)) = 14
        _DripSpeed ("Drip Speed", Range(0, 3)) = 0.35
        _BoilFps ("Boil FPS", Range(0, 24)) = 6
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PaintBubble"
            Tags { "LightMode" = "MarmartFXOverlay" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _RimColor;
                half _FillAlpha, _RimWidth, _RimJitter, _HighlightSize, _Wobble;
                float _DripLength, _DripCount, _DripSpeed, _BoilFps;
                half _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 noisePos : TEXCOORD2;
                float drip : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x); float3 f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 seed = UNITY_MATRIX_M._m03_m13_m23 * 0.71;
                float frame = _BoilFps > 0 ? floor(_Time.y * _BoilFps) : 0;
                float3 p = v.positionOS.xyz;
                float3 n = normalize(p + 1e-5);

                // Hand-drawn wobble of the whole shape.
                float w = Noise(n * 2.5 + frame * 3.7 + seed) - 0.5;
                p *= 1.0 + w * _Wobble * 2.0;

                // Drips: some longitude columns on the lower half sag down, regrowing over time.
                float ang = atan2(n.z, n.x) / (2.0 * PI) + 0.5;
                float col = floor(ang * _DripCount);
                float sel = Hash(float3(col, seed.x, seed.z));
                float grow = frac(_Time.y * _DripSpeed * (0.5 + sel) + sel * 5.3);
                float lower = smoothstep(-0.15, -0.95, n.y);
                float colCenter = 1.0 - abs(frac(ang * _DripCount) - 0.5) * 2.0; // 1 at column middle
                float drip = (sel > 0.5 ? 1.0 : 0.0) * lower * smoothstep(0.2, 0.9, colCenter);
                p.y -= drip * _DripLength * grow * 0.5; // object space sphere radius is 0.5
                p.xz *= 1.0 - drip * grow * 0.35;         // drips taper inward

                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.noisePos = n * 4.0 + seed + frame * 1.37;
                o.drip = drip * grow;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half ndv = saturate(dot(N, V));

                // Thick, uneven painted rim (hard edge, like a brush stroke).
                half jitter = (Noise(i.noisePos) - 0.5) * _RimJitter;
                half rimEdge = 1.0 - _RimWidth * (1.0 + jitter);
                half rim = step(rimEdge, 1.0 - ndv);
                // A second, thinner inner stroke for a sketchy double line.
                half inner = step(abs((1.0 - ndv) - rimEdge * 0.82), 0.025 * (1.0 + jitter));

                // Painted highlight crescent, upper-left in view.
                half3 camRight = UNITY_MATRIX_V[0].xyz;
                half3 camUp = UNITY_MATRIX_V[1].xyz;
                half3 H = normalize(V + camUp * 0.55 - camRight * 0.45);
                half spot = step(1.0 - _HighlightSize * (1.0 + jitter * 0.5), dot(N, H));

                half4 c = half4(_Color.rgb, _FillAlpha);
                c = lerp(c, half4(_Color.rgb, 0.85), inner * 0.8);
                c = lerp(c, half4(_RimColor.rgb, 1), rim);
                c = lerp(c, half4(1, 1, 1, 0.95), spot * (1.0 - rim));
                c = lerp(c, half4(_Color.rgb, 1), saturate(i.drip * 3.0)); // drips are solid paint
                c.a *= _Fade;
                return c;
            }
            ENDHLSL
        }
    }
}
