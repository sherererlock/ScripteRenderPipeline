#ifndef CUSTOM_SHADOWS
#define CUSTOM_SHADOWS

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#endif

#if defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#endif

#if defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define Max_Directional_Light_Count 4
#define Max_Cascades_Count 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _ShadowDistanceFade;
	float4 _ShadowAtlasSize;
	float4 _CullingSpheres[Max_Cascades_Count];
	float4 _CascadeData[Max_Cascades_Count];
	float4x4 _DirectionalShadowMatrices[Max_Directional_Light_Count * Max_Cascades_Count];
CBUFFER_END

struct ShadowData
{
	int cascadeIndex;
	float strength;
	float cascadeBlend;
};

struct DirectionalShadowData
{
	float shadowStrength;
	int tileIndex;
	float normalBias;
};

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterDirectionalShadow(float3 positionSTS)
{
#ifdef DIRECTIONAL_FILTER_SETUP
	
	float shadow = 0.0;
	float weights[DIRECTIONAL_FILTER_SAMPLES];
	float2 positions[DIRECTIONAL_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.yyxx;
	DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);

	for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
		shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));

	return shadow;
#else
	return SampleDirectionalShadowAtlas(positionSTS);
#endif
}

float GetDirectionalShadowAttenuation(DirectionalShadowData data, ShadowData shadowData, Surface surface)
{
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif

	if (data.shadowStrength <= 0.0)
		return 1.0;

	float3 normalBias = surface.normal * data.normalBias * _CascadeData[shadowData.cascadeIndex].y;

	float3 positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex], float4(surface.position + normalBias, 1.0)).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (shadowData.cascadeBlend < 1.0)
	{
		normalBias = surface.normal * (data.normalBias * _CascadeData[shadowData.cascadeIndex + 1].y);
		positionSTS = mul( _DirectionalShadowMatrices[data.tileIndex + 1], float4(surface.position + normalBias, 1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, shadowData.cascadeBlend);
	}

	return lerp(1.0, shadow, data.shadowStrength);
}

float FadedShadowStrength(float distance, float scale, float fade)
{
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surface)
{
	ShadowData data;

	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(surface.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	int i = 0;
	for (i = 0; i < _CascadeCount; i++)
	{
		float distanceSqr = DistanceSquared(surface.position, _CullingSpheres[i].xyz);
		if (distanceSqr < _CullingSpheres[i].w)
		{
			float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
			if (i == _CascadeCount - 1)
				data.strength *= fade;
			else
				data.cascadeBlend = fade;

			break;
		}
	}

	if (i >= _CascadeCount)
	{
		data.strength = 0.0;
	}
#ifdef _CASCADE_BLEND_DITHER
	else if (data.cascadeBlend < surface.dither)
	{
		i += 1;
	}
#endif

#ifndef _CASCADE_BLEND_SOFT
	data.cascadeBlend = 1.0;
#endif

	data.cascadeIndex = i;

	return data;
}

#endif