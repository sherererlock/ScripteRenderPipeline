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

#if defined(_LIGHTS_PER_OBJECT)

	for (int j = 0; j < min(unity_LightData.y, 8); j++)
	{
		int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
		Light light = GetOtherLight(lightIndex, surface, data);
		color += GetLighting(surface, brdf, light);
	}

#else

	for (int j = 0; j < GetOtherLightCount(); j++)
	{
		Light light = GetOtherLight(j, surface, data);
		color += GetLighting(surface, brdf, light);
	}

#endif

    return color;
}

#endif