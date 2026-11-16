Shader "Hidden/VirtualWallOutlineSoil"
{
    Properties {
        _MainTex("Approved terrain atlas",2D)="white" {}
        _SoilPaper("Soil paper",Vector)=(.86,.87,.84,1)
        _TextureContrast("Texture contrast",Float)=1
        _OutlineWidth("Outline width",Float)=0
        _OutlineColor("Outline color",Vector)=(.75,.71,.65,1)
        _Rim("Rim renderer",Float)=0
        _Hatch0("Hatch 0",2D)="black" {}
        _Hatch1("Hatch 1",2D)="black" {}
        _Hatch2("Hatch 2",2D)="black" {}
        _Hatch3("Hatch 3",2D)="black" {}
        _BackdropBeam("Beam bounds",Vector)=(0,0,1,1)
        _BackdropColumn("Column bounds",Vector)=(0,0,1,1)
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        // The rim (2026-11-12, user choice A "가장자리 한 단 진하게"): the outer _OutlineWidth px of every support in
        // _OutlineColor, one shade under the soil, never a black line. URP draws one pass, so a support is drawn twice:
        // a rim renderer (_Rim 1, the whole shape in _OutlineColor) one sorting order under every body renderer
        // (_Rim 0, the shape inset by _OutlineWidth). Supports overlap; a rim never shows inside another support.
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_Hatch0,_Hatch1,_Hatch2,_Hatch3;
            float4 _BackdropBeam,_BackdropColumn,_SoilPaper;
            float _TextureContrast,_OutlineWidth,_Rim;
            float4 _OutlineColor;
            struct v2f { float4 vertex:SV_POSITION;float2 world:TEXCOORD0; };
            v2f vert(appdata_base v) {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;o.world=float2(world.x,-world.y)*32;return o;
            }
            float box(float2 p,float4 bounds) {
                float2 center=(bounds.xy+bounds.zw)*.5;
                float2 halfSize=(bounds.zw-bounds.xy)*.5;
                float2 q=abs(p-center)-max(halfSize-4,0);
                return length(max(q,0))+min(max(q.x,q.y),0)-4;
            }
            float hash(float2 n) { return frac(sin(dot(n,float2(127.1,311.7)))*43758.5453); }
            // Signed distance to this support's beam and column; negative inside.
            float Edge(float2 world) {
                float2 edgePixel=floor(world)+.5;
                return min(box(edgePixel,_BackdropBeam),box(edgePixel,_BackdropColumn));
            }
            fixed3 Body(float2 world) {
                float2 p=floor(world/2)*2+1;
                float2 cell=floor(p/32),local=p-cell*32;
                float variant=floor(hash(cell)*4);
                float2 uv=(float2(256+variant*32+local.x,32-local.y)+.5)/float2(512,384);
                fixed4 soil=tex2D(_MainTex,uv);
                float2 hatchCell=floor(p/64),huv=frac(p/64);
                float h=hash(hatchCell+19);fixed4 ink;
                if(h<.25)ink=tex2D(_Hatch0,huv);else if(h<.5)ink=tex2D(_Hatch1,huv);
                else if(h<.75)ink=tex2D(_Hatch2,huv);else ink=tex2D(_Hatch3,huv);
                float darkness=(1-dot(soil.rgb,float3(.299,.587,.114)))*.32+ink.a*.095;
                fixed3 paper=GammaToLinearSpace(_SoilPaper.rgb);
                return paper-darkness*_TextureContrast;
            }
            fixed4 frag(v2f i):SV_Target {
                float edge=Edge(i.world);clip(-edge);
                if(_Rim>0)return fixed4(GammaToLinearSpace(_OutlineColor.rgb),1);
                clip(-edge-_OutlineWidth);
                return fixed4(Body(i.world),1);
            }
            ENDCG
        }
    }
}
