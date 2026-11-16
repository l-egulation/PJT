// Presentation masks select an endpoint; no crossfade or random pixel dissolve.
// The source painting's material shapes supply the local contours.
float VillageRegionProgress(float2 p)
{
    if(_VillageArtMode<3.5)return 1-saturate((p.x-_VillageBoundaryPixels+240)/480);
    return saturate((p.x-_VillageAuthoredRange.x)/max(1,_VillageAuthoredRange.y-_VillageAuthoredRange.x));
}
float VillageMaterialRegion(float2 p,float3 rgb,float4 anchor,float layer)
{
    if(anchor.z>.5)p=anchor.xy;
    float progress=VillageRegionProgress(p);
    if(progress<=0)return 1;
    if(progress>=1)return 0;
    // Select complete nearby drawings. A material-color classifier would turn
    // individual leaves/windows into confetti and spoil the clean district edge.
    if(anchor.z>.5)
    {
        float priority=.25+.6*frac(sin(anchor.x*.013+anchor.y*.007)*13.37);
        return progress<priority?1:0;
    }
    // Distant layers remain large readable masses. Their native alpha supplies
    // roof/foliage contours, without classifying tiny source texture variations.
    if(layer>0)
    {
        float bend=.018*sin(p.y*.003)+.009*sin(p.y*.007+1.3);
        return progress<layer+bend?1:0;
    }
    #ifndef UNITY_COLORSPACE_GAMMA
        rgb=LinearToGammaSpace(rgb);
    #endif
    float rank=.94; // light plaster / broad blank sketch faces last
    if(rgb.b>rgb.r*1.12&&rgb.b>rgb.g*.94)rank=.22; // sky and water
    else if(rgb.g>rgb.r*1.04&&rgb.g>rgb.b*1.12)rank=.58; // whole foliage masses
    else if(rgb.r>rgb.g*1.24&&rgb.g>rgb.b*1.12)rank=.40; // roofs, warm wood
    else if(max(rgb.r,max(rgb.g,rgb.b))<.48)rank=.76;
    return progress<rank?1:0;
}
float VillageEarthRegion(float2 p,float3 soil,float depth)
{
    float progress=VillageRegionProgress(p);
    if(progress<=0)return 1;
    if(progress>=1)return 0;
    #ifndef UNITY_COLORSPACE_GAMMA
        soil=LinearToGammaSpace(soil);
    #endif
    // Erosion follows exposed terrain depth; the actual painted clods supply
    // only a small edge irregularity. No repeated rectangular masks or blur.
    float rank=.78+.17*saturate(depth/128)+.04*saturate(dot(soil,float3(.3,.5,.2)));
    return progress<rank?1:0;
}
