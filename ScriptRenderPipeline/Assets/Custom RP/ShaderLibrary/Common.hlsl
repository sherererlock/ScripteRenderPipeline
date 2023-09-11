#ifndef CUSTOM_COMMON
#define CUSTOM_COMMON

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include"UnityInput.hlsl"

//float3 TransformObjectToWorld(float3 positionOS)
//{
//	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
//}
//
//float4 TransformWorldToHClip(float3 positionWS)
//{
//	return mul(unity_MatrixVP, float4(positionWS, 1.0));
//}

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined _SHADOW_MASK_DISTANCE || defined _SHADOW_MASK_ALWAYS
#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

float Square(float v) {
	return v * v;
}

float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD(float2 positionCS, float fade) {
#if defined(LOD_FADE_CROSSFADE)
	//float dither = (positionCS.y % 32) / 32;
	float dither = InterleavedGradientNoise(positionCS.xy, 0);
	clip(fade + (fade < 0.0 ? dither : -dither));
#endif
}

#endif