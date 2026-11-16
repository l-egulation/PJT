Shader "Deviant/Chapter2/LanternContactImpact"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float3 positionOS : POSITION; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            V Vertex(A input)
            {
                V output; output.positionCS = TransformObjectToHClip(input.positionOS); output.color = input.color; return output;
            }
            half4 Fragment(V input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
