// Shared dissolve + 1px outline for RPG/Sprite Lit FX (plan T22).
// The material block (UnityPerMaterial) is declared by the shader; this file only has the maths.
#ifndef RPG_SPRITE_FX
#define RPG_SPRITE_FX

float4 _MainTex_TexelSize;

// cheap per-cell hash (0..1)
half RpgHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// > 0 keeps the texel, < 0 dissolves it; blocky in texture pixels so it suits pixel art
half RpgDissolve(float2 uv, half amount, half pixel)
{
    float2 cell = floor(uv * _MainTex_TexelSize.zw / max(pixel, 1.0));
    return RpgHash(cell) - amount * 1.02;
}

// 1 where the texel is empty but a neighbour (width texels away) is not
half RpgOutline(TEXTURE2D_PARAM(tex, samp), float2 uv, half alpha, half width)
{
    float2 d = _MainTex_TexelSize.xy * width;
    half n = SAMPLE_TEXTURE2D(tex, samp, uv + float2(d.x, 0)).a;
    n = max(n, SAMPLE_TEXTURE2D(tex, samp, uv - float2(d.x, 0)).a);
    n = max(n, SAMPLE_TEXTURE2D(tex, samp, uv + float2(0, d.y)).a);
    n = max(n, SAMPLE_TEXTURE2D(tex, samp, uv - float2(0, d.y)).a);
    return saturate(n - alpha * 4.0);
}

#endif
