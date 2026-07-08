// Battle-map diorama terrain: 3-way splat (grass/dirt/rock) weighted by
// vertex color RGB, with vertex alpha as a baked darkening factor for the
// diorama base cut. Rock is sampled triplanar so cliff walls don't stretch.
// Lighting: main light + shadows + SH ambient (COZY owns the light rig).
Shader "InfernosCurse/BattleTerrainSplat"
{
    Properties
    {
        _GrassTex ("Grass", 2D) = "white" {}
        _DirtTex ("Dirt", 2D) = "white" {}
        _RockTex ("Rock", 2D) = "white" {}
        _GrassTint ("Grass Tint", Color) = (1,1,1,1)
        _DirtTint ("Dirt Tint", Color) = (1,1,1,1)
        _RockTint ("Rock Tint", Color) = (1,1,1,1)
        _Tiling ("Tiling (texels per meter)", Float) = 0.35
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_DirtTex);  SAMPLER(sampler_DirtTex);
            TEXTURE2D(_RockTex);  SAMPLER(sampler_RockTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassTint;
                half4 _DirtTint;
                half4 _RockTint;
                float _Tiling;
                float _AmbientBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.color      = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float2 uvTop = i.positionWS.xz * _Tiling;

                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uvTop).rgb * _GrassTint.rgb;
                // dirt triplanar too — the diorama walls carry dirt weight
                float3 anD = abs(n);
                half3 dirtY = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uvTop).rgb;
                half3 dirtX = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, i.positionWS.zy * _Tiling).rgb;
                half3 dirtZ = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, i.positionWS.xy * _Tiling).rgb;
                half3 dirt  = (dirtY * anD.y + dirtX * anD.x + dirtZ * anD.z)
                              / max(anD.x + anD.y + anD.z, 1e-4) * _DirtTint.rgb;

                // triplanar rock: walls project along their facing axis
                float3 an = abs(n);
                half3 rockY = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvTop).rgb;
                half3 rockX = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, i.positionWS.zy * _Tiling).rgb;
                half3 rockZ = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, i.positionWS.xy * _Tiling).rgb;
                half3 rock  = (rockY * an.y + rockX * an.x + rockZ * an.z) / max(an.x + an.y + an.z, 1e-4) * _RockTint.rgb;

                half3 w = i.color.rgb;
                w /= max(w.r + w.g + w.b, 1e-4);
                half3 albedo = grass * w.r + dirt * w.g + rock * w.b;
                albedo *= i.color.a;   // diorama-base darkening

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction));
                half3 direct  = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(n) + _AmbientBoost;
                return half4(albedo * (direct + ambient), 1);
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

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V vertShadow (A v)
            {
                V o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 fragShadow (V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
