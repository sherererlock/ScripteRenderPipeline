#ifndef CUSTOM_SHADOWS
#define CUSTOM_SHADOWS

#define Max_Directional_Light_Count 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
float4x4 _DirectionalShadowMatrices[Max_Directional_Light_Count];
CBUFFER_END


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

#endif