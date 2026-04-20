Shader "Custom/RoomEnvironmentSimpleLit"
{
    Properties
    {
        [Header(Base Textures)]
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base Texture", 2D) = "white" {}
        _Tiling ("World Space Tiling (X, Y)", Vector) = (1.0, 1.0, 0, 0)
        _Offset ("World Space Offset (X, Y, Z)", Vector) = (0, 0, 0, 0)
        
        [Header(Dirt Settings)]
        _DirtTex ("Dirt Texture", 2D) = "white" {}
        _DirtColor ("Dirt Color", Color) = (0.2, 0.15, 0.1, 1)
        _DirtSpread ("Dirt Spread (Lower = thicker, Higher = thinner)", Range(1.0, 30.0)) = 5.0
        
        [Header(Environment Type)]
        [Toggle(IS_FLOOR)] _IsFloor ("Is Floor? (On = all 4 edges, Off = bottom only)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local IS_FLOOR
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            
            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float fogCoord      : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float4 _Tiling;
                float4 _Offset;
                float4 _DirtTex_ST;
                float4 _DirtColor;
                float _DirtSpread;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                
                // Triplanar
                float3 blendWeights = abs(normalWS);
                blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);
                
                float3 offsetPos = input.positionWS + _Offset.xyz;
                float2 uvX = offsetPos.zy * _Tiling.xy;
                float2 uvY = offsetPos.xz * _Tiling.xy;
                float2 uvZ = offsetPos.xy * _Tiling.xy;
                
                float4 colX = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvX);
                float4 colY = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvY);
                float4 colZ = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZ);
                
                float4 albedo = (colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z) * _BaseColor;

                // Dirt Mask
                float mask = 0;
                #if IS_FLOOR
                    float distU = abs(input.uv.x - 0.5) * 2.0; 
                    float distV = abs(input.uv.y - 0.5) * 2.0;
                    mask = pow(saturate(max(distU, distV)), _DirtSpread);
                #else
                    float distBottom = 1.0 - input.uv.y;
                    mask = pow(saturate(distBottom), _DirtSpread);
                #endif

                float4 dirtColorSample = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, input.uv * _DirtTex_ST.xy + _DirtTex_ST.zw);
                float4 finalDirt = dirtColorSample * _DirtColor;
                
                albedo = lerp(albedo, finalDirt, mask * finalDirt.a);

                // Setup SimpleLit Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness = 0.5;
                surfaceData.normalTS = half3(0,0,1);
                surfaceData.emission = half3(0,0,0);
                surfaceData.occlusion = 1.0;
                
                // Calculate BlinnPhong lighting
                float4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Simple Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Simple Lit/DepthOnly"
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
