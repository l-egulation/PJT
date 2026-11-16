// Runtime paper material for licensed hit particles, sampled on the 960x540 game pixel grid.
Shader "Deviant/HitFeedback/PaperInk"
{
    Properties
    {
        _MainTex("Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Ink("Hatching",Color)=(0.133,0.110,0.094,1)
        _OutlineColor("Outline",Color)=(0.1019608,0.1019608,0.1019608,1)
        _Paper("Body",Color)=(1,0.992,0.965,1)
        _Cut("Transparent below",Range(0,1))=0.2
        _Core("Hatched below",Range(0,1))=0.55
        _HatchStep("Hatch spacing (game px)",Float)=3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;half4 _Color,_Ink,_OutlineColor,_Paper;half _Cut,_Core;float _HatchStep;
            CBUFFER_END
            Varyings vert(Attributes v)
            { Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color*_Color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o; }
            half Glow(float2 uv,half4 tint)
            { half4 c=SAMPLE_TEXTURE2D_LOD(_MainTex,sampler_MainTex,uv,0)*tint;return max(c.r,max(c.g,c.b))*c.a; }
            half4 frag(Varyings i):SV_Target
            {
                float gamePixel=max(1.0,_ScreenParams.y/540.0);
                float2 cell=floor(i.positionCS.xy/gamePixel);
                float2 toCentre=(cell+0.5)*gamePixel-i.positionCS.xy;
                float2 stepX=ddx(i.uv)*gamePixel,stepY=ddy(i.uv)*gamePixel;
                float2 uv=i.uv+ddx(i.uv)*toCentre.x+ddy(i.uv)*toCentre.y;
                half value=Glow(uv,i.color);
                clip(value-_Cut);
                half around=min(min(Glow(uv+stepX,i.color),Glow(uv-stepX,i.color)),min(Glow(uv+stepY,i.color),Glow(uv-stepY,i.color)));
                bool outline=around<_Cut;
                bool hatch=value<_Core&&fmod(cell.x+cell.y,_HatchStep)<1.0;
                return half4(outline?_OutlineColor.rgb:(hatch?_Ink.rgb:_Paper.rgb),1);
            }
            ENDHLSL
        }
    }
}
