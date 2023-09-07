#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"


struct Attributes
{
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
    float3 normalOS : NORMAL;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID // uint instanceID : SV_InstanceID;
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
    float3 normalWS : VAR_NORMAL;
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input)
{
	Varyings output;

	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);

	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = TransformBaseUV(input.baseUV);;

    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);

	ClipLOD(input.positionCS.xy, unity_LODFade.x);

	float4 base = GetBase(input.baseUV);
    float3 normalWS = normalize(input.normalWS);
	
    Surface surface;
	surface.position = input.positionWS;
    surface.color = base.rgb;
    surface.normal = normalWS;
    surface.alpha = base.a;
	surface.metallic = GetMetallic(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);

	#ifdef _CLIPPING
	clip(base.a - GetCutoff(input.baseUV));
	#endif
	
	#ifdef _PREMULTIPLY_ALPHA
	BRDF brdf = GetBRDF(surface, true);
	#else
	BRDF brdf = GetBRDF(surface);
	#endif

	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
    float3 color = GetLighting(surface, brdf, gi);
	color += GetEmission(input.baseUV);

    return float4(color, surface.alpha);
}

#endif