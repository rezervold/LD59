Shader "Retro 3D Shader Pack/URP/Unity Lit (Transparent)"
{
    Properties
    {
        _MainTex("Albedo Texture", 2D) = "white" {}
        _Color("Albedo Color Tint", Color) = (1, 1, 1, 1)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SpecularColor("Specular Color", Color) = (0, 0, 0, 1)
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.0
        _BumpMap("Normal Map", 2D) = "bump" {}
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
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local ENABLE_SCREENSPACE_JITTER
            #pragma shader_feature_local USING_SPECULAR_MAP
            #pragma shader_feature_local EMISSION_ENABLED
            #pragma shader_feature_local USING_EMISSION_MAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _SpecularColor;
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
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uv_affine : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                float drawDistClip : TEXCOORD5;
                float fogCoord : TEXCOORD6;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS;
                
                float geoRes = (_VertJitter - 1.0f) * -1000.0f;
                float3 viewPos = TransformWorldToView(positionWS);
                viewPos.xyz = floor(viewPos.xyz * geoRes) / geoRes;
                positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));

                float4x4 invView = UNITY_MATRIX_I_V;
                positionWS = mul(invView, float4(viewPos, 1.0)).xyz;
                
                output.positionCS = positionCS;
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = input.uv;
                
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), sign);

                float wVal = positionCS.w;
                output.uv_affine = float3(input.uv * wVal, wVal);

                output.drawDistClip = 0;
                if (_DrawDist != 0 && distance(positionWS, _WorldSpaceCameraPos) > _DrawDist)
                    output.drawDistClip = 1;
                
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

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * _Color;

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, finalUV));
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, tangentToWorld));

                half4 specular = _SpecularColor;
                #ifdef USING_SPECULAR_MAP
                    specular = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, finalUV);
                #endif
                
                half3 emission = half3(0, 0, 0);
                #ifdef EMISSION_ENABLED
                    emission = _EmissionColor.rgb;
                    #ifdef USING_EMISSION_MAP
                        emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, finalUV).rgb;
                    #endif
                #endif
                
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);

                Light mainLight = GetMainLight();
                
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * NdotL;
                
                half3 halfDir = normalize(viewDirWS + mainLight.direction);
                half NdotH = saturate(dot(normalWS, halfDir));
                half3 spec = mainLight.color * pow(NdotH, _Glossiness * 128.0 + 1.0) * specular.rgb;
                
                uint lightsCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightsCount; i++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    NdotL = saturate(dot(normalWS, light.direction));
                    diffuse += light.color * NdotL * light.distanceAttenuation;
                    
                    halfDir = normalize(viewDirWS + light.direction);
                    NdotH = saturate(dot(normalWS, halfDir));
                    spec += light.color * pow(NdotH, _Glossiness * 128.0 + 1.0) * specular.rgb * light.distanceAttenuation;
                }
                
                half3 ambient = SampleSH(normalWS);
                
                half4 col;
                col.rgb = albedo.rgb * (diffuse + ambient) + spec + emission;
                col.a = albedo.a;
                
                col.rgb = MixFog(col.rgb, input.fogCoord);
                
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
