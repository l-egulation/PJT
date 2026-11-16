Shader "Deviant/Lighting/WindowBeam"
{
    Properties { [HideInInspector] _WindowCount("Directional windows", Integer) = 0 }
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
            #pragma target 3.5
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WindowDirectionalLighting.hlsl"
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 localXY : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                DEVIANT_WINDOW_PROPERTIES
            CBUFFER_END
            #define DEVIANT_WINDOW_FUNCTIONS
            #include "WindowDirectionalLighting.hlsl"
            Varyings Vertex(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS);
                output.localXY = input.positionOS.xy; return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float mask = WindowCoverage(0, input.localXY);
                if (mask <= 0) return 0;
                return half4(_WindowColor[0].rgb, _WindowColor[0].a * mask);
            }
            ENDHLSL
        }
    }
}
