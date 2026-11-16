Shader "Deviant/HitFeedback/Particle"
{
    Properties
    {
        _MainTex("Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Desaturate("Grey blend",Range(0,1))=0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha [_DstBlend]
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;half4 _Color;half _Desaturate;
            CBUFFER_END
            Varyings vert(Attributes v)
            { Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color*_Color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o; }
            half4 frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                // Corpse debris fades toward its own luminance; 0 keeps every other user's colour unchanged.
                c.rgb=lerp(c.rgb,dot(c.rgb,half3(.299h,.587h,.114h)).xxx,_Desaturate);
                return c*i.color;
            }
            ENDHLSL
        }
    }
}
