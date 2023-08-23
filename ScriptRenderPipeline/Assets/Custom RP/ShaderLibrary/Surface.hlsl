#ifndef CUSTOM_SURFACE
#define CUSTOM_SURFACE

struct Surface
{
    float3 color;
    float3 normal;
    float3 viewDirection;
    float alpha;
    float metallic;
    float smoothness;
};


#endif