// v26 지하수로 뒷벽과 가로 배관. 그림 대신 밝기 단계 지도를 월드 화소(32px = 1u)마다 읽고, 등·채광 빛 지도(_Light, 8px 격자)를 단계에 더해
// 렌더러와 같은 램프(차가운 11색·따뜻한 11색, 4×4 베이어 디더)로 칠한다. 빛이 돌 색 단계를 올리므로 윗변 빛·줄눈 그늘이 그대로 살아 있다.
Shader "Deviant/Chapter2/SewerWall" {
 Properties {
  _Level("Wall level map (R = level x 20)",2D)="black"{}
  _Ramp("Ramp (top cool, bottom warm)",2D)="white"{}
  _Light("Light map (R = L / 6.5, G = warm)",2D)="black"{}
  _LightRect("Light map world rect",Vector)=(0,0,1,1)
  _WallLevel("Rear wall level map (for shadow rows = 0)",2D)="black"{}
  _Tile("Level tile size px (xy), origin px (zw)",Vector)=(576,512,0,0)
  _Ambient("Ambient lift in ramp levels",Float)=0
 }
 SubShader { Tags {"RenderType"="Transparent" "Queue"="Transparent-6" "RenderPipeline"="UniversalPipeline"}
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; };
 struct V { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; };
 TEXTURE2D(_Level); SAMPLER(sampler_Level);
 TEXTURE2D(_Ramp); SAMPLER(sampler_Ramp);
 TEXTURE2D(_Light); SAMPLER(sampler_Light);
 TEXTURE2D(_WallLevel); SAMPLER(sampler_WallLevel);
 float4 _LightRect; float4 _Tile; float _Ambient;
 float4 _SewerPlayerLight;   // 주인공 둘레 빛(전역): xy 월드 자리, z 반지름 px, w 단계
 static const float Bayer[16] = {0,8,2,10, 12,4,14,6, 3,11,1,9, 15,7,13,5};
 V vert(A a){V v;float3 w=TransformObjectToWorld(a.positionOS.xyz);v.positionCS=TransformWorldToHClip(w);v.world=w.xy;return v;}
 half4 frag(V v):SV_Target
 {
  float2 px=floor(v.world*32+1e-3);
  float2 wall=float2(576,512);
  float2 wt=px-floor(px/wall)*wall;                       // 월드 화소를 벽 타일(576×512) 안으로
  float row=wall.y-1-wt.y;                               // 그림의 위에서 잰 줄(렌더러 좌표). 디더는 늘 벽 기준
  float2 q=px-_Tile.zw; float2 t=q-floor(q/_Tile.xy)*_Tile.xy;   // 이 재질의 단계 지도(벽 또는 가로 배관) 안 자리
  float raw=SAMPLE_TEXTURE2D_LOD(_Level,sampler_Level,(t+.5)/_Tile.xy,0).r*255.0;
  float level=raw<.5?SAMPLE_TEXTURE2D_LOD(_WallLevel,sampler_WallLevel,(wt+.5)/wall,0).r*255.0/20.0-1:raw/20.0;   // 0 = 관 그늘(벽 -1단)
  float2 lw=SAMPLE_TEXTURE2D_LOD(_Light,sampler_Light,(v.world-_LightRect.xy)/_LightRect.zw,0).rg;
  int bx=(int)(wt.x-floor(wt.x/4)*4), by=(int)(row-floor(row/4)*4);
  float b=(Bayer[by*4+bx]+.5)/16;
  float2 pd=(v.world-_SewerPlayerLight.xy)*32*float2(1,1.15);
  float pl=_SewerPlayerLight.z>0?_SewerPlayerLight.w*pow(saturate(1-length(pd)/_SewerPlayerLight.z),1.9):0;
  float lv=level+_Ambient+min(lw.r*6.5+pl,6.5);
  float li=clamp(floor(lv+.5+(b-.5)*.3-.5),0,10);
  float ws=clamp(floor(lw.g*3+b),0,3)/3;
  half3 cool=SAMPLE_TEXTURE2D_LOD(_Ramp,sampler_Ramp,float2((li+.5)/11,.75),0).rgb;
  half3 warm=SAMPLE_TEXTURE2D_LOD(_Ramp,sampler_Ramp,float2((li+.5)/11,.25),0).rgb;
  return half4(lerp(cool,warm,ws),1);
 }
 ENDHLSL
 } }
}
