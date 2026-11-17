// MarmartFX: writes the player's id into the stencil wherever their cart is visible, drawing no color.
// Rendered just before the highlight (render queue 2999) so the outline pass can skip pixels on the cart
// and only the outer silhouette gets a line.
Shader "Hidden/MarmartFX/VFX/HighlightMask"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Int) = 16
        _MaskExpand ("Mask Expand (px-ish, closes gaps in wire meshes)", Range(0, 40)) = 10
        _UseSmoothNormal ("Use Smoothed Normals (UV4)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Stencil { Ref [_StencilRef] ReadMask 240 WriteMask 240 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _MaskExpand;
                float _UseSmoothNormal;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float3 smoothNormalOS : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 posCS = TransformObjectToHClip(v.positionOS.xyz);
                // Grow the mask in screen space (same way as the outline) so the gaps between thin
                // wires are covered and only the outer silhouette gets a line.
                float3 nOS = (_UseSmoothNormal > 0.5 && dot(v.smoothNormalOS, v.smoothNormalOS) > 0.01) ? v.smoothNormalOS : v.normalOS;
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float2 nCS = normalize(mul((float3x3)UNITY_MATRIX_VP, nWS).xy + 1e-5);
                nCS.x *= _ScreenParams.y / _ScreenParams.x;
                posCS.xy += nCS * (_MaskExpand * 0.002) * posCS.w;
                o.positionCS = posCS;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
