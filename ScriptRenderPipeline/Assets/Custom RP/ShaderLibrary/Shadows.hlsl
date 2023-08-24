#ifndef CUSTOM_SHADOWS
#define CUSTOM_SHADOWS

#define Max_Directional_Light_Count 4
#define Max_Cascades_Count 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _ShadowDistanceFade;
	float4 _CullingSpheres[Max_Cascades_Count];
	float4x4 _DirectionalShadowMatrices[Max_Directional_Light_Count * Max_Cascades_Count];
CBUFFER_END


struct ShadowData
{
	int cascadeIndex;
	float strength;
};

struct DirectionalShadowData
{
	float shadowStrength;
	int tileIndex;
};

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surface)
{
	if (data.shadowStrength <= 0.0)
		return 1.0;

	float3 positonSTS = mul(_DirectionalShadowMatrices[data.tileIndex], float4(surface.position, 1.0)).xyz;
	float shadow = SampleDirectionalShadowAtlas(positonSTS);

	return lerp(1.0, shadow, data.shadowStrength);
}

float FadedShadowStrength(float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surface)
{
	ShadowData data;

	data.strength = FadedShadowStrength(surface.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	int i = 0;
	for (i = 0; i < _CascadeCount; i++)
	{
		float distanceSqr = DistanceSquared(surface.position, _CullingSpheres[i].xyz);
		if (distanceSqr < _CullingSpheres[i].w)
		{
			if (i == _CascadeCount - 1)
				data.strength *= FadedShadowStrength(distanceSqr, 1.0 / _CullingSpheres[i].w, _ShadowDistanceFade.z);

			break;
		}
	}

	if(i >= _CascadeCount)
		data.strength = 0.0;

	data.cascadeIndex = i;

	return data;
}

#endif