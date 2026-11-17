#ifndef MARMARTFX_ART_INCLUDED
#define MARMARTFX_ART_INCLUDED

// Procedural helpers for the MarmartFX/Art/* shaders.

float HS_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float HS_VNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = HS_Hash21(i);
    float b = HS_Hash21(i + float2(1, 0));
    float c = HS_Hash21(i + float2(0, 1));
    float d = HS_Hash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float HS_Fbm(float2 p)
{
    return HS_VNoise(p) * 0.5 + HS_VNoise(p * 2.03 + 17.1) * 0.3 + HS_VNoise(p * 4.01 + 41.7) * 0.2;
}

static const float kHSBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float HS_Bayer4(float2 px)
{
    int2 p = (int2)fmod(floor(px), 4.0);
    return (kHSBayer[p.y * 4 + p.x] + 0.5) / 16.0;
}

// Frame index for "boiling" hand-animated looks (0 when fps is 0).
float HS_BoilFrame()
{
    return _ArtFps > 0.001 ? floor(_Time.y * _ArtFps) : 0.0;
}

half HS_Luma(half3 c)
{
    return dot(c, half3(0.299h, 0.587h, 0.114h));
}

#endif
