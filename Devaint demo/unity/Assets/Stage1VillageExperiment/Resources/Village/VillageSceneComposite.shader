Shader "Hidden/Stage1Village/SceneComposite"
{
    Properties
    {
        _MainTex("Completed color scene",2D)="white"{}
        _SketchTex("Completed sketch scene",2D)="white"{}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex,_SketchTex;
            float4 _MainTex_TexelSize,_SketchTex_TexelSize;
            float _VillageArtMode,_VillageArtWeight,_VillageBoundaryPixels;
            float4 _VillageAuthoredRange;
            float4 _VillageViewportOrigin,_VillageViewportRight,_VillageViewportUp;

            struct interpolator
            {
                float4 pos:SV_POSITION;
                float2 colorUV:TEXCOORD0;
                float2 sketchUV:TEXCOORD1;
                float2 viewportUV:TEXCOORD2;
            };

            interpolator vert(appdata_img v)
            {
                interpolator o;
                o.pos=UnityObjectToClipPos(v.vertex);
                // Blit already supplies the source's orientation. The independently
                // rendered texture and world reconstruction use upright viewport UVs.
                o.colorUV=v.texcoord;
                o.viewportUV=v.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                    if(_MainTex_TexelSize.y<0)o.viewportUV.y=1-o.viewportUV.y;
                #endif
                o.sketchUV=o.viewportUV;
                #if UNITY_UV_STARTS_AT_TOP
                    if(_SketchTex_TexelSize.y<0)o.sketchUV.y=1-o.sketchUV.y;
                #endif
                return o;
            }

            float ArtWeight(float2 worldPixels)
            {
                // Keep identical to VillageArtTransition.SampleWeight and the region
                // shader, except endpoint overrides never affect the final composite.
                if(_VillageArtMode<.5)return 0;
                if(_VillageArtMode<1.5)return 1;
                if(_VillageArtMode<2.5)return saturate(_VillageArtWeight);
                float curve=sin(worldPixels.y*.004)*28+sin(worldPixels.y*.010+1.4)*12;
                if(_VillageArtMode<3.5)
                {
                    float t=saturate((worldPixels.x-_VillageBoundaryPixels-curve+90)/180);
                    return t*t*(3-2*t);
                }
                float width=_VillageAuthoredRange.y-_VillageAuthoredRange.x;
                float authored=saturate((worldPixels.x-_VillageAuthoredRange.x)/width);
                float bend=clamp(curve/width,-.25,.25);
                authored=saturate(authored-bend*4*authored*(1-authored));
                return 1-authored*authored*(3-2*authored);
            }

            float4 frag(interpolator i):SV_Target
            {
                float2 worldPixels=_VillageViewportOrigin.xy+
                    _VillageViewportRight.xy*i.viewportUV.x+_VillageViewportUp.xy*i.viewportUV.y;
                float weight=ArtWeight(worldPixels);
                // Exact endpoint selection also preserves changed alpha silhouettes:
                // each image already contains its own correctly exposed background.
                if(weight<=0)return tex2D(_MainTex,i.colorUV);
                if(weight>=1)return tex2D(_SketchTex,i.sketchUV);
                return lerp(tex2D(_MainTex,i.colorUV),tex2D(_SketchTex,i.sketchUV),weight);
            }
            ENDCG
        }
    }
    Fallback Off
}
