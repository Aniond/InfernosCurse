Shader "InfernosCurse/HybridZoneTerrain"
{
    Properties
    {
        [HideInInspector] _Control ("Control", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "white" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "white" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "white" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "white" {}
        [HideInInspector] _TerrainHolesTexture ("Holes", 2D) = "white" {}

        _LayerTint0 ("Layer 0 Tint", Color) = (0.78,0.86,0.68,1)
        _LayerTint1 ("Layer 1 Tint", Color) = (0.83,0.76,0.62,1)
        _LayerTint2 ("Layer 2 Tint", Color) = (0.66,0.61,0.45,1)
        _LayerTint3 ("Layer 3 Tint", Color) = (1,1,1,1)
        _UrbanVertexBlend ("Use Urban Vertex Blend", Range(0,1)) = 0
        _VertexBlendContrast ("Vertex Blend Contrast", Range(0.25,4)) = 1
        _BlendExponent ("Blend Exponent", Range(0.25,4)) = 1.15
        _MacroScale ("Macro Variation Scale", Float) = 0.055
        _MacroStrength ("Macro Variation Strength", Range(0,0.35)) = 0.10
        _ExposedTint ("Exposed Surface Tint", Color) = (1.04,1.01,0.92,1)
        _RecessTint ("Recess Tint", Color) = (0.72,0.82,0.86,1)
        _SlopeStrength ("Slope Strength", Range(0,0.65)) = 0.24
        _ElevationTintStrength ("Elevation Tint Strength", Range(0,0.35)) = 0.08
        _HeightMinMax ("Height Min Max", Vector) = (1,2.2,0,0)
        _AmbientBoost ("Ambient Boost", Range(0,1)) = 0.22
        _WetDarkening ("Wet Darkening", Range(0,0.65)) = 0.28
        _WetHighlight ("Wet Highlight", Range(0,0.35)) = 0.08
        _WetColor ("Wet Tint", Color) = (0.45,0.42,0.34,1)
        _LayerWetResponse ("Layer Wet Response", Vector) = (0.45,1,0.8,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-100" "TerrainCompatible"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
            TEXTURE2D(_Splat1); SAMPLER(sampler_Splat1);
            TEXTURE2D(_Splat2); SAMPLER(sampler_Splat2);
            TEXTURE2D(_Splat3); SAMPLER(sampler_Splat3);

            CBUFFER_START(UnityPerMaterial)
                float4 _Control_ST;
                float4 _Splat0_ST;
                float4 _Splat1_ST;
                float4 _Splat2_ST;
                float4 _Splat3_ST;
                half4 _LayerTint0;
                half4 _LayerTint1;
                half4 _LayerTint2;
                half4 _LayerTint3;
                half4 _ExposedTint;
                half4 _RecessTint;
                half4 _WetColor;
                float4 _HeightMinMax;
                float4 _LayerWetResponse;
                float _UrbanVertexBlend;
                float _VertexBlendContrast;
                float _BlendExponent;
                float _MacroScale;
                float _MacroStrength;
                float _SlopeStrength;
                float _ElevationTintStrength;
                float _AmbientBoost;
                float _WetDarkening;
                float _WetHighlight;
            CBUFFER_END

            float _GrassWetness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 controlUV : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float4 color : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.controlUV = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.color = input.color;
                return output;
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

            half4 frag(Varyings input) : SV_Target
            {
                float4 terrainWeights = SAMPLE_TEXTURE2D(_Control, sampler_Control, input.controlUV);
                float4 urbanWeights = float4(
                    saturate(input.color.b),
                    saturate(input.color.r),
                    saturate(input.color.g),
                    0.0);
                float4 weights = lerp(terrainWeights, urbanWeights, saturate(_UrbanVertexBlend));
                float contrast = lerp(_BlendExponent, _VertexBlendContrast, saturate(_UrbanVertexBlend));
                weights = pow(max(weights, 1e-4), contrast);
                weights /= max(dot(weights, 1.0), 1e-4);

                half3 c0 = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, input.controlUV * _Splat0_ST.xy + _Splat0_ST.zw).rgb * _LayerTint0.rgb;
                half3 c1 = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat1, input.controlUV * _Splat1_ST.xy + _Splat1_ST.zw).rgb * _LayerTint1.rgb;
                half3 c2 = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat2, input.controlUV * _Splat2_ST.xy + _Splat2_ST.zw).rgb * _LayerTint2.rgb;
                half3 c3 = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat3, input.controlUV * _Splat3_ST.xy + _Splat3_ST.zw).rgb * _LayerTint3.rgb;
                half3 albedo = c0 * weights.x + c1 * weights.y + c2 * weights.z + c3 * weights.w;

                float3 normalWS = normalize(input.normalWS);
                float slope = 1.0 - saturate(normalWS.y);
                float macro = (ValueNoise(input.positionWS.xz * _MacroScale) - 0.5) * 2.0;
                albedo *= 1.0 + macro * _MacroStrength;
                albedo *= lerp(1.0, _RecessTint.rgb, saturate(slope * _SlopeStrength));

                float heightRange = max(_HeightMinMax.y - _HeightMinMax.x, 0.001);
                float elevation = saturate((input.positionWS.y - _HeightMinMax.x) / heightRange);
                albedo *= lerp(1.0, _ExposedTint.rgb, elevation * _ElevationTintStrength);

                float wetness = saturate(_GrassWetness);
                float wetAffinity = dot(weights, _LayerWetResponse);
                float wetAmount = wetness * wetAffinity;
                albedo = lerp(albedo, albedo * _WetColor.rgb, wetAmount * _WetDarkening);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * (1.0 + _AmbientBoost);
                half3 lit = albedo * (ambient + mainLight.color * ndl * mainLight.shadowAttenuation);

                float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = SafeNormalize(viewDir + mainLight.direction);
                float wetSpec = pow(saturate(dot(normalWS, halfDir)), 48.0) * wetAmount * _WetHighlight;
                lit += mainLight.color * wetSpec * mainLight.shadowAttenuation;
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Terrain/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Terrain/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Terrain/Lit/DepthNormals"
    }

    Fallback "Universal Render Pipeline/Terrain/Lit"
}
