#ifndef CUSTOM_SURFACE
#define CUSTOM_SURFACE

struct Surface
{
    float3 position;
    float3 color;
    float3 normal;
    float3 viewDirection;
    float depth;
    float alpha;
    float metallic;
    float smoothness;
    float dither;
    float fresnelStrength;
};

#endif