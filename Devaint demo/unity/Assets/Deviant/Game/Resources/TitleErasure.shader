Shader "Deviant/UI/TitleErasure"
{
    Properties
    {
        [PerRendererData] _MainTex ("Title", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glitch ("Glitch", Float) = 0
        _Tick ("Unscaled transition time", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Glitch, _Tick;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float tick=floor(_Tick/.022),band=floor(uv.y*47);
                float h=hash(float2(band,tick));
                uv.x=saturate(uv.x+(h-.5)*.065*step(.72,h)*_Glitch);
                fixed4 c=tex2D(_MainTex,uv)*i.color;
                float noise=hash(floor(uv*float2(128,72))+tick);
                c.rgb=lerp(c.rgb,noise.xxx,saturate(_Glitch*.115));
                c.rgb*=1-step(.7,frac(uv.y*235.25+tick*.25))*_Glitch*.1;
                return c;
            }
            ENDCG
        }
    }
}
