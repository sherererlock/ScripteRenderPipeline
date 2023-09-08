#ifndef CUSTOM_LIGHTING
#define CUSTOM_LIGHTING

float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

// Lo = Li * fr * ndotl
float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surface, BRDF brdf, GI gi)
{
    ShadowData data = GetShadowData(surface);
    data.shadowMask = gi.shadowMask;

    float3 color = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);

    for (int i = 0; i < GetDirectionalLightCount(); i ++)
        color += GetLighting(surface, brdf, GetDirectionalLight(i, surface, data));

    return color;
}

#endif