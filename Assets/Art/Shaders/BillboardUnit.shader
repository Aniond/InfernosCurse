// HD-2D battle unit billboard: cylindrical billboard (sprite stays vertical,
// turns to face the camera), with DEPTH LOCKED AT THE PIVOT (feet) — every
// pixel writes the feet-line depth, so a rising slope in front of the feet
// never slices into the body and the sprite sorts against terrain as one
// plane. Works with a plain SpriteRenderer (pivot = object origin = feet).
// Alpha-clipped cutout + shadow caster: units cast real shadows like FFT.
Shader "InfernosCurse/BillboardUnit"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _LightWrap ("Light Wrap", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Cutoff;
                half _LightWrap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 pivotWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 pivotWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float sx = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float sy = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));

                // cylindrical billboard: camera right, world up
                float3 camRight = normalize(float3(UNITY_MATRIX_V._m00, UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m02));
                float3 worldPos = pivotWS
                    + camRight * (v.positionOS.x * sx)
                    + float3(0, 1, 0) * (v.positionOS.y * sy);

                o.positionCS = TransformWorldToHClip(worldPos);

                // depth-lock at the feet (tiny lift so the feet line itself
                // doesn't z-fight the ground triangle it stands on)
                float4 pivotCS = TransformWorldToHClip(pivotWS + float3(0, 0.03, 0));
                o.positionCS.z = pivotCS.z / pivotCS.w * o.positionCS.w;

                o.uv = v.uv;
                o.pivotWS = pivotWS;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * i.color;
                clip(tex.a - _Cutoff);

                // soft wrap lighting sampled at the pivot — the whole sprite
                // lights as one object, no fake surface normal
                float4 shadowCoord = TransformWorldToShadowCoord(i.pivotWS);
                Light mainLight = GetMainLight(shadowCoord);
                half wrap = saturate((dot(float3(0,1,0), mainLight.direction) + _LightWrap) / (1 + _LightWrap));
                half3 lighting = mainLight.color * (wrap * mainLight.shadowAttenuation)
                               + SampleSH(float3(0,1,0)) * 0.6 + 0.25;
                return half4(tex.rgb * lighting, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Cutoff;
                half _LightWrap;
            CBUFFER_END

            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V vertShadow (A v)
            {
                V o;
                float3 pivotWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float sx = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float sy = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                // shadow silhouette: face the LIGHT so the cast shape is the sprite
                float3 lightRight = normalize(cross(float3(0, 1, 0), _LightDirection));
                float3 worldPos = pivotWS
                    + lightRight * (v.positionOS.x * sx)
                    + float3(0, 1, 0) * (v.positionOS.y * sy);
                float3 nWS = -_LightDirection;
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = v.uv;
                return o;
            }

            half4 fragShadow (V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
