#ifndef RPG_WIND_INCLUDED
#define RPG_WIND_INCLUDED

// Thảo Nguyên Gió's wind (Wind.cs hands it over once a frame): xy the wind (heading × strength),
// z the wind's clock in seconds, w how much of it the screen shows (1 on the steppe).
float4 _RpgWind;

// A sprite whose pivot is at its foot sways: its roots stay, its tips bend with the wind, a
// travelling gust makes neighbours move a little out of step.
float3 RpgSway(float3 positionOS, half sway)
{
    if (sway <= 0.0001) return positionOS;
    float3 wpos = TransformObjectToWorld(positionOS);
    float h = max(0.0, positionOS.y);
    float t = _RpgWind.z;
    float2 w = _RpgWind.xy * _RpgWind.w;
    float strength = length(w);
    float gust = 0.55 + 0.45 * sin(t * 2.2 + wpos.x * 0.55 + wpos.y * 0.35);
    float flutter = sin(t * 7.3 + wpos.x * 2.1 + wpos.y * 1.3) * (0.08 + 0.12 * strength);
    float bend = sway * h * h;
    wpos.x += (w.x * gust + flutter) * bend;
    wpos.y += (w.y * gust * 0.35 - abs(w.x) * gust * 0.12) * bend;
    return TransformWorldToObject(wpos);
}

// Light bands rolling over a grass field in the wind's direction (0..1).
half RpgWave(float2 wpos, half wave)
{
    if (wave <= 0.0001) return 0;
    float2 w = _RpgWind.xy * _RpgWind.w;
    float s = length(w);
    if (s < 0.01) return 0;
    float2 d = w / s;
    float phase = dot(wpos, d) * 0.42 - _RpgWind.z * 3.0;
    float band = saturate(sin(phase) * 0.5 + 0.5);
    band = band * band * band;
    float breakup = 0.55 + 0.45 * sin(dot(wpos, float2(-d.y, d.x)) * 0.27 + _RpgWind.z * 0.4);
    return (half)(band * breakup * s * wave);
}

#endif
