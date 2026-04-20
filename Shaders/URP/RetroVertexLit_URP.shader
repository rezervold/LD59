Shader "Retro 3D Shader Pack/URP/Vertex Lit"
{
    Properties
    {
        _MainTex("Albedo Texture", 2D) = "white" {}
        _Color("Albedo Color Tint", Color) = (1, 1, 1, 1)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SpecColor("Specular Color", Color) = (0, 0, 0, 1)
        _Glossiness("Smoothness", Range(0.01, 1.0)) = 0.5
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [HDR] _EmissionMap("Emission Map", 2D) = "black" {}

        _VertJitter("Vertex Jitter", Range(0.0, 0.999)) = 0.95
        _AffineMapIntensity("Affine Texture Mapping Intensity", Range(0.0, 1.0)) = 1.0
        _DrawDist("Draw Distance", Float) = 0
        
        [Toggle(ENABLE_SCREENSPACE_JITTER)] _EnableScreenSpaceJitter("Screen Space Jitter", Float) = 0
        [Toggle(USING_SPECULAR_MAP)] _UseSpecularMap("Use Specular Map", Float) = 0
        [Toggle(EMISSION_ENABLED)] _EmissionEnabled("Enable Emission", Float) = 0
        [Toggle(USING_EMISSION_MAP)] _UseEmissionMap("Use Emission Map", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma shader_feature_local ENABLE_SCREENSPACE_JITTER
            #pragma shader_feature_local USING_SPECULAR_MAP
            #pragma shader_feature_local EMISSION_ENABLED
            #pragma shader_feature_local USING_EMISSION_MAP

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _SpecColor;
                half _Glossiness;
                half4 _EmissionColor;
                float _VertJitter;
                float _AffineMapIntensity;
                float _DrawDist;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_SpecGlossMap);
            SAMPLER(sampler_SpecGlossMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uv_affine : TEXCOORD1;
                half3 diffuse : TEXCOORD2;
                half3 specular : TEXCOORD3;
                float drawDistClip : TEXCOORD4;
                float fogCoord : TEXCOORD5;
            };

            float4 ScreenSnap(float4 clipPos)
            {
                float geoRes = _VertJitter * 125.0f + 1.0f;
                float2 pixelPos = round((clipPos.xy / clipPos.w) * _ScreenParams.xy / geoRes) * geoRes;
                clipPos.xy = pixelPos / _ScreenParams.xy * clipPos.w;
                return clipPos;
            }

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS;
                
                #ifdef ENABLE_SCREENSPACE_JITTER
                    positionCS = TransformWorldToHClip(positionWS);
                    positionCS = ScreenSnap(positionCS);
                #else
                    float geoRes = (_VertJitter - 1.0f) * -1000.0f;
                    float3 viewPos = TransformWorldToView(positionWS);
                    viewPos.xyz = floor(viewPos.xyz * geoRes) / geoRes;
                    positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
                #endif
                
                output.positionCS = positionCS;
                output.uv = input.uv;
                
                float wVal = positionCS.w;
                output.uv_affine = float3(input.uv * wVal, wVal);

                output.drawDistClip = 0;
                if (_DrawDist != 0 && distance(positionWS, _WorldSpaceCameraPos) > _DrawDist)
                    output.drawDistClip = 1;

                half3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);
                output.diffuse = SampleSH(normalWS);
                output.specular = half3(0, 0, 0);

                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                output.diffuse += mainLight.color * NdotL;
                
                half3 halfDir = normalize(viewDir + mainLight.direction);
                half NdotH = saturate(dot(normalWS, halfDir));
                output.specular += mainLight.color * pow(NdotH, _Glossiness * 128.0) * 0.5;
                
                uint lightsCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightsCount; i++)
                {
                    Light light = GetAdditionalLight(i, positionWS);
                    NdotL = saturate(dot(normalWS, light.direction));
                    output.diffuse += light.color * NdotL * light.distanceAttenuation;
                    
                    halfDir = normalize(viewDir + light.direction);
                    NdotH = saturate(dot(normalWS, halfDir));
                    output.specular += light.color * pow(NdotH, _Glossiness * 128.0) * 0.5 * light.distanceAttenuation;
                }
                
                half4 emissionParameter = half4(0, 0, 0, 0);
                #ifdef EMISSION_ENABLED
                    emissionParameter = _EmissionColor;
                    #ifdef USING_EMISSION_MAP
                        emissionParameter *= SAMPLE_TEXTURE2D_LOD(_EmissionMap, sampler_EmissionMap, input.uv, 0);
                    #endif
                #endif
                output.diffuse = (output.diffuse * _Color.rgb + emissionParameter.rgb) * 2;
                
                half4 specularParameter = _SpecColor;
                #ifdef USING_SPECULAR_MAP
                    specularParameter = SAMPLE_TEXTURE2D_LOD(_SpecGlossMap, sampler_SpecGlossMap, input.uv, 0);
                #endif
                output.specular *= specularParameter.rgb * 2;
                
                output.fogCoord = ComputeFogFactor(positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (input.drawDistClip != 0)
                    clip(-1);

                float2 correctUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float2 affineBaseUV = input.uv_affine.xy / input.uv_affine.z;
                float2 affineUV = affineBaseUV * _MainTex_ST.xy + _MainTex_ST.zw;
                float2 finalUV = lerp(correctUV, affineUV, _AffineMapIntensity);
                
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);
                col.rgb = col.rgb * input.diffuse + input.specular;
                col.a = col.a * _Color.a;
                
                col.rgb = MixFog(col.rgb, input.fogCoord);
                
                return col;
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
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _SpecColor;
                half _Glossiness;
                half4 _EmissionColor;
                float _VertJitter;
                float _AffineMapIntensity;
                float _DrawDist;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
