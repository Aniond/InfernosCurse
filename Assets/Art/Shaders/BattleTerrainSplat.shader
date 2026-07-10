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
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.22
        // Curse mask is fed per-battle by BattleTerrainCurse via property
        // block; black default = uncursed.
        _CurseMask ("Curse Mask", 2D) = "black" {}
        _CurseRect ("Curse Rect (xy origin, zw size)", Vector) = (0, 0, 14, 12)
        _CurseColor ("Curse Color", Color) = (0.22, 0.06, 0.28, 1)
        _CurseGlow ("Curse Glow", Color) = (0.45, 0.10, 0.55, 1)
        // 4th layer (paths/cobbles) — weight rides vertex alpha; slope shade
        // moved to UV2.x. Shoreline wetness darkens below _WaterLevel+band.
        _PathTex ("Path", 2D) = "white" {}
        _PathTint ("Path Tint", Color) = (1, 1, 1, 1)
        _BlendSoftness ("Material Blend Softness", Range(0, 1)) = 0.62
        _MacroScale ("Macro Variation Scale", Float) = 0.055
        _MacroStrength ("Macro Variation Strength", Range(0, 0.35)) = 0.11
        _ExposedTint ("Exposed Surface Tint", Color) = (1.06, 1.01, 0.90, 1)
        _RecessTint ("Recess Tint", Color) = (0.72, 0.82, 0.88, 1)
        _FoldStrength ("Fold Strength", Range(0, 0.65)) = 0.28
        _ElevationTintStrength ("Elevation Tint Strength", Range(0, 0.35)) = 0.10
        _HeightMinMax ("Generated Height Min Max", Vector) = (-3.2, 2, 0, 0)
        _WetDarkening ("Wet Darkening", Range(0, 0.65)) = 0.30
        _WetHighlight ("Wet Highlight", Range(0, 0.35)) = 0.10
        _WaterLevel ("Water Level (world Y, -100 = off)", Float) = -100
        _WetColor ("Wet Tint", Color) = (0.45, 0.42, 0.34, 1)
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
            TEXTURE2D(_CurseMask); SAMPLER(sampler_CurseMask);
            TEXTURE2D(_PathTex); SAMPLER(sampler_PathTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassTint;
                half4 _DirtTint;
                half4 _RockTint;
                half4 _PathTint;
                half4 _CurseColor;
                half4 _CurseGlow;
                half4 _WetColor;
                half4 _ExposedTint;
                half4 _RecessTint;
                float4 _CurseRect;
                float4 _HeightMinMax;
                float _Tiling;
                float _AmbientBoost;
                float _WaterLevel;
                float _BlendSoftness;
                float _MacroScale;
                float _MacroStrength;
                float _FoldStrength;
                float _ElevationTintStrength;
                float _WetDarkening;
                float _WetHighlight;
            CBUFFER_END

            float _GrassWetness;
            float _CorruptionEnabled;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;      // rgb = grass/dirt/rock, a = path
                float2 uv2        : TEXCOORD1;  // x = slope shade, y = fold/cavity
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                float2 detail     : TEXCOORD2;  // x = shade, y = cavity
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.color      = v.color;
                o.detail.x   = v.uv2.x <= 0.001 ? 1.0 : v.uv2.x;  // old meshes: no uv2
                o.detail.y   = saturate(v.uv2.y);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
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

                half3 path = SAMPLE_TEXTURE2D(_PathTex, sampler_PathTex, uvTop).rgb * _PathTint.rgb;
                half exponent = lerp(2.2h, 0.72h, saturate(_BlendSoftness));
                half pw = pow(saturate(i.color.a), exponent);
                half3 w = pow(saturate(i.color.rgb), exponent) * (1.0h - pw);
                half sum = w.r + w.g + w.b + pw;
                w /= max(sum, 1e-4);
                pw /= max(sum, 1e-4);
                half3 albedo = grass * w.r + dirt * w.g + rock * w.b + path * pw;
                albedo *= i.detail.x;     // slope/diorama darkening (from UV2)

                // Broad painterly breakup is stable in world space and much
                // larger than the material texture tiling.
                half macro = ValueNoise(i.positionWS.xz * max(_MacroScale, 0.001));
                albedo *= lerp(1.0h - _MacroStrength, 1.0h + _MacroStrength, macro);

                half heightRange = max(_HeightMinMax.y - _HeightMinMax.x, 0.001);
                half height01 = saturate((i.positionWS.y - _HeightMinMax.x) / heightRange);
                half cavity = i.detail.y;
                albedo = lerp(albedo, albedo * _ExposedTint.rgb,
                              height01 * (1.0h - cavity) * _ElevationTintStrength);
                albedo = lerp(albedo, albedo * _RecessTint.rgb,
                              cavity * _FoldStrength);
                albedo *= 1.0h - cavity * _FoldStrength * 0.35h;

                // shoreline wetness: darken + tint just above the waterline
                half wet = saturate((_WaterLevel + 0.35 - i.positionWS.y) / 0.35);
                albedo = lerp(albedo, albedo * _WetColor.rgb * 1.6, wet * 0.8);

                half materialWetResponse = w.r * 0.25h + w.g * 0.82h + w.b * 0.68h + pw * 0.92h;
                half weatherWet = saturate(_GrassWetness) * materialWetResponse;
                albedo *= 1.0h - weatherWet * _WetDarkening;

                // Curse creep: mask texel per cell, softened by bilinear +
                // veined by the dirt texture's own noise, breathing slowly.
                float2 cuv = (i.positionWS.xz - _CurseRect.xy) / _CurseRect.zw;
                half curse = SAMPLE_TEXTURE2D(_CurseMask, sampler_CurseMask, cuv).r * saturate(_CorruptionEnabled);
                float2 edge = abs(cuv - 0.5) * 2.0;
                curse *= saturate((1.0 - max(edge.x, edge.y)) * 8.0);   // don't smear past the field
                if (curse > 0.001)
                {
                    half vein = smoothstep(0.35, 0.8, dirtY.g + dirtY.r * 0.5);
                    half pulse = 0.75 + 0.25 * sin(_Time.y * 1.6 + i.positionWS.x + i.positionWS.z);
                    half3 cursed = lerp(_CurseColor.rgb, _CurseGlow.rgb, vein * pulse);
                    albedo = lerp(albedo, cursed, saturate(curse * (0.75 + 0.25 * vein)));
                }

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction));
                half3 direct  = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(n) + _AmbientBoost;
                half3 lit = albedo * (direct + ambient);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half wetSpec = pow(saturate(dot(n, halfDir)), 48.0h) * weatherWet *
                               _WetHighlight * mainLight.shadowAttenuation;
                lit += mainLight.color * wetSpec;
                lit += _CurseGlow.rgb * (curse * curse * 0.12);          // faint unlit ember
                return half4(lit, 1);
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
