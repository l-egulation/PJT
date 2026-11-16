Shader "Deviant/Lighting/LitInkOutline"
{
    Properties
    {
        _MainTex("Sprite", 2D) = "white" {}
        _MaskTex("Light mask", 2D) = "white" {}
        _NormalMap("Normal map", 2D) = "bump" {}
        _SurfaceNormal("Bound surface normal", 2D) = "bump" {}
        _OutlineColor("Ink outline", Color) = (0.1333,0.1098,0.0941,1)
        _OutlineStrength("Outline strength", Range(0,1)) = 1
        [HideInInspector] _WindowCount("Directional windows", Integer) = 0
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable external alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex LitVertex
            #pragma target 3.5
            #pragma fragment LitFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color : COLOR; float3 windowWorld : TEXCOORD6; float2 spritePosition : TEXCOORD7; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            #include "WindowDirectionalLighting.hlsl"
            TEXTURE2D(_SurfaceNormal);
            SAMPLER(sampler_SurfaceNormal);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half _OutlineStrength;
                DEVIANT_WINDOW_PROPERTIES
            CBUFFER_END
            #define DEVIANT_WINDOW_FUNCTIONS
            #include "WindowDirectionalLighting.hlsl"
            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.spritePosition = input.positionOS.xy;
                o.windowWorld = TransformObjectToWorld(input.positionOS);
                return o;
            }
            half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }
            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half4 lit = CommonLitFragment(input, input.color);
                if (_WindowCount > 0)
                {
                    SetUpSpriteInstanceProperties();
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_SurfaceNormal, sampler_SurfaceNormal, input.uv));
                    normalTS.xy *= unity_SpriteProps.xy;
                    half3 normalWS = TransformTangentToWorld(normalTS, half3x3(TransformObjectToWorldDir(float3(1,0,0)),
                        TransformObjectToWorldDir(float3(0,1,0)), TransformObjectToWorldDir(float3(0,0,-1))));
                    [loop] for (int w = 0; w < min(_WindowCount, 4); w++)
                    {
                        float2 windowPoint = mul(_WindowWorldToLocal[w], float4(input.windowWorld, 1)).xy;
                        lit.rgb += main.rgb * _WindowColor[w].rgb * saturate(dot(normalize(normalWS), _WindowToLight[w].xyz))
                            * WindowCoverage(w, windowPoint);
                    }
                }
                // Preserve the existing 1/32 local-unit outline, including higher-PPU frames.
                float2 du = ddx(input.uv), dv = ddy(input.uv);
                float2 dx = ddx(input.spritePosition), dy = ddy(input.spritePosition);
                float determinant = max(abs(du.x * dv.y - du.y * dv.x), 1e-12);
                float2 unitsPerUV = float2(length(dx * dv.y - dy * du.y), length(dy * du.x - dx * dv.x)) / determinant;
                float2 texel = max(_MainTex_TexelSize.xy, rcp(max(unitsPerUV * 32.0, float2(1e-5,1e-5))));
                half neighbour = max(max(Alpha(input.uv + float2(texel.x,0)), Alpha(input.uv - float2(texel.x,0))),
                                     max(Alpha(input.uv + float2(0,texel.y)), Alpha(input.uv - float2(0,texel.y))));
                half outline = neighbour * _OutlineColor.a * _OutlineStrength * input.color.a * (1-main.a);
                half alpha = main.a + outline;
                if (alpha <= 0) return 0;
                return half4((lit.rgb * main.a + _OutlineColor.rgb * outline) / alpha, alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex NormalVertex
            #pragma target 3.5
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            struct Attributes { COMMON_2D_NORMALS_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            #include "WindowDirectionalLighting.hlsl"
            TEXTURE2D(_SurfaceNormal);
            SAMPLER(sampler_SurfaceNormal);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half _OutlineStrength;
                DEVIANT_WINDOW_PROPERTIES
            CBUFFER_END
            Varyings NormalVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 NormalFragment(Varyings input) : SV_Target
            {
                SetUpSpriteInstanceProperties();
                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_SurfaceNormal, sampler_SurfaceNormal, input.uv));
                return NormalsRenderingShared(main, normalTS, input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }
    }
}
