Shader "ForestCombatV2/PlayerOutline"
{
    // URP Sprite-Unlit-Default plus a 1/32 local-unit ink outline in transparent
    // texels that touch the sprite (up/down/left/right). Frames must keep a
    // transparent border; Full Rect meshes leave room for the outline.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline color", Color) = (0.1333,0.1098,0.0941,1)
        _OutlineStrength ("Outline strength", Range(0,1)) = 1
        _Contrast ("Sprite contrast", Range(1,2)) = 1
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment OutlineFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 spritePosition : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half _OutlineStrength;
                half _Contrast;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.spritePosition = input.positionOS.xy;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                // Change luminance only: per-channel contrast clips the greens out of brown wooden props.
                half luminance = dot(sprite.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half contrasted = saturate((luminance - 0.18h) * _Contrast + 0.18h);
                sprite.rgb *= contrasted / max(luminance, 0.0001h);
                // Derive local sprite units per UV, so PPU128 review frames get
                // four texture texels of ink, equal to one texel at native PPU32.
                // Local coordinates retain renderer scaling and flipping behavior.
                float2 du = ddx(input.uv), dv = ddy(input.uv);
                float2 dx = ddx(input.spritePosition), dy = ddy(input.spritePosition);
                float determinant = max(abs(du.x * dv.y - du.y * dv.x), 1e-12);
                float2 unitsPerUV = float2(length(dx * dv.y - dy * du.y), length(dy * du.x - dx * dv.x)) / determinant;
                float2 texel = max(_MainTex_TexelSize.xy, rcp(max(unitsPerUV * 32.0, float2(1e-5, 1e-5))));
                half neighbour = max(max(Alpha(input.uv + float2(texel.x, 0)), Alpha(input.uv - float2(texel.x, 0))),
                                     max(Alpha(input.uv + float2(0, texel.y)), Alpha(input.uv - float2(0, texel.y))));
                // The outline fades with the renderer's alpha but keeps its own ink color.
                half outline = neighbour * _OutlineColor.a * _OutlineStrength * input.color.a * (1 - sprite.a);
                half alpha = sprite.a + outline;
                if (alpha <= 0) return 0;
                half3 rgb = (sprite.rgb * sprite.a + _OutlineColor.rgb * outline) / alpha;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
