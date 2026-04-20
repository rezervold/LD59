Shader "Bash and Gush/URP/Union Outline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Color", Color) = (1.0, 0.55, 0.1, 1.0)
        _OutlineAlpha("Outline Alpha", Range(0.0, 1.0)) = 0.35
        _OutlineWidth("Outline Width (px)", Range(0.0, 12.0)) = 3.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _OutlineZTest("Outline ZTest", Float) = 8
        [IntRange] _StencilRef("Outline Group", Range(0, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
        }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "BashAndGushOutlineMask" }

            Blend Off
            ZWrite Off
            ZTest Always
            Cull Back
            ColorMask 0

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "OutlineFill"
            Tags { "LightMode" = "BashAndGushOutlineFill" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_OutlineZTest]
            Cull Front

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineAlpha;
                float _OutlineWidth;
            CBUFFER_END

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

            float4 GetOutlinePositionCS(float3 positionOS, float3 normalOS)
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                float3 positionVS = TransformWorldToView(positionWS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);

                float4 positionCS = mul(UNITY_MATRIX_P, float4(positionVS, 1.0));
                float4 offsetPositionCS = mul(UNITY_MATRIX_P, float4(positionVS + normalVS, 1.0));

                float invW = rcp(max(abs(positionCS.w), 0.0001));
                float invOffsetW = rcp(max(abs(offsetPositionCS.w), 0.0001));
                float2 offsetDirection = offsetPositionCS.xy * invOffsetW - positionCS.xy * invW;
                float directionLength = length(offsetDirection);

                if (directionLength > 0.0001)
                    offsetDirection /= directionLength;
                else
                    offsetDirection = float2(0.0, 0.0);

                float2 pixelOffset = (_OutlineWidth * 2.0) / _ScreenParams.xy;
                positionCS.xy += offsetDirection * pixelOffset * positionCS.w;
                return positionCS;
            }

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                output.positionCS = GetOutlinePositionCS(input.positionOS.xyz, input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = _OutlineColor;
                col.a *= _OutlineAlpha;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
