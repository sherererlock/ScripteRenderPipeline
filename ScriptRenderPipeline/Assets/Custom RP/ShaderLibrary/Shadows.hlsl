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

#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define Max_Directional_Light_Count 4
#define Max_Cascades_Count 4

#define Max_Other_Light_Count 16

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);

#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _ShadowDistanceFade;
	float4 _ShadowAtlasSize;
	float4 _CullingSpheres[Max_Cascades_Count];
	float4 _CascadeData[Max_Cascades_Count];
	float4 _OtherShadowTiles[Max_Other_Light_Count];
	float4x4 _DirectionalShadowMatrices[Max_Directional_Light_Count * Max_Cascades_Count];
	float4x4 _OtherShadowMatrices[Max_Other_Light_Count];
CBUFFER_END

struct ShadowMask {
	bool always;
	bool distance;
	float4 shadows;
};

struct ShadowData
{
	int cascadeIndex;
	float strength;
	float cascadeBlend;
	ShadowMask shadowMask;
};

struct DirectionalShadowData
{
	float shadowStrength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};

struct OtherShadowData{
	float strength;
	int tileIndex;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 spotDirectionWS;
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

float SampleOtherShadowAtlas(float3 positionSTS)
{
	return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterOtherShadow(float3 positionSTS)
{
#ifdef OTHER_FILTER_SETUP

	float shadow = 0.0;
	float weights[OTHER_FILTER_SAMPLES];
	float2 positions[OTHER_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.wwzz;
	OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);

	for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
		shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z));

	return shadow;
#else
	return SampleOtherShadowAtlas(positionSTS);
#endif
}

float GetCascadedShadow(
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) 
{
	float3 normalBias = surfaceWS.interpolatedNormal *
		(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;

	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) 
	{
		normalBias = surfaceWS.interpolatedNormal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel)
{
	float shadow = 1.0;
	if (mask.distance || mask.always) {
		if(channel >= 0)
		shadow = mask.shadows[channel];
	}
	return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
	if (mask.distance || mask.always) {
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}

float MixBakedAndRealtimeShadows(
	ShadowData global, float shadow, int shadowMaskChannel, float strength
)
{
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	if (global.shadowMask.always)
	{
		shadow = lerp(1.0, shadow, global.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}

	if (global.shadowMask.distance) 
	{
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}

	return lerp(1.0, shadow, strength * global.strength);
}

float GetDirectionalShadowAttenuation(DirectionalShadowData data, ShadowData shadowData, Surface surface)
{
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif

	float shadow = 1.0;
	if (data.shadowStrength * shadowData.strength <= 0.0)
	{
		shadow = GetBakedShadow(shadowData.shadowMask, data.shadowMaskChannel, abs(data.shadowStrength));
	}
	else
	{
		shadow = GetCascadedShadow(data, shadowData, surface);
		shadow = MixBakedAndRealtimeShadows(shadowData, shadow, data.shadowMaskChannel, data.shadowStrength);
	}

	return shadow;
}

float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
	float4 tileData = _OtherShadowTiles[other.tileIndex];

	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight, other.spotDirectionWS);

	float3 normalBias = surfaceWS.interpolatedNormal * tileData.w * distanceToLightPlane;
	float4 positionSTS = mul(
		_OtherShadowMatrices[other.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w);
}

float GetOtherShadowAttenuation(OtherShadowData other, ShadowData global, Surface surfaceWS)
 {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	
	float shadow;
	if (other.strength * global.strength <= 0.0) 
	{
		shadow = GetBakedShadow(
			global.shadowMask, other.shadowMaskChannel, abs(other.strength)
		);
	}
	else 
	{
		shadow = GetOtherShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(global, shadow, other.shadowMaskChannel, other.strength);
	}

	return shadow;
}

float FadedShadowStrength(float distance, float scale, float fade)
{
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surface)
{
	ShadowData data;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;

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

	if (i >= _CascadeCount && _CascadeCount > 0)
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