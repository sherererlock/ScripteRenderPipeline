#ifndef CUSTOM_LIGHT
#define CUSTOM_LIGHT

#define Max_Directional_Light_Count 4
#define Max_Other_Light_Count 64

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[Max_Directional_Light_Count];
    float4 _DirectionalLightDirections[Max_Directional_Light_Count];
    float4 _DirectionalLightShadowData[Max_Directional_Light_Count];
    int _OtherLightCount;
    float4 _OtherLightColors[Max_Other_Light_Count];
    float4 _OtherLightPositions[Max_Other_Light_Count];
    float4 _OtherLightDirections[Max_Other_Light_Count];
    float4 _OtherLightSpots[Max_Other_Light_Count];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.shadowStrength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;

    return data;
}

Light GetDirectionalLight(int index, Surface surface, ShadowData shadowData)
{
    Light lit;
    lit.color = _DirectionalLightColors[index].rgb;
    lit.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData data = GetDirectionalShadowData(index, shadowData);
    lit.attenuation = GetDirectionalShadowAttenuation(data, shadowData, surface);

    //lit.attenuation = shadowData.cascadeIndex * 0.25f;

    return lit;
}

Light GetOtherLight(int index, Surface surface, ShadowData shadowData)
{
    Light lit;
    lit.color = _OtherLightColors[index].rgb;
    float3 ray = _OtherLightPositions[index].xyz - surface.position;
    lit.direction = normalize(ray);
    float distanceSqr = max(dot(ray, ray), 0.00001);
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));
    float4 spotAngles = _OtherLightSpots[index];
    float spotAttenuation = saturate(dot(_OtherLightDirections[index].xyz, lit.direction) * spotAngles.x + spotAngles.y);
    lit.attenuation = spotAttenuation * rangeAttenuation / distanceSqr;

    return lit;
}

#endif