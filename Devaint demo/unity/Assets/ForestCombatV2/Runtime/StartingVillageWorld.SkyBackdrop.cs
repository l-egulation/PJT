using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // 하늘길의 하늘 (docs/design/world/village/starting-village.md 「하늘길」, as the HTML prototype's drawSky): above the village the
    // village's own sky picture fades out over a paper sky that turns to dusk and then to an ink night with twinkling stars
    // at the top. Clouds (the village's sketch clouds, cut in two) hang at fixed heights all the way up, drift slowly and
    // slide down at their own depth as the camera climbs. At the village ground (camera at rest) nothing changes.
    public sealed partial class StartingVillageWorld
    {
        // Height (units above the climb's rest view) → top and bottom colour of the sky.
        static readonly (float h, Color32 top, Color32 bottom)[] SkyStops =
        {
            (0, new Color32(244, 240, 229, 255), new Color32(244, 240, 229, 255)),
            (50, new Color32(240, 236, 226, 255), new Color32(246, 241, 230, 255)),
            (160, new Color32(228, 222, 214, 255), new Color32(244, 228, 208, 255)),
            (250, new Color32(96, 98, 116, 255), new Color32(214, 176, 150, 255)),
            (318, new Color32(38, 38, 52, 255), new Color32(92, 82, 96, 255)),
        };
        // The view's bottom when the hero stands on the village ground (the ground line sits 97 px above it).
        const float SkyRestBelowGround = 97 / 32f;
        const string SkyCloudArt = "Hub/village-art-preview/clouds-sketch", SkyCloudFallback = "Hub/approved-background/sketch/clouds";
        // The two clouds of the 600x160 picture (top-left pixel rects), as the prototype's cloud-a and cloud-b.
        static readonly RectInt[] SkyCloudCuts = { new RectInt(36, 22, 328, 113), new RectInt(353, 70, 218, 68) };

        sealed class SkyCloud { public SpriteRenderer art; public float y, x, depth; public int part; }
        sealed class SkyStar { public SpriteRenderer art; public float x, y, twinkle; }
        readonly List<SkyCloud> skyClouds = new List<SkyCloud>();
        readonly List<SkyStar> skyStars = new List<SkyStar>();
        readonly List<Object> skyBackdropMade = new List<Object>();
        SpriteRenderer skyGradient; Texture2D skyGradientTexture;

        void BuildSkyBackdrop()
        {
            skyGradientTexture = new Texture2D(1, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var gradient = Sprite.Create(skyGradientTexture, new Rect(0, 0, 1, 2), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            skyBackdropMade.Add(skyGradientTexture); skyBackdropMade.Add(gradient);
            skyGradient = Renderer("Sky backdrop", gradient, -121);
            skyGradient.enabled = false;
            skyObjects.Add(skyGradient.gameObject);

            var cuts = SkyCloudSprites();
            if (cuts != null)
            {
                int seed = 7;
                float Next() { seed = (int)((long)seed * 16807 % 2147483647); return seed / 2147483647f; }
                for (float y = 14; y < 340; y += 5 + Next() * 6)
                {
                    var cloud = new SkyCloud { y = y, x = Next() * 900 - 250 };
                    cloud.part = Next() < .5f ? 0 : 1; cloud.depth = .35f + Next() * .4f;
                    cloud.art = Renderer("Sky cloud", cuts[cloud.part], -119);
                    cloud.art.enabled = false;
                    skyObjects.Add(cloud.art.gameObject);
                    skyClouds.Add(cloud);
                }
            }

            var dotTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            dotTexture.SetPixel(0, 0, Color.white); dotTexture.Apply();
            var dot = Sprite.Create(dotTexture, new Rect(0, 0, 1, 1), Vector2.zero, 32, 0, SpriteMeshType.FullRect);
            skyBackdropMade.Add(dotTexture); skyBackdropMade.Add(dot);
            int starSeed = 3;
            float Star() { starSeed = (int)((long)starSeed * 16807 % 2147483647); return starSeed / 2147483647f; }
            for (int i = 0; i < 90; i++)
            {
                var star = new SkyStar { x = Star() * 960, y = Star() * 540 };
                float size = Star() < .15f ? 2 : 1; star.twinkle = Star() * 6;
                star.art = Renderer("Sky star", dot, -120);
                star.art.transform.localScale = new Vector3(size, size, 1);
                star.art.enabled = false;
                skyObjects.Add(star.art.gameObject);
                skyStars.Add(star);
            }
        }

        // The two clouds cut from the village's sketch clouds; each keeps its own connected pixels (and any inside the cut
        // that do not touch its border), dropping slivers of the neighbouring cloud the rectangle catches.
        Sprite[] SkyCloudSprites()
        {
            var source = Resources.Load<Texture2D>(SkyCloudArt) ?? Resources.Load<Texture2D>(SkyCloudFallback);
            if (!source || !source.isReadable) return null;
            var all = source.GetPixels32();
            var sprites = new Sprite[SkyCloudCuts.Length];
            for (int c = 0; c < SkyCloudCuts.Length; c++)
            {
                var cut = SkyCloudCuts[c]; int w = cut.width, h = cut.height;
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = all[(source.height - 1 - (cut.y + h - 1 - y)) * source.width + cut.x + x];
                var label = new int[w * h]; for (int i = 0; i < label.Length; i++) label[i] = -1;
                var sizes = new List<int>(); var edges = new List<bool>(); var queue = new Queue<int>();
                for (int start = 0; start < px.Length; start++)
                {
                    if (px[start].a == 0 || label[start] >= 0) continue;
                    int id = sizes.Count, count = 0; bool edge = false;
                    label[start] = id; queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int at = queue.Dequeue(), x = at % w, y = at / w; count++;
                        edge |= x == 0 || y == 0 || x == w - 1 || y == h - 1;
                        void Visit(int nx, int ny) { if (nx < 0 || ny < 0 || nx >= w || ny >= h) return; int n = ny * w + nx; if (px[n].a == 0 || label[n] >= 0) return; label[n] = id; queue.Enqueue(n); }
                        Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
                    }
                    sizes.Add(count); edges.Add(edge);
                }
                int biggest = 0; for (int i = 1; i < sizes.Count; i++) if (sizes[i] > sizes[biggest]) biggest = i;
                for (int i = 0; i < px.Length; i++) if (label[i] >= 0 && label[i] != biggest && edges[label[i]]) px[i] = new Color32(0, 0, 0, 0);
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                texture.SetPixels32(px); texture.Apply(false, true);
                sprites[c] = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.zero, 32, 0, SpriteMeshType.FullRect);
                skyBackdropMade.Add(texture); skyBackdropMade.Add(sprites[c]);
            }
            return sprites;
        }

        static (Color top, Color bottom) SkyColours(float h)
        {
            for (int i = 1; i < SkyStops.Length; i++)
                if (h <= SkyStops[i].h)
                {
                    var a = SkyStops[i - 1]; var b = SkyStops[i]; float k = (h - a.h) / (b.h - a.h);
                    return (Color.Lerp(a.top, b.top, k), Color.Lerp(a.bottom, b.bottom, k));
                }
            var last = SkyStops[SkyStops.Length - 1];
            return (last.top, last.bottom);
        }

        // After the village background (LateUpdate), so it uses this frame's camera.
        void TickSkyBackdrop()
        {
            if (!skyGradient || !World.View) return;
            var view = World.View.transform.position;
            float halfHeight = World.View.orthographicSize, halfWidth = halfHeight * World.View.aspect;
            float bottom = view.y - halfHeight, left = view.x - halfWidth;
            float lift = Mathf.Max(0, bottom - (GroundTop - SkyRestBelowGround));
            float time = Time.unscaledTime;

            // The village's sky picture gives way to the paper sky as the camera climbs.
            float paper = Mathf.Clamp01(1 - lift / 135);
            if (sky) { var tint = sky.color; tint.a = paper; sky.color = tint; }
            skyGradient.enabled = paper < 1;
            if (skyGradient.enabled)
            {
                var (top, low) = SkyColours(lift + 5.2f);
                skyGradientTexture.SetPixel(0, 0, low); skyGradientTexture.SetPixel(0, 1, top); skyGradientTexture.Apply(false);
                skyGradient.transform.position = new Vector3(view.x, view.y, 0);
                skyGradient.transform.localScale = new Vector3(halfWidth * 2 + 2, halfHeight * 2, 1);
            }

            float starAlpha = Mathf.Clamp01((lift + 5.2f - 250) / 55);
            foreach (var star in skyStars)
            {
                star.art.enabled = starAlpha > 0;
                if (!star.art.enabled) continue;
                star.art.transform.position = new Vector3(left + Mathf.Round(star.x) / 32, bottom + (540 - Mathf.Round(star.y)) / 32, 0);
                star.art.color = new Color32(244, 236, 216, (byte)(255 * starAlpha * (.55f + .45f * Mathf.Sin(time * 1.7f + star.twinkle))));
            }

            // Clouds: placed in screen pixels from the view's bottom-left as the prototype, faded in over the first units of
            // the climb so the village at rest keeps its own sky.
            float span = Sky.worldWidth * 16 + 900, enter = Mathf.Clamp01(lift / 4);
            foreach (var cloud in skyClouds)
            {
                var size = cloud.art.sprite.rect.size;
                float up = (cloud.y - lift * cloud.depth) * 32 * .9f;
                bool visible = enter > 0 && up > -size.y - 10 && up < 540 + 10;
                cloud.art.enabled = visible;
                if (!visible) continue;
                float dusk = Mathf.Clamp01((cloud.y - 220) / 90);
                float cx = cloud.x * 3 + Mathf.Sin(time * .05f + cloud.y) * 30 - left * 32 * cloud.depth * .5f;
                cx = (cx % span + span) % span - 400;
                cloud.art.transform.position = new Vector3(left + Mathf.Round(cx) / 32, bottom + Mathf.Round(up) / 32, 0);
                cloud.art.color = new Color(1, 1, 1, enter * (.55f + cloud.depth * .5f) * (1 - dusk * .45f));
            }
        }

        void DisposeSkyBackdrop()
        {
            foreach (var made in skyBackdropMade) if (made) Destroy(made);
            skyBackdropMade.Clear();
        }
    }
}
