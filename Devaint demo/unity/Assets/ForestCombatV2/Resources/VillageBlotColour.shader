// The colour that comes back to the starting village with each palette piece (docs/design/progression/village-palette-pieces.md,
// StartingVillageWorld.ColourBlots): a sprite drawn over its sketch twin that shows only inside the colour blots. The
// mask lives in world 4 px cells with a fixed paper-grain noise, so its shape stays put when the camera moves; a blot's
// edge fades over 40 px as in the HTML opening prototype. Globals: _VillageBlots (x, y, radius in units), _VillageBlotCount,
// _VillageBlotGrain (32x32 grain, 0.5 = none).
Shader "Hidden/Deviant/VillageBlotColour"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_VillageBlotGrain); SAMPLER(sampler_point_repeat);
            float4 _VillageBlots[128];
            float _VillageBlotCount;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv; o.color = v.color; o.world = world.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                // 4 px cells (32 px per unit), the grain tiled every 32 cells.
                float2 cell = floor(i.world * 8);
                float grain = SAMPLE_TEXTURE2D(_VillageBlotGrain, sampler_point_repeat, (cell + .5) / 32).r - .5;
                float2 centre = (cell + .5) / 8;
                float best = -9;
                for (int n = 0; n < (int)_VillageBlotCount; n++)
                {
                    float4 b = _VillageBlots[n];
                    best = max(best, (b.z - distance(centre, b.xy)) * 32 / 40 + .5 + grain);
                }
                c.a *= saturate(best);
                return c;
            }
            ENDHLSL
        }
    }
}
