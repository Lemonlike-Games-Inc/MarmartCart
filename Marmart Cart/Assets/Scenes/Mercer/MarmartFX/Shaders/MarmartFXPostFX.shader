Shader "Hidden/MarmartFX/PostFX"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "MarmartFX PostFX"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Intensity;
            TEXTURE2D(_MarmartFXExcludeMask);   // 1 = object excluded from the style filter (carts)
            float _MarmartFXExcludeOn;
            float4 _OutlineColor;   // rgb ink color, a ink strength
            float4 _OutlineParams;  // x thickness, y depth threshold, z normal threshold, w fade distance
            float4 _PaintParams;    // x posterize steps, y shadow hue strength
            float4 _ShadowHue;
            float4 _StyleParams0;   // x pixel size, y kuwahara radius, z watercolor bleed, w line wobble (px)
            float4 _StyleParams1;   // x wobble fps, y wobble scale, z paper strength, w paper scale
            float4 _StyleParams2;   // x film grain, y scanlines, z misregistration (px), w dither

            static const float kBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Bayer4(float2 px)
            {
                int2 p = (int2)fmod(floor(px), 4.0);
                return (kBayer[p.y * 4 + p.x] + 0.5) / 16.0;
            }

            half3 SampleCol(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half3 Kuwahara(float2 uv, int r, float2 texel)
            {
                half3 m0 = 0, m1 = 0, m2 = 0, m3 = 0;
                half3 s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                [loop] for (int j = -r; j <= r; j++)
                {
                    [loop] for (int i = -r; i <= r; i++)
                    {
                        half3 c = SampleCol(uv + float2(i, j) * texel);
                        half3 cc = c * c;
                        if (i <= 0 && j <= 0) { m0 += c; s0 += cc; }
                        if (i >= 0 && j <= 0) { m1 += c; s1 += cc; }
                        if (i <= 0 && j >= 0) { m2 += c; s2 += cc; }
                        if (i >= 0 && j >= 0) { m3 += c; s3 += cc; }
                    }
                }
                half n = (half)((r + 1) * (r + 1));
                m0 /= n; m1 /= n; m2 /= n; m3 /= n;
                half3 v0 = abs(s0 / n - m0 * m0), v1 = abs(s1 / n - m1 * m1);
                half3 v2 = abs(s2 / n - m2 * m2), v3 = abs(s3 / n - m3 * m3);
                half q0 = v0.r + v0.g + v0.b, q1 = v1.r + v1.g + v1.b;
                half q2 = v2.r + v2.g + v2.b, q3 = v3.r + v3.g + v3.b;
                half3 best = m0; half bq = q0;
                if (q1 < bq) { best = m1; bq = q1; }
                if (q2 < bq) { best = m2; bq = q2; }
                if (q3 < bq) { best = m3; }
                return best;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 res = _ScreenParams.xy;
                float2 texel = 1.0 / res;
                float2 uvSrc = input.texcoord;
                half3 src = SampleCol(uvSrc);

                // Excluded objects (carts) keep their own shading: no style filter, no ink lines.
                float exC = _MarmartFXExcludeOn > 0.5 ? SAMPLE_TEXTURE2D_LOD(_MarmartFXExcludeMask, sampler_PointClamp, uvSrc, 0).r : 0.0;
                if (exC > 0.5) return half4(src, 1.0);

                // --- Pixelate ---
                float px = _StyleParams0.x;
                float2 uv = uvSrc;
                float2 ditherPx = uvSrc * res;
                if (px > 1.5)
                {
                    float2 block = floor(uvSrc * res / px);
                    uv = (block + 0.5) * px / res;
                    ditherPx = block;
                    texel *= px;
                }

                // --- Hand-drawn wobble (boils at N fps) ---
                float wob = _StyleParams0.w;
                if (wob > 0.001)
                {
                    float frame = _StyleParams1.x > 0.001 ? floor(_Time.y * _StyleParams1.x) : 0.0;
                    float2 np = uvSrc * res / _StyleParams1.y;
                    float2 n2 = float2(VNoise(np + frame * 13.1), VNoise(np + 71.7 + frame * 7.3)) - 0.5;
                    uv += n2 * 2.0 * wob / res;
                }

                // --- Base color: oil paint (Kuwahara) or plain ---
                int kr = (int)_StyleParams0.y;
                half3 col = kr > 0 ? Kuwahara(uv, kr, 1.0 / res) : SampleCol(uv);

                // --- Watercolor bleed ---
                float bleed = _StyleParams0.z;
                if (bleed > 0.001)
                {
                    float2 np = uv * res / 18.0;
                    float2 dir = (float2(VNoise(np), VNoise(np + 31.4)) - 0.5) * 2.0;
                    half3 acc = col;
                    [unroll] for (int k = 1; k <= 4; k++)
                        acc += SampleCol(uv + dir * k * 2.0 * bleed / res);
                    col = lerp(col, acc / 5.0, bleed);
                }

                // --- Print misregistration / chromatic offset ---
                float mis = _StyleParams2.z;
                if (mis > 0.001)
                {
                    col.r = SampleCol(uv + float2(mis, mis * 0.5) / res).r;
                    col.b = SampleCol(uv - float2(mis * 0.5, mis) / res).b;
                }

                // --- Ink outline from depth + normals ---
                float2 o = _OutlineParams.x * texel;
                float2 uv0 = uv + float2(-o.x, -o.y);
                float2 uv1 = uv + float2( o.x,  o.y);
                float2 uv2 = uv + float2( o.x, -o.y);
                float2 uv3 = uv + float2(-o.x,  o.y);
                float dC = EyeDepth(uv);
                float depthDiff = (abs(EyeDepth(uv0) - EyeDepth(uv1)) + abs(EyeDepth(uv2) - EyeDepth(uv3))) / max(dC, 1e-3);
                float depthEdge = smoothstep(_OutlineParams.y, _OutlineParams.y * 1.5, depthDiff);
                float normalDiff = length(SampleSceneNormals(uv0) - SampleSceneNormals(uv1)) + length(SampleSceneNormals(uv2) - SampleSceneNormals(uv3));
                float normalEdge = smoothstep(_OutlineParams.z, _OutlineParams.z * 1.3, normalDiff);
                float edge = max(depthEdge, normalEdge) * (1.0 - saturate(dC / _OutlineParams.w));
                if (_MarmartFXExcludeOn > 0.5)
                {
                    // No ink line around excluded objects either (the edge taps that land on them).
                    float ex = max(max(SAMPLE_TEXTURE2D_LOD(_MarmartFXExcludeMask, sampler_PointClamp, uv0, 0).r,
                                       SAMPLE_TEXTURE2D_LOD(_MarmartFXExcludeMask, sampler_PointClamp, uv1, 0).r),
                                   max(SAMPLE_TEXTURE2D_LOD(_MarmartFXExcludeMask, sampler_PointClamp, uv2, 0).r,
                                       SAMPLE_TEXTURE2D_LOD(_MarmartFXExcludeMask, sampler_PointClamp, uv3, 0).r));
                    edge *= 1.0 - ex;
                }

                // --- Paint: lift shadows toward a hue ---
                half luma = dot(col, half3(0.2126, 0.7152, 0.0722));
                half darkMask = 1.0 - smoothstep(0.0, 0.45, luma);
                col += _ShadowHue.rgb * 0.3 * darkMask * _PaintParams.y;

                // --- Posterize with optional ordered dither ---
                if (_PaintParams.x > 0.5)
                {
                    half steps = _PaintParams.x;
                    half d = lerp(0.5, Bayer4(ditherPx), _StyleParams2.w);
                    col = floor(col * steps + d) / steps;
                }

                col = lerp(col, _OutlineColor.rgb, edge * _OutlineColor.a);

                // --- Paper texture ---
                float paper = _StyleParams1.z;
                if (paper > 0.001)
                {
                    float2 pp = uvSrc * res / _StyleParams1.w;
                    float pn = VNoise(pp) * 0.6 + VNoise(pp * 4.0) * 0.4;
                    col *= lerp(1.0, 0.82 + 0.36 * pn, paper);
                }

                // --- Film grain ---
                if (_StyleParams2.x > 0.0001)
                    col += (Hash21(uvSrc * res + frac(_Time.y) * 113.0) - 0.5) * _StyleParams2.x;

                // --- Scanlines ---
                if (_StyleParams2.y > 0.001)
                    col *= 1.0 - _StyleParams2.y * 0.6 * (0.5 + 0.5 * sin(uvSrc.y * res.y * PI));

                return half4(lerp(src, max(col, 0.0), _Intensity), 1.0);
            }
            ENDHLSL
        }
    }
}
