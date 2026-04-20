#ifndef RETRO_COMMON_URP_INCLUDED
#define RETRO_COMMON_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _Color;
    half4 _SpecColor;
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

float4 ScreenSnap(float4 clipPos, float vertJitter)
{
    float geoRes = vertJitter * 125.0f + 1.0f;
    float2 pixelPos = round((clipPos.xy / clipPos.w) * _ScreenParams.xy / geoRes) * geoRes;
    clipPos.xy = pixelPos / _ScreenParams.xy * clipPos.w;
    return clipPos;
}

float4 ViewSpaceSnap(float3 positionOS, float vertJitter)
{
    float geoRes = (vertJitter - 1.0f) * -1000.0f;
    float3 viewPos = TransformWorldToView(TransformObjectToWorld(positionOS));
    viewPos.xyz = floor(viewPos.xyz * geoRes) / geoRes;
    return mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
}

float3 CalculateAffineCoords(float2 uv, float4 clipPos)
{
    float wVal = clipPos.w;
    return float3(uv * wVal, wVal);
}

float2 ApplyAffineMapping(float2 uv, float3 affineUV, float intensity, float4 texST)
{
    float2 correctUV = uv * texST.xy + texST.zw;
    float2 affineUVFinal = (affineUV.xy / affineUV.z) * texST.xy + texST.zw;
    return lerp(correctUV, affineUVFinal, intensity);
}

float CheckDrawDistance(float3 worldPos, float drawDist)
{
    if (drawDist == 0) return 0;
    float dist = distance(worldPos, _WorldSpaceCameraPos);
    return dist > drawDist ? 1 : 0;
}

half3 CalculateVertexLighting(float3 positionWS, float3 normalWS, half glossiness)
{
    half3 diffuse = half3(0, 0, 0);
    half3 specular = half3(0, 0, 0);
    half3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);

    Light mainLight = GetMainLight();
    half NdotL = saturate(dot(normalWS, mainLight.direction));
    diffuse += mainLight.color * NdotL * mainLight.distanceAttenuation;
    
    half3 halfDir = normalize(viewDir + mainLight.direction);
    half NdotH = saturate(dot(normalWS, halfDir));
    specular += mainLight.color * pow(NdotH, glossiness * 128.0) * 0.5 * mainLight.distanceAttenuation;
    
    uint additionalLightsCount = GetAdditionalLightsCount();
    for (uint i = 0; i < additionalLightsCount; i++)
    {
        Light light = GetAdditionalLight(i, positionWS);
        NdotL = saturate(dot(normalWS, light.direction));
        diffuse += light.color * NdotL * light.distanceAttenuation;
        
        halfDir = normalize(viewDir + light.direction);
        NdotH = saturate(dot(normalWS, halfDir));
        specular += light.color * pow(NdotH, glossiness * 128.0) * 0.5 * light.distanceAttenuation;
    }
    
    diffuse += SampleSH(normalWS);
    
    return diffuse + specular;
}

#endif
