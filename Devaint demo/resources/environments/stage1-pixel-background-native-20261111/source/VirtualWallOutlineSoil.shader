Shader "Hidden/VirtualWallOutlineSoil"
{
    Properties {
        _MainTex("Approved terrain atlas",2D)="white" {}
        _SoilPaper("Soil paper",Vector)=(.86,.87,.84,1)
        _TextureContrast("Texture contrast",Float)=1
        _OutlineWidth("Outline width",Float)=0
        _OutlineColor("Outline color",Vector)=(.75,.71,.65,1)
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
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_Hatch0,_Hatch1,_Hatch2,_Hatch3;
            float4 _BackdropBeam,_BackdropColumn,_SoilPaper;
            float _TextureContrast,_OutlineWidth;
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
            fixed4 frag(v2f i):SV_Target {
                float2 p=floor(i.world/2)*2+1;
                float2 edgePixel=floor(i.world)+.5;
                float edge=min(box(edgePixel,_BackdropBeam),box(edgePixel,_BackdropColumn));clip(-edge);
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
                fixed3 body=paper-darkness*_TextureContrast;
                float rim=_OutlineWidth>0&&edge>=-_OutlineWidth?(.8+.2*hash(floor(i.world/12))):0;
                return fixed4(lerp(body,GammaToLinearSpace(_OutlineColor.rgb),rim),1);
            }
            ENDCG
        }
    }
}
