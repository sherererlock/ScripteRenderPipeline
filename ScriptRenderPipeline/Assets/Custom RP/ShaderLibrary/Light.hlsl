#ifndef CUSTOM_LIGHT
#define CUSTOM_LIGHT

#define Max_Directional_Light_Count 4

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[Max_Directional_Light_Count];
    float4 _DirectionalLightDirections[Max_Directional_Light_Count];
    float4 _DirectionalLightShadowData[Max_Directional_Light_Count];
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

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.shadowStrength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;

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

#endif