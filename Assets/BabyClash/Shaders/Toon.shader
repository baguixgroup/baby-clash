// Cartoon shading for Baby Clash (URP): two-tone ramp with soft terminator, real shadows, SSAO, rim light,
// a small specular "shine", vertex colours, a per-renderer accent colour (vertex alpha = 0 marks accent areas,
// e.g. the baby's shirt) and an inverted-hull outline.
Shader "BabyClash/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _AccentColor ("Accent Color (where vertex alpha is 0)", Color) = (1, 1, 1, 1)
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 1

        [Header(Shading)]
        _ShadeColor ("Shadow Tint", Color) = (0.8, 0.72, 0.9, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.0
        _ShadeSoftness ("Shade Softness", Range(0.001, 1)) = 0.12
        _AmbientStrength ("Shadow Fill Strength", Range(0, 2)) = 1.0

        [Header(Highlights)]
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.22
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _SpecStrength ("Shine Strength", Range(0, 1)) = 0.12
        _SpecSize ("Shine Size", Range(0.5, 1)) = 0.93
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.23, 0.15, 0.18, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.05)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Shared by every pass so the SRP Batcher can batch this shader.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _AccentColor;
            half _VertexColorStrength;
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _SpecStrength;
            half _SpecSize;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half Ramp(half nDotL)
            {
                return smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, nDotL);
            }

            // Cel lighting: 'lit' is the soft two-tone ramp times shadows/attenuation. The lit side shows the albedo at the
            // light's colour; the dark side is filled by the (tinted) ambient light, so colours never wash out.
            half3 ShadeLight(Light light, half3 albedo, half3 shadowFill, half3 normalWS, half3 viewDirWS, out half lit)
            {
                half nDotL = dot(normalWS, light.direction);
                lit = Ramp(nDotL) * light.shadowAttenuation * light.distanceAttenuation;
                half3 diffuse = albedo * lerp(shadowFill, light.color, lit);
                half3 halfDir = SafeNormalize(light.direction + viewDirWS);
                half shine = smoothstep(_SpecSize, _SpecSize + 0.02, dot(normalWS, halfDir)) * _SpecStrength * lit;
                return diffuse + shine * light.color;
            }

            half3 AddLight(Light light, half3 albedo, half3 normalWS)
            {
                half lit = Ramp(dot(normalWS, light.direction)) * light.shadowAttenuation * light.distanceAttenuation;
                return albedo * light.color * lit;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 vertexTint = lerp(1.0, input.color.rgb, _VertexColorStrength);
                half3 albedo = texel.rgb * _BaseColor.rgb * vertexTint;
                albedo = lerp(albedo, texel.rgb * _AccentColor.rgb, 1.0 - input.color.a);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half directOcclusion = 1.0;
                half indirectOcclusion = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    directOcclusion = ao.directAmbientOcclusion;
                    indirectOcclusion = ao.indirectAmbientOcclusion;
                #endif

                half3 shadowFill = SampleSH(normalWS) * _ShadeColor.rgb * _AmbientStrength * indirectOcclusion;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half mainLit;
                half3 color = ShadeLight(mainLight, albedo, shadowFill, normalWS, viewDirWS, mainLit);
                color *= lerp(1.0, directOcclusion, 0.5);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                        color += AddLight(GetAdditionalLight(dirIndex, input.positionWS), albedo, normalWS);
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        color += AddLight(GetAdditionalLight(lightIndex, input.positionWS), albedo, normalWS) * 0.6;
                    LIGHT_LOOP_END
                #endif

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * (0.35 + 0.65 * mainLit);
                color += _EmissionColor.rgb;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                // A zero-width outline collapses behind the surface instead of costing overdraw.
                if (_OutlineWidth <= 0.0)
                    output.positionCS = float4(0, 0, -1, 1);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
