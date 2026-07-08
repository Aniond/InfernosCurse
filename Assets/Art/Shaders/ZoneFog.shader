// Fog-of-war overlay: transparent dark veil whose alpha comes from the
// per-cell visibility mask (bilinear = soft vision edges). Drawn on a
// ground-conforming mesh above the terrain.
Shader "InfernosCurse/ZoneFog"
{
    Properties
    {
        _FogMask ("Visibility Mask", 2D) = "white" {}
        _FogRect ("Fog Rect (xy origin, zw size)", Vector) = (0, 0, 32, 32)
        _FogColor ("Fog Color", Color) = (0.07, 0.09, 0.14, 0.8)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Fog"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FogMask); SAMPLER(sampler_FogMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _FogRect;
                half4 _FogColor;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            V vert (A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float2 uv = (i.positionWS.xz - _FogRect.xy) / _FogRect.zw;
                half vis = SAMPLE_TEXTURE2D(_FogMask, sampler_FogMask, uv).r;
                return half4(_FogColor.rgb, _FogColor.a * (1.0h - vis));
            }
            ENDHLSL
        }
    }
}
