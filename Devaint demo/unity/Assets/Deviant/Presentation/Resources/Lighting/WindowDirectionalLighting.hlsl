#ifndef DEVIANT_WINDOW_PROPERTIES_INCLUDED
#define DEVIANT_WINDOW_PROPERTIES_INCLUDED
// Four receiver-local windows, eight convex opening pieces, 64 opening planes and
// 128 shadow edges per window. Match WindowLightBuffer's fixed upload capacities.
#define DEVIANT_WINDOW_PROPERTIES \
    float4x4 _WindowWorldToLocal[4]; \
    float4 _WindowPlanes[64]; \
    float4 _WindowEdges[512]; \
    float4 _WindowEdgeOwners[512]; \
    float4 _WindowOpeningPlanes[256]; \
    float4 _WindowPieces[32]; \
    float4 _WindowDirection[4]; \
    float4 _WindowToLight[4]; \
    half4 _WindowColor[4]; \
    float4 _WindowShadow[4]; \
    float4 _WindowData[4]; \
    float4 _WindowCounts[4]; \
    int _WindowCount;
#endif

#if defined(DEVIANT_WINDOW_FUNCTIONS) && !defined(DEVIANT_WINDOW_FUNCTIONS_INCLUDED)
#define DEVIANT_WINDOW_FUNCTIONS_INCLUDED
float WindowCross(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }
bool WindowSourceDistance(int w, float2 p, out float reach)
{
    reach = 1e20;
    [loop] for (int piece = 0; piece < (int)_WindowCounts[w].z; piece++)
    {
        float2 range = _WindowPieces[w * 8 + piece].xy;
        float enter = 0, exit = _WindowData[w].z; bool hit = true;
        [loop] for (int i = 0; i < (int)range.y; i++)
        {
            float3 plane = _WindowOpeningPlanes[w * 64 + (int)range.x + i].xyz;
            float value = dot(plane.xy, p) + plane.z, rate = dot(plane.xy, _WindowDirection[w].xy);
            if (abs(rate) < 1e-7) { if (value < 0) { hit = false; break; } }
            else if (rate > 0) exit = min(exit, value / rate);
            else enter = max(enter, value / rate);
            if (exit < enter) { hit = false; break; }
        }
        if (hit) reach = min(reach, enter);
    }
    return reach < 1e19;
}
float WindowRayBlocked(int w, float2 p, float reach)
{
    float2 ray = -_WindowDirection[w].xy;
    [loop] for (int i = 0; i < (int)_WindowCounts[w].y; i++)
    {
        int index = w * 128 + i;
        if (_WindowShadow[w].z > 0 && _WindowEdgeOwners[index].x == _WindowShadow[w].z) continue;
        float2 a = _WindowEdges[index].xy, v = _WindowEdges[index].zw - a;
        float denominator = WindowCross(ray, v); if (abs(denominator) < 1e-7) continue;
        float2 offset = a - p;
        float t = WindowCross(offset, v) / denominator, u = WindowCross(offset, ray) / denominator;
        if (t > 1e-4 && t < reach - 1e-4 && u >= 0 && u <= 1) return 1;
    }
    return 0;
}
float WindowRayTransmission(int w, float2 p)
{
    float reach;
    if (!WindowSourceDistance(w, p, reach)) return 0;
    float endFade = 1 - smoothstep(max(0, _WindowData[w].z - _WindowData[w].w), _WindowData[w].z, reach);
    return endFade * (1 - _WindowShadow[w].x * WindowRayBlocked(w, p, reach));
}
float WindowCoverage(int w, float2 p)
{
    if (_WindowData[w].x < .5 || _WindowCounts[w].x < 3) return 0;
    float edge = 1e6;
    [loop] for (int i = 0; i < (int)_WindowCounts[w].x; i++)
        edge = min(edge, dot(_WindowPlanes[w * 16 + i].xy, p) + _WindowPlanes[w * 16 + i].z);
    if (edge <= 0) return 0;
    float transmission = WindowRayTransmission(w, p), feather = _WindowShadow[w].y;
    if (feather > 1e-5)
    {
        // All samples stay parallel. Filter transmission as well as occlusion so
        // a frame gap never becomes light just because no shadow caster was hit.
        float2 offset = float2(-_WindowDirection[w].y, _WindowDirection[w].x) * feather;
        transmission = transmission * .4 + (WindowRayTransmission(w, p + offset * .5) + WindowRayTransmission(w, p - offset * .5)) * .2
            + (WindowRayTransmission(w, p + offset) + WindowRayTransmission(w, p - offset)) * .1;
    }
    return smoothstep(0, max(_WindowData[w].y, 1e-4), edge) * transmission;
}
#endif
