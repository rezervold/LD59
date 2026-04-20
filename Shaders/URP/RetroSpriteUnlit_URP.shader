Shader "Retro 3D Shader Pack/URP/Sprite (Unlit)"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color Tint", Color) = (1, 1, 1, 1)

        _VertJitter("Vertex Jitter", Range(0.0, 0.999)) = 0.95
        _AffineMapIntensity("Affine Texture Mapping Intensity", Range(0.0, 1.0)) = 1.0
        _DrawDist("Draw Distance", Float) = 0
        
        [Toggle(ENABLE_SCREENSPACE_JITTER)] _EnableScreenSpaceJitter("Screen Space Jitter", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local ENABLE_SCREENSPACE_JITTER

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _VertJitter;
                float _AffineMapIntensity;
                float _DrawDist;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                float3 uv_affine : TEXCOORD1;
                float drawDistClip : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                float4 color : COLOR;
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
                Varyings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
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
                output.color = input.color;
                
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

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * input.color * _Color;
                
                col.rgb = MixFog(col.rgb, input.fogCoord);
                
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
