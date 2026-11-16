Shader "ForestCombatV2/UnlitHit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite alpha", 2D) = "white" {}
        _Color ("Flash tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // Core2D carries the SpriteRenderer flip (unity_SpriteProps): without it the flash ignored flipX and was
            // drawn mirrored against the body it covers (playtest 2026-11-04, the millstone).
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(UnityFlipSprite(input.positionOS,unity_SpriteProps.xy));
                output.uv=input.uv;output.color=input.color*_Color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
                return half4(input.color.rgb,input.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
