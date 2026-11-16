using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // 하늘길 도착 환영 (docs/design/world/village/starting-village.md 「하늘길」): reaching the bell tower stone rings the tower. Three rings of
    // light dots spread from the bell, five pixel fireworks burst over the tower one after another, paper confetti falls
    // across the view for 2.5 s and the view flashes once softly; the keeper's welcome bubble is the village screens' part
    // (TemporaryGameGui.SkyParkour). Everything runs on unscaled time and leaves nothing behind.
    public sealed partial class StartingVillageWorld
    {
        static readonly Color32[] SkyPartyColours =
        {
            new Color32(212, 80, 42, 255), new Color32(235, 165, 22, 255), new Color32(2, 100, 140, 255),
            new Color32(253, 238, 218, 255), new Color32(214, 120, 150, 255), new Color32(120, 170, 210, 255),
        };
        // The bell's pixel in the tower picture: its middle column and the row of its mouth's top (pixel 112x368, colour 280x311).
        const float PixelTowerBellX = 47, PixelTowerBellRow = 98, ColourTowerBellX = 140, ColourTowerBellRow = 96;

        sealed class SkySpark { public SpriteRenderer art; public Vector2 at, v; public float age, life, gravity, drag, sway, spin; public bool paper; }
        readonly List<SkySpark> skySparks = new List<SkySpark>();
        readonly Queue<SpriteRenderer> skySparkPool = new Queue<SpriteRenderer>();
        readonly List<(float at, Vector2 where, int kind)> skyPartyQueue = new List<(float, Vector2, int)>();
        Sprite skySparkDot; SpriteRenderer skyFlash; float skyPartyStart = -1, skyConfettiUntil, skyConfettiNext;
        Vector2 skyBellAt;
        System.Random skyPartyRandom = new System.Random(5);
        public bool SkyCelebrating => skyPartyStart >= 0;

        float PartyRandom(float a, float b) => a + (float)skyPartyRandom.NextDouble() * (b - a);

        void StartSkyCelebration()
        {
            if (skyGoal == null) return;
            if (!skySparkDot)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                texture.SetPixel(0, 0, Color.white); texture.Apply();
                skySparkDot = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
                skyPropMade.Add(texture); skyPropMade.Add(skySparkDot);
            }
            float now = Time.unscaledTime;
            skyPartyStart = now; skyPartyQueue.Clear();
            float line = GroundTop + skyGoal.data.y, centre = skyGoal.data.x + skyGoal.data.w / 2;
            for (int i = 0; i < 3; i++) skyPartyQueue.Add((now + i * .28f, skyBellAt, 0));
            Vector2[] bursts = { new Vector2(-3.2f, 10.5f), new Vector2(3.4f, 11.6f), new Vector2(-.4f, 13.2f), new Vector2(-5.6f, 8.4f), new Vector2(5.8f, 9.1f) };
            for (int i = 0; i < bursts.Length; i++) skyPartyQueue.Add((now + .15f + i * .3f, new Vector2(centre, line) + bursts[i], 1));
            skyConfettiUntil = now + 2.5f; skyConfettiNext = now + .1f;
            if (!skyFlash)
            {
                skyFlash = Renderer("Sky arrival flash", skySparkDot, 30);
                skyObjects.Add(skyFlash.gameObject);
            }
            PresentationSfx.Play("story-ink-awaken");
        }

        SpriteRenderer SkySparkArt()
        {
            var r = skySparkPool.Count > 0 ? skySparkPool.Dequeue() : null;
            if (!r) { r = Renderer("Sky spark", skySparkDot, 5); skyObjects.Add(r.gameObject); }
            r.enabled = true;
            return r;
        }

        void SkySpray(Vector2 where, int kind)
        {
            if (kind == 0)
            {
                // A ring of light dots from the bell, cream and gold.
                for (int i = 0; i < 28; i++)
                {
                    float a = i * Mathf.PI * 2 / 28;
                    var s = new SkySpark { art = SkySparkArt(), at = where, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5.5f, life = .7f, drag = 2.2f };
                    s.art.color = i % 2 == 0 ? (Color)SkyPartyColours[3] : SkyPartyColours[1];
                    s.art.transform.localScale = new Vector3(2, 2, 1);
                    skySparks.Add(s);
                }
                return;
            }
            // A firework: two colours, 56 sparks (mostly 3 px) thrown round, falling and slowing as they fade.
            var first = SkyPartyColours[skyPartyRandom.Next(SkyPartyColours.Length)];
            var second = SkyPartyColours[skyPartyRandom.Next(SkyPartyColours.Length)];
            for (int i = 0; i < 56; i++)
            {
                float a = i * Mathf.PI * 2 / 56 + PartyRandom(-.08f, .08f), speed = PartyRandom(4.2f, 7.6f);
                var s = new SkySpark { art = SkySparkArt(), at = where, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, life = PartyRandom(1f, 1.5f), gravity = 4.5f, drag = 1.6f };
                s.art.color = i % 3 == 0 ? (Color)second : first;
                float size = PartyRandom(0, 1) < .65f ? 3 : 2;
                s.art.transform.localScale = new Vector3(size, size, 1);
                skySparks.Add(s);
            }
        }

        void SkyConfetti(Vector3 view, float halfWidth, float halfHeight)
        {
            var s = new SkySpark
            {
                art = SkySparkArt(), paper = true, life = 5,
                at = new Vector2(view.x + PartyRandom(-halfWidth, halfWidth), view.y + halfHeight + .3f),
                v = new Vector2(PartyRandom(-.4f, .4f), -PartyRandom(1.6f, 2.8f)), sway = PartyRandom(0, 6.3f), spin = PartyRandom(5, 11),
            };
            s.art.color = SkyPartyColours[skyPartyRandom.Next(SkyPartyColours.Length)];
            skySparks.Add(s);
        }

        void TickSkyCelebration()
        {
            if (skyPartyStart < 0) return;
            float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            for (int i = skyPartyQueue.Count - 1; i >= 0; i--)
                if (now >= skyPartyQueue[i].at) { SkySpray(skyPartyQueue[i].where, skyPartyQueue[i].kind); skyPartyQueue.RemoveAt(i); }
            var view = World.View ? World.View.transform.position : Vector3.zero;
            float halfHeight = World.View ? World.View.orthographicSize : 8.4f, halfWidth = World.View ? halfHeight * World.View.aspect : 15;
            while (now < skyConfettiUntil && now >= skyConfettiNext) { SkyConfetti(view, halfWidth, halfHeight); skyConfettiNext += .035f; }

            if (skyFlash)
            {
                float flash = Mathf.Clamp01(1 - (now - skyPartyStart) / .45f) * .35f;
                skyFlash.enabled = flash > 0;
                skyFlash.transform.position = new Vector3(view.x, view.y, 0);
                skyFlash.transform.localScale = new Vector3(halfWidth * 64 + 64, halfHeight * 64 + 64, 1);
                skyFlash.color = new Color(1, .97f, .88f, flash);
            }

            for (int i = skySparks.Count - 1; i >= 0; i--)
            {
                var s = skySparks[i];
                s.age += dt;
                if (s.paper)
                {
                    // Paper flutters: drifts side to side and flips (its width swings through zero).
                    s.at += new Vector2(s.v.x + Mathf.Sin(s.age * 2.4f + s.sway) * .9f, s.v.y) * dt;
                    float flip = Mathf.Abs(Mathf.Sin(s.age * s.spin)) * 3;
                    s.art.transform.localScale = new Vector3(Mathf.Max(1, Mathf.Round(flip)), 2, 1);
                    bool below = s.at.y < view.y - halfHeight - .5f;
                    if (below || s.age >= s.life) { RetireSpark(i); continue; }
                }
                else
                {
                    s.v *= Mathf.Max(0, 1 - s.drag * dt); s.v.y -= s.gravity * dt; s.at += s.v * dt;
                    if (s.age >= s.life) { RetireSpark(i); continue; }
                    var c = s.art.color; c.a = Mathf.Clamp01(1.4f - s.age / s.life * 1.4f + .25f); s.art.color = c;
                }
                s.art.transform.position = new Vector3(Mathf.Round(s.at.x * 32) / 32, Mathf.Round(s.at.y * 32) / 32, 0);
            }
            if (skySparks.Count == 0 && skyPartyQueue.Count == 0 && now >= skyConfettiUntil && now - skyPartyStart > .5f)
            {
                skyPartyStart = -1;
                if (skyFlash) skyFlash.enabled = false;
            }
        }

        void RetireSpark(int i)
        {
            var s = skySparks[i];
            if (s.art) { s.art.enabled = false; s.art.color = Color.white; skySparkPool.Enqueue(s.art); }
            skySparks.RemoveAt(i);
        }
    }
}
