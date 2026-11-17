// MarmartFX: stylized multicolor paint-drip outline for spilled products.
// Inverted-hull outline extruded in screen space: blotchy width, colors picked from a 5-color
// palette in patches, and drips that run down from the lower edge and regrow over time.
// Drawn after post-processing (LightMode MarmartFXOverlay, see MarmartFXPostFXFeature).
Shader "MarmartFX/VFX/PaintDrip"
{
    Properties
    {
        _Color0 ("Paint 1", Color) = (1, 0.15, 0.35, 1)
        _Color1 ("Paint 2", Color) = (1, 0.8, 0.05, 1)
        _Color2 ("Paint 3", Color) = (0.1, 0.85, 0.35, 1)
        _Color3 ("Paint 4", Color) = (0.15, 0.55, 1, 1)
        _Color4 ("Paint 5", Color) = (0.75, 0.25, 1, 1)
        _Width ("Width", Range(0, 20)) = 5
        _Blotch ("Blotchiness", Range(0, 1)) = 0.6
        _PatchScale ("Color Patch Scale", Range(0.5, 40)) = 6
        _DripLength ("Drip Length", Range(0, 60)) = 18
        _DripDensity ("Drip Density", Range(1, 60)) = 14
        _DripSpeed ("Drip Speed", Range(0, 3)) = 0.4
        _BoilFps ("Boil FPS", Range(0, 24)) = 6
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PaintDrip"
            Tags { "LightMode" = "MarmartFXOverlay" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color0, _Color1, _Color2, _Color3, _Color4;
                float _Width, _Blotch, _PatchScale, _DripLength, _DripDensity, _DripSpeed, _BoilFps;
                half _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 seedPos : TEXCOORD0; float drip : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

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

                float3 objSeed = UNITY_MATRIX_M._m03_m13_m23 * 0.37; // per-object variation
                float3 p = v.positionOS.xyz;
                float frame = _BoilFps > 0 ? floor(_Time.y * _BoilFps) : 0;

                float4 posCS = TransformObjectToHClip(p);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float2 nCS = normalize(mul((float3x3)UNITY_MATRIX_VP, nWS).xy + 1e-5);
                float aspect = _ScreenParams.y / _ScreenParams.x;

                float n = Noise(p * _PatchScale * 1.7 + frame * 5.13 + objSeed);
                float width = _Width * max(0.2, 1.0 + _Blotch * (n * 2.0 - 1.0) * 1.3);
                float2 off = nCS * width;

                // Drips: on the lower silhouette, some columns run down the screen, regrowing over time.
                float down = saturate(-nCS.y * 1.5);
                float3 wp = TransformObjectToWorld(p);
                float col = floor((wp.x + wp.z) * _DripDensity + objSeed.x * 13.0);
                float sel = Hash(float3(col, objSeed.y, 7.7));
                float grow = frac(_Time.y * _DripSpeed * (0.6 + sel) + sel * 3.1);
                float drip = (sel > 0.55 ? (sel - 0.55) / 0.45 : 0.0) * down * smoothstep(0.0, 0.25, grow);
                off.y -= drip * _DripLength * grow;

                off.x *= aspect;
                posCS.xy += off * 0.002 * posCS.w;
                o.positionCS = posCS;
                o.seedPos = p * _PatchScale + objSeed;
                o.drip = drip;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Pick one of 5 paints per patch, so neighbouring blobs are different colors.
                float k = Noise(i.seedPos) * 0.7 + Noise(i.seedPos * 2.3 + 11.0) * 0.3;
                int idx = (int)floor(frac(k * 3.7) * 4.999);
                half4 c = idx == 0 ? _Color0 : idx == 1 ? _Color1 : idx == 2 ? _Color2 : idx == 3 ? _Color3 : _Color4;
                c.a = _Fade;
                return c;
            }
            ENDHLSL
        }
    }
}
