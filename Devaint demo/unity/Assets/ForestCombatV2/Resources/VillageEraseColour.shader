// The opening's erasure (docs/design/story/opening/opening-playable.md part 2, StartingVillageWorld.EraseColour): a colour twin
// drawn over its sketch twin while a front sweeps the village. The colour goes in 4-art-pixel cells, not along a line
// (HTML prototype 2026-11-04 「잘게 점점이」, 「불규칙하게」): each cell is erased a little before or after the front by an
// amount fixed to the world (large blots ±40 px, small blots ±60 px, per cell ±90 px), the front itself wobbles slowly,
// and every piece has its own lead that sways at its own pace (「사물마다 따로따로」). Globals: _VillageErase (left front x,
// right front x, unused, time; units). Per twin: _EraseOffset (units the front reaches this piece early, negative late).
// _EraseInvert 1 draws the opposite cells: the sketch under a twin shows only where the colour is gone, so a sketch drawn
// differently from its colour picture (the far market row) never shows round the colour (playtest 2026-11-06).
Shader "Hidden/Deviant/VillageEraseColour"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite", 2D) = "white" {}
        _EraseOffset("Erase offset", Float) = 0
        _EraseInvert("Show erased cells only", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _VillageErase;
            CBUFFER_START(UnityPerMaterial)
                float _EraseOffset;
                float _EraseInvert;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(UnityFlipSprite(v.positionOS.xyz, unity_SpriteProps.xy));
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv; o.color = v.color; o.world = world.xy;
                return o;
            }

            // The prototype's hash3 and value noise, on world cells.
            float Hash(int x, int y, int s)
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (float)(h ^ (h >> 16)) / 4294967295.0;
            }
            float Noise(float2 p, float scale, int s)
            {
                float2 f = p / scale; int2 i = (int2)floor(f); float2 u = f - floor(f); u = u * u * (3 - 2 * u);
                float a = Hash(i.x, i.y, s), b = Hash(i.x + 1, i.y, s), c = Hash(i.x, i.y + 1, s), d = Hash(i.x + 1, i.y + 1, s);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float Wave(float y, float t, float k) { return 22 * sin(y * .017 + t * .55 + k) + 12 * sin(y * .043 - t * .9 + k * 2) + 6 * sin(y * .11 + t * 1.7 + k * 3); }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                // Art pixels are 1/32 unit; a cell is 4 of them, anchored to the world so the pattern never slides.
                int2 cell = (int2)floor(i.world * 8);
                float2 p = cell * 4 + 2;
                float off = (Noise(p, 120, 1) - .5) * 80 + (Noise(p, 10, 2) - .5) * 120 + (Hash(cell.x, cell.y, 3) - .5) * 180;
                float t = _VillageErase.w, lead = _EraseOffset * 32;
                float left = _VillageErase.x * 32 + Wave(p.y, t, 0) + lead, right = _VillageErase.y * 32 - Wave(p.y, t, 4.1) - lead;
                bool erased = p.x <= left + off || p.x >= right - off;
                if (erased != (_EraseInvert > .5)) c.a = 0;
                return c;
            }
            ENDHLSL
        }
    }
}
