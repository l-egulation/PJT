Shader "Stage1Village/ReferenceGround"
{
 Properties{[PerRendererData]_MainTex("Complete sketch drawing",2D)="white"{} _BrightTex("Registered bright drawing",2D)="white"{} _SoilTex("Generated bright soil",2D)="white"{} _SoilPeriod("Soil repeat",Vector)=(160,96,0,0) _SoilEdge("Shared wall silhouette",Float)=0 _Canvas("Canvas and contact anchor",Vector)=(32,32,0,0) _Body("Source paper body",Float)=0 _Period("Paper repeat",Vector)=(512,336,0,0) _HardExterior("Suppress faint exterior halo",Float)=0 _Fringe("Interior fringe direction",Float)=0 _BackdropFade("Pale non-solid soil backdrop",Range(0,1))=0
 _BackdropTiles("Native soil contour tiles",Float)=0 _BackdropCorner("Tile corner radius",Float)=9
 _BackdropLeftTex("Approved left edge",2D)="white"{} _BackdropRightTex("Approved right edge",2D)="white"{} _BackdropBottomTex("Approved underside",2D)="white"{}
 _BackdropLeftCanvas("Left native canvas",Vector)=(1,1,0,0) _BackdropRightCanvas("Right native canvas",Vector)=(1,1,0,0) _BackdropBottomCanvas("Bottom native canvas",Vector)=(1,1,0,0)
 _BackdropEdgePeriods("Repeat lengths",Vector)=(128,256,0,0) _BackdropBeam("Beam bounds",Vector)=(0,0,1,1) _BackdropColumn("Column bounds",Vector)=(0,0,1,1)}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"} Cull Off ZWrite Off Lighting Off Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex,_BrightTex,_SoilTex;float _Body,_HardExterior,_Fringe,_SoilEdge,_BackdropFade;float4 _Period,_SoilPeriod,_Canvas;
 float _VillageArtMode,_VillageArtWeight,_VillageBoundaryPixels,_VillageEndpointOverride;float4 _VillageAuthoredRange;
 float _VillageCrispRegions;
 #include "../VillageCrispRegions.cginc"
 #include "BackdropTileContours.cginc"
 struct v{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct o{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;fixed4 color:COLOR;};
 o vert(v i){o r;r.pos=UnityObjectToClipPos(i.vertex);r.uv=i.uv;r.world=mul(unity_ObjectToWorld,i.vertex).xy*float2(32,-32);r.color=i.color;return r;}
 float Weight(float2 p){if(_VillageEndpointOverride>=0)return saturate(_VillageEndpointOverride);if(_VillageArtMode<.5)return 0;if(_VillageArtMode<1.5)return 1;if(_VillageArtMode<2.5)return saturate(_VillageArtWeight);float curve=sin(p.y*.004)*28+sin(p.y*.010+1.4)*12;if(_VillageArtMode<3.5){float t=saturate((p.x-_VillageBoundaryPixels-curve+90)/180);return t*t*(3-2*t);}float width=_VillageAuthoredRange.y-_VillageAuthoredRange.x;float t=saturate((p.x-_VillageAuthoredRange.x)/width);float bend=clamp(curve/width,-.25,.25);t=saturate(t-bend*4*t*(1-t));return 1-t*t*(3-2*t);}
 float3 Soil(float2 world,float depth)
 {
  float3 soil=tex2D(_SoilTex,frac((floor(world)+.5)*float2(1,-1)/_SoilPeriod.xy)).rgb;
  float3 core=float3(39,35,36)/255;
  #ifndef UNITY_COLORSPACE_GAMMA
   core=GammaToLinearSpace(core);
  #endif
  // Distance is measured from EVERY exposed edge, including walls and ceilings.
  // Only color changes; silhouettes and contact lines are never blurred.
  return lerp(soil*.86,core,smoothstep(4,76,max(0,depth)));
 }
 fixed4 EdgeAlpha(fixed4 c,float2 uv,float2 world)
 {
  float sourceAlpha=c.a;
  if(_HardExterior>.5)c.a=smoothstep(.65,.98,c.a);
  if(_Fringe>3.5)
  {
   // Caps land on the existing 32px grid. Restore the single pixel just above it.
   // Halo cleanup used to discard this partially transparent contour completely.
   float ink=1-smoothstep(.35,.60,max(c.r,max(c.g,c.b)));
   if(frac(world.y/32)*32>=31)
    c.a=max(c.a,saturate(sourceAlpha*1.4)*ink);
  }
  if(_Fringe>.5&&_Fringe<1.5)c.a*=1-smoothstep(.5,.95,uv.x);
  else if(_Fringe<2.5&&_Fringe>1.5)c.a*=smoothstep(.05,.5,uv.x);
  else if(_Fringe>2.5&&_Fringe<3.5)c.a*=1-smoothstep(.62,.95,uv.y);
  else if(_Fringe>3.5)c.a*=smoothstep(.02,.35,uv.y);
  return c;
 }
 fixed4 frag(o i):SV_Target
 {
  float weight=Weight(i.world);
  float2 uv=_Body>.5?frac((floor(i.world)+.5)*float2(1,-1)/_Period.xy):i.uv;
  fixed4 sketch=tex2D(_MainTex,uv)*(_Body>.5?fixed4(1,1,1,1):i.color);
  if(_Body>.5)
  {
   float3 tile=_BackdropTiles>.5?BackdropCoverage(i.world):float3(1,0,i.uv.x);
   float depth=_BackdropTiles>.5?max(0,tile.z):i.uv.x;
   if(tile.x<=0)discard;
   if(_VillageCrispRegions>.5&&_VillageEndpointOverride<0&&_VillageArtMode>2.5)
    weight=VillageEarthRegion(i.world,Soil(i.world,0),depth);
   float pressure=lerp(1,.45,smoothstep(24,112,depth));
   sketch.rgb=lerp(fixed3(.988235,.949020,.878431),sketch.rgb,pressure);
   float3 result=lerp(Soil(i.world,depth),sketch.rgb,weight);
   if(_BackdropTiles>.5)
   {
    float3 graphite=float3(48,44,39)/255;
    #ifndef UNITY_COLORSPACE_GAMMA
     graphite=GammaToLinearSpace(graphite);
    #endif
    result=lerp(result,lerp(Soil(i.world,0)*.55,graphite,weight),tile.y);
   }
   if(_BackdropFade>0)
   {
    float3 paper=float3(248,237,220)/255;
    #ifndef UNITY_COLORSPACE_GAMMA
     paper=GammaToLinearSpace(paper);
    #endif
    // Keep the actual soil's brightness, depth and texture. Recessed colored
    // walls reduce chroma rather than mixing every pixel toward pale beige.
    float luminance=dot(result,float3(.299,.587,.114));
    float3 quietSoil=lerp(luminance.xxx,result,.45);
    result=lerp(quietSoil,lerp(result,paper,_BackdropFade),weight);
   }
   return fixed4(result,tile.x);
  }
  fixed4 bright;
  if(_SoilEdge>.5)
  {
   float depth=_Fringe<1.5?i.uv.x*_Canvas.x-_Canvas.z:
     _Fringe<2.5?_Canvas.z-i.uv.x*_Canvas.x:
     i.uv.y*_Canvas.y-(_Canvas.y-_Canvas.w);
   bright=fixed4(Soil(i.world,depth),sketch.a);
   // The SAME native wall alpha carries both renderings, so exposed turns agree.
   float outline=1-smoothstep(.18,.4,max(sketch.r,max(sketch.g,sketch.b)));
   bright.rgb*=1-.25*outline;
  }
  else bright=tex2D(_BrightTex,uv)*i.color;
  if(_VillageCrispRegions>.5&&_VillageEndpointOverride<0&&_VillageArtMode>2.5)
   weight=VillageEarthRegion(i.world,Soil(i.world,0),0);
  sketch=EdgeAlpha(sketch,uv,i.world);bright=EdgeAlpha(bright,uv,i.world);
  float alpha=lerp(bright.a,sketch.a,weight);
  float3 premul=lerp(bright.rgb*bright.a,sketch.rgb*sketch.a,weight);
  return fixed4(alpha>0?premul/alpha:float3(0,0,0),alpha);
 }
 ENDCG}
 }
}
