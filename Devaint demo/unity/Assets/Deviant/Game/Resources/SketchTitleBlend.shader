Shader "Deviant/UI/SketchTitleBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Colour",2D)="white" {}
        _SketchTex ("Sketch",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _SketchShift ("Sketch UV offset",Vector)=(0,0,0,0)
        _UseFront ("Use sketch front",Float)=1
        _Daylight ("Approved daylight",Float)=0
        _Outline ("Sketch character outline",Float)=0
        _StencilComp ("Stencil Comparison",Float)=8
        _Stencil ("Stencil ID",Float)=0
        _StencilOp ("Stencil Operation",Float)=0
        _StencilWriteMask ("Stencil Write Mask",Float)=255
        _StencilReadMask ("Stencil Read Mask",Float)=255
        _ColorMask ("Color Mask",Float)=15
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
            sampler2D _MainTex,_SketchTex;
            float4 _SketchTex_TexelSize,_SketchShift;
            fixed4 _Color;
            float _UseFront,_Daylight,_Outline,_SketchTitleProgress;
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;
                // Overlay UI batching changes object transforms. Clip position stays in the visible viewport.
                o.screen=ComputeScreenPos(o.vertex);return o;
            }
            float hashCell(int2 p)
            {
                uint n=(uint)(p.x+71)*374761393u^(uint)(p.y+13)*668265263u;
                n=(n^(n>>13))*1274126177u;return (n^(n>>16))/4294967295.0;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 title=float2(i.screen.x/i.screen.w,1-i.screen.y/i.screen.w)*float2(1600,900);
                fixed4 colour=tex2D(_MainTex,i.uv);
                fixed4 sketch=tex2D(_SketchTex,i.uv+_SketchShift.xy);
                if(_Daylight>.5)
                {
                    float t=saturate(title.y/730);
                    float3 top=float3(207,237,248)/255,mid=float3(227,244,244)/255,low=float3(247,247,224)/255;
                    float3 sky=t<=.58?lerp(top,mid,t/.58):lerp(mid,low,(t-.58)/.42);
                    #ifndef UNITY_COLORSPACE_GAMMA
                    sky=GammaToLinearSpace(sky);
                    #endif
                    colour=fixed4(sky,1);
                }
                if(_Outline>.5)
                {
                    float alpha=0;
                    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                        alpha=max(alpha,tex2D(_SketchTex,i.uv+float2(x,y)*_SketchTex_TexelSize.xy).a);
                    if(sketch.a<.5)
                    {
                        float3 ink=float3(56,54,47)/255;
                        #ifndef UNITY_COLORSPACE_GAMMA
                        ink=GammaToLinearSpace(ink);
                        #endif
                        sketch=fixed4(ink,alpha);
                    }
                }
                int2 cell=(int2)floor(title/12);
                float scatter=(hashCell(cell/3)-.5)*108+(hashCell(cell)-.5)*28;
                float threshold=(1600-cell.x*12-6+scatter+90)/1780;
                float ink=step(threshold,_SketchTitleProgress)*step(.00001,_SketchTitleProgress);
                if(_SketchTitleProgress>=1)ink=1;
                return lerp(colour,sketch,ink*_UseFront)*i.color;
            }
            ENDCG
        }
    }
}
