Shader "RuralNature32/Soft Palette"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Saturation("Saturation",Float)=1
        _Contrast("Contrast",Float)=1
        _Lift("Lift (sRGB)",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Saturation,_Contrast;float4 _Lift;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS);o.uv=input.uv;o.color=input.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb=LinearToSRGB(c.rgb);
                #endif
                float l=dot(c.rgb,float3(.2126,.7152,.0722));
                c.rgb=saturate((lerp(l.xxx,c.rgb,_Saturation)-.5)*_Contrast+.5+_Lift.rgb);
                c.rgb=floor(c.rgb*255+.5)/255;
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb=SRGBToLinear(c.rgb);
                #endif
                return c;
            }
            ENDHLSL
        }
    }
}
