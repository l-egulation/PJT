using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // The awakening of the playable opening (docs/design/story/opening/opening-playable.md parts 2-3; the erasure itself is in
    // StartingVillageWorld.OpeningChase): once the fronts meet the hero does not fall but turns to sketch where it stands,
    // softly from both hands; the pixel ink helper gathers out of streaks of light beside the well, leaps into the hero's
    // chest, and the colour comes back from there as the ink body. EndOpening clears all of it.
    public sealed partial class StartingVillageWorld
    {
        const float HeroRestoreSeconds = .6f;
        static readonly Color ErasedHero = new Color(.8f, .79f, .74f, 1);

        SpriteRenderer heroPose;
        CharacterClip heroClip;
        float heroClipStarted;
        bool heroReverse;
        Color heroTint = Color.white, tintFrom, tintTo;
        float tintStarted = -1, tintSeconds;
        public bool HeroAnimating => tintStarted >= 0 || (heroClip != null && Time.unscaledTime - heroClipStarted < heroClip.Duration) || HeroSketching || HeroGathering;

        public void DrainHero(float seconds) => TintHero(ErasedHero, seconds);
        public void RestoreHero() => TintHero(Color.white, HeroRestoreSeconds);

        void TintHero(Color target, float seconds)
        { tintFrom = heroTint; tintTo = target; tintSeconds = Mathf.Max(.01f, seconds); tintStarted = Time.unscaledTime; }

        // Plays a clip of the hero's current body (base_sword when none) over the live sprite, which is hidden meanwhile.
        // A forward clip holds its last frame until the next clip; a reversed one hands back.
        public void PlayHeroClip(string name, bool reverse)
        {
            var clip = HeroSet()?.Find(name);
            if (clip == null) { Debug.LogWarning("Opening hero clip missing: " + name); return; }
            if (clip.sprites == null) clip.Load();
            var visual = World.arena.Player.visual;
            if (!heroPose)
            {
                heroPose = new GameObject("Opening hero pose").AddComponent<SpriteRenderer>();
                heroPose.transform.SetParent(visual.transform, false);
                heroPose.sharedMaterial = visual.sharedMaterial; heroPose.sortingLayerID = visual.sortingLayerID; heroPose.sortingOrder = visual.sortingOrder;
            }
            heroClip = clip; heroReverse = reverse; heroClipStarted = Time.unscaledTime; gatherStarted = -1;
            visual.forceRenderingOff = true;
        }

        // ---- The first return (part 5): back from a death the hero lies at the well as a shape of ink while specks spiral
        // into it (the revive's burst), then its colour comes back; the death clip played backwards gets it up after.
        const float GatherColour = .6f;
        float gatherStarted = -1;
        public bool HeroGathering => gatherStarted >= 0 && Time.unscaledTime - gatherStarted < BurstSeconds + GatherColour;

        public void StartInkGather(float x)
        {
            var player = World.arena.Player;
            if (!float.IsNaN(x))
            {
                player.Position = new Vector2(x, GroundTop); player.velocity = Vector2.zero;
                Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
                World.RefreshCamera(0, true);
            }
            PlayHeroClip("death", false);
            if (heroClip == null) return;
            // Lying from the first frame: the clip's last pose, not the fall.
            heroClipStarted = Time.unscaledTime - heroClip.Duration; gatherStarted = Time.unscaledTime;
            StartBurst(player.Position.x);
        }

        // Ink black fading in while the specks gather, then the colour.
        Color GatherTint(float t) => t < BurstSeconds
            ? new Color(BurstInk.r, BurstInk.g, BurstInk.b, t / BurstSeconds)
            : Color.Lerp(BurstInk, Color.white, (t - BurstSeconds) / GatherColour);

        void TickHero()
        {
            var visual = World.arena.Player.visual;
            bool tinting = tintStarted >= 0;
            if (tinting)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - tintStarted) / tintSeconds);
                heroTint = Color.Lerp(tintFrom, tintTo, t);
                if (t >= 1) tintStarted = -1;
            }
            // Only while the opening tints the hero; the lobby otherwise leaves the sprite colour alone.
            if (visual && (tinting || heroTint != Color.white)) visual.color = heroTint;
            if (!heroPose || heroClip == null) return;
            float age = Time.unscaledTime - heroClipStarted, duration = heroClip.Duration;
            if (heroReverse && age >= duration) { EndHeroPose(); return; }
            heroPose.sprite = heroClip.At(heroReverse ? Mathf.Max(0, duration - age - .0001f) : age, false);
            heroPose.flipX = visual.flipX;
            heroPose.color = gatherStarted >= 0 ? GatherTint(Time.unscaledTime - gatherStarted) : heroTint;
        }

        void EndHeroPose()
        {
            if (heroPose) Destroy(heroPose.gameObject);
            heroPose = null; heroClip = null; gatherStarted = -1;
            var visual = World.arena.Player ? World.arena.Player.visual : null;
            if (visual && sketchStarted < 0) visual.forceRenderingOff = false;
        }

        // ---- The hero turning to sketch (HTML prototype HERO_SKETCH, in the 128x128 body frames: the prototype's 224x192
        // frames shifted by (48, 32)). Paper-tone body with a pencil outline, spreading in soft discs from both hands; when
        // it has covered the body the pose holds. The revive soaks the helper's ink in from the chest (OpeningInkSeep):
        // the sketch floods with ink, then the ink drains into the face as the colour comes back.

        static readonly Vector2[] SketchHands = { new Vector2(82, 101), new Vector2(58, 101) };   // image px from the top-left
        static readonly Vector2 SketchHeart = new Vector2(65, 86);
        const float SketchReach = 58, SketchSeconds = 3.8f, SketchSoft = 6, ReviveSeconds = OpeningInkSeep.Seconds, ReviveSettle = .15f;

        SpriteRenderer sketchView;
        Texture2D sketchTexture;
        Sprite sketchSprite, frozenFrame;
        Color32[] sketchPixels;
        float sketchStarted = -1, reviveStarted = -1;
        readonly Dictionary<Sprite, (Color32[] colour, Color32[] paper)> sketchFrames = new Dictionary<Sprite, (Color32[], Color32[])>();
        readonly Dictionary<Sprite, OpeningInkSeep.Maps> seepFrames = new Dictionary<Sprite, OpeningInkSeep.Maps>();

        public bool HeroSketching => sketchStarted >= 0 && (reviveStarted < 0
            ? Time.unscaledTime - sketchStarted < SketchSeconds
            : Time.unscaledTime - reviveStarted < ReviveSeconds + ReviveSettle);
        // 0 when the sketch starts at the hands, 1 when it has covered the body.
        public float HeroSketchReach => sketchStarted < 0 ? 0 : Mathf.Clamp01((Time.unscaledTime - sketchStarted) / SketchSeconds);
        public bool HeroReviving => reviveStarted >= 0;

        public void StartHeroSketch() { sketchStarted = Time.unscaledTime; reviveStarted = -1; frozenFrame = null; }

        // The helper's ink reaches the chest: the hero is the ink body from now on.
        public void StartHeroRevive()
        {
            if (sketchStarted < 0) sketchStarted = Time.unscaledTime - SketchSeconds;
            reviveStarted = Time.unscaledTime; frozenFrame = null;
            SetHeroBody(VillageBody);
            var player = World.arena.Player;
            StartSplash(new Vector2(player.Position.x + .2f * player.facing, player.Position.y + LeapChest));
        }

        void TickHeroSketch()
        {
            if (sketchStarted < 0) return;
            var visual = World.arena.Player.visual;
            float now = Time.unscaledTime;
            if (reviveStarted >= 0 && now - reviveStarted >= ReviveSeconds + ReviveSettle) { EndHeroSketch(); return; }
            float reach = HeroSketchReach;
            if (reviveStarted < 0 && reach >= 1 && !frozenFrame) frozenFrame = visual.sprite;
            var source = reviveStarted < 0 && frozenFrame ? frozenFrame : visual.sprite;
            if (!source) return;
            var (colour, paper) = SketchFrame(source);
            int w = (int)source.rect.width, h = (int)source.rect.height;
            if (!sketchTexture || sketchTexture.width != w || sketchTexture.height != h)
            {
                if (sketchTexture) Destroy(sketchTexture);
                sketchTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                sketchPixels = new Color32[w * h];
                if (sketchSprite) Destroy(sketchSprite);
                sketchSprite = null;
            }
            if (!sketchSprite) sketchSprite = Sprite.Create(sketchTexture, new Rect(0, 0, w, h), source.pivot / source.rect.size, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            float radius = SketchReach * Mathf.Pow(reach, 1.5f) + 2;
            OpeningInkSeep.Maps seep = null;
            if (reviveStarted >= 0 && !seepFrames.TryGetValue(source, out seep)) seepFrames[source] = seep = OpeningInkSeep.Build(colour, w, h, SketchHeart);
            float revived = now - reviveStarted;
            for (int y = 0; y < h; y++)
            {
                int top = h - 1 - y;   // image rows from the top, as the prototype measures
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x; var c = colour[i];
                    if (c.a == 0) { sketchPixels[i] = c; continue; }
                    if (seep != null)
                    {
                        var look = OpeningInkSeep.At(seep, i, x, top, revived);
                        sketchPixels[i] = look == OpeningInkSeep.Look.Paper ? paper[i] : look == OpeningInkSeep.Look.Ink ? OpeningInkSeep.Ink : c;
                        continue;
                    }
                    float m = 0;
                    foreach (var hand in SketchHands) m = Mathf.Max(m, Mathf.Clamp01((radius - Vector2.Distance(new Vector2(x, top), hand)) / SketchSoft));
                    var k = paper[i];
                    sketchPixels[i] = new Color32((byte)(c.r + (k.r - c.r) * m), (byte)(c.g + (k.g - c.g) * m), (byte)(c.b + (k.b - c.b) * m), c.a);
                }
            }
            sketchTexture.SetPixels32(sketchPixels); sketchTexture.Apply(false);
            if (!sketchView)
            {
                sketchView = new GameObject("Opening hero sketch").AddComponent<SpriteRenderer>();
                sketchView.transform.SetParent(visual.transform.parent, false);
                sketchView.sharedMaterial = visual.sharedMaterial; sketchView.sortingLayerID = visual.sortingLayerID;
            }
            sketchView.gameObject.SetActive(true);
            sketchView.sprite = sketchSprite; sketchView.flipX = visual.flipX; sketchView.sortingOrder = visual.sortingOrder;
            sketchView.transform.SetPositionAndRotation(visual.transform.position, visual.transform.rotation);
            sketchView.transform.localScale = visual.transform.localScale;
            visual.forceRenderingOff = true;
        }

        // Colour and paper-tone pixels of a body frame, made once per frame sprite.
        (Color32[] colour, Color32[] paper) SketchFrame(Sprite source)
        {
            if (sketchFrames.TryGetValue(source, out var frame)) return frame;
            var rect = source.textureRect; int w = (int)rect.width, h = (int)rect.height, stride = source.texture.width;
            var all = source.texture.GetPixels32();
            var colour = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) colour[y * w + x] = all[((int)rect.y + y) * stride + (int)rect.x + x];
            var paper = new Color32[w * h];
            bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && colour[y * w + x].a >= 128;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = colour[y * w + x];
                    if (c.a < 128) { colour[y * w + x] = paper[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    bool rim = !Opaque(x + 1, y) || !Opaque(x - 1, y) || !Opaque(x, y + 1) || !Opaque(x, y - 1);
                    float l = Mathf.Pow((c.r * .3f + c.g * .59f + c.b * .11f) / 255f, .7f);
                    paper[y * w + x] = rim ? new Color32(122, 114, 104, 255) : new Color32((byte)(150 + 95 * l), (byte)(142 + 96 * l), (byte)(130 + 96 * l), 255);
                    colour[y * w + x].a = 255;
                }
            return sketchFrames[source] = (colour, paper);
        }

        void EndHeroSketch()
        {
            sketchStarted = reviveStarted = -1; frozenFrame = null;
            if (sketchView) sketchView.gameObject.SetActive(false);
            var visual = World.arena.Player ? World.arena.Player.visual : null;
            if (visual && !heroPose) visual.forceRenderingOff = false;
        }

        // ---- The pixel ink helper (Hub/opening/ink-helper, individual 320x200 frames, foot anchor 160,194 from the
        // top-left; prototype make_sheet.py). Row 1: streaks of yellow light gather and it pops out (frame 21); row 2:
        // breathing with a blink every third round; row 3: the leap into the hero's chest (crouch, take off, three in the
        // air; the sheet's three flattening frames are not shown: on the chest the last air frame narrows and sinks in, cut
        // at the chest line). While the light gathers the world behind the hero darkens.

        const int AppearFrames = OpeningHelperArt.AppearFrames, IdleFrames = OpeningHelperArt.IdleFrames;
        const float SheetFrameSeconds = .07f, IdleStepSeconds = .16f, HelperPopSeconds = 1.45f, GatherDim = .42f;
        // The data's spot (x 17) is this far right of a hero that walked in and stopped at x 12.5.
        const float HelperGap = 4.5f;
        const float LeapCrouch = .24f, LeapTakeoff = .31f, LeapLand = .85f, LeapGone = 1.15f, LeapPeak = 3, LeapChest = .55f;

        SpriteRenderer helper, helperDim;
        Sprite[][] helperFrames;
        float helperStarted = -1, leapStarted = -1, helperX;
        // Until the pop while it gathers, until it lands while it leaps.
        public bool HelperAnimating => helper && (leapStarted >= 0 ? Time.unscaledTime - leapStarted < LeapLand : Time.unscaledTime - helperStarted < HelperPopSeconds);
        public bool HelperShown => helper && helper.enabled && helper.sprite;
        // Top centre of the helper's drawn pixels, where its speech bubble points.
        public Vector2 HelperTop => HelperShown && helperTops.TryGetValue(helper.sprite, out float top)
            ? new Vector2(helper.transform.position.x, helper.transform.position.y + top) : Vector2.zero;
        // Height of each frame's highest drawn pixel above its foot anchor, measured once from its texture.
        readonly Dictionary<Sprite, float> helperTops = new Dictionary<Sprite, float>();

        // The helper gathers at x, beside the hero (behind names a station whose art it stands behind; unused by the pixel
        // helper, which stands in front of the village).
        public void SpawnHelper(string sprite, float x, string behind)
        {
            DespawnHelper();
            try { helperFrames = OpeningHelperArt.Load(sprite); }
            catch (System.InvalidOperationException error) { Debug.LogError(error.Message); return; }
            foreach (var row in helperFrames)
            {
                foreach (var frame in row)
                {
                    var pixels = frame.texture.GetPixels32(); int highest = -1;
                    for (int py = OpeningHelperArt.Height - 1; py >= 0 && highest < 0; py--)
                        for (int px = 0; px < OpeningHelperArt.Width; px++)
                            if (pixels[py * OpeningHelperArt.Width + px].a > 32) { highest = py; break; }
                    helperTops[frame] = highest < 0 ? 0 : (highest + 1 - (OpeningHelperArt.Height - OpeningHelperArt.Anchor.y)) / 32f;
                }
            }
            // It stands at least HelperGap right of the hero, never on it: a hero that dashed into the well beat slides
            // past x 12 towards the helper's spot, hides it behind its body, and the helper's lines read as the hero's.
            x = Mathf.Max(x, World.arena.Player.Position.x + HelperGap);
            int hero = World.arena.Player.visual.sortingOrder;
            helperDim = Renderer("Opening helper dim", Pixel(), hero - 2);
            helperDim.color = new Color(0, 0, 0, 0);
            helper = Renderer("Opening helper", helperFrames[0][0], hero - 1);
            helperX = x; helper.transform.position = new Vector3(x, GroundTop, 0);
            helperStarted = Time.unscaledTime; leapStarted = -1;
        }

        // "leap": the helper jumps into the hero's chest; any other clip is not drawn by the pixel helper.
        public void PlayHelperClip(string clip)
        {
            if (!helper) return;
            if (clip == "leap") leapStarted = Time.unscaledTime;
            else Debug.LogWarning("Opening helper clip unknown: " + clip);
        }

        // ---- Sinking into the chest: the last air frame narrows to half its width and moves down by its height; what
        // passes the chest line (its foot row, held at the chest) is inside the hero and not drawn. k runs 0..1.
        const float SinkDepth = 46, SinkNarrow = .5f;
        Texture2D sinkTexture;
        Sprite sinkSprite, sinkSource;
        Color32[] sinkFrom, sinkPixels;

        Sprite SinkFrame(Sprite from, float k)
        {
            var rect = from.textureRect; int w = (int)rect.width, h = (int)rect.height;
            if (sinkSource != from)
            {
                DestroySink();
                var all = from.texture.GetPixels32(); int stride = from.texture.width;
                sinkFrom = new Color32[w * h]; sinkPixels = new Color32[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) sinkFrom[y * w + x] = all[((int)rect.y + y) * stride + (int)rect.x + x];
                sinkTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                sinkSprite = Sprite.Create(sinkTexture, new Rect(0, 0, w, h), from.pivot / rect.size, from.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sinkSource = from;
            }
            float s = 1 - Mathf.Pow(1 - Mathf.Clamp01(k), 1.4f), width = 1 - SinkNarrow * s, centre = from.pivot.x;
            int down = Mathf.RoundToInt(s * SinkDepth), chest = Mathf.FloorToInt(from.pivot.y) - 1;
            System.Array.Clear(sinkPixels, 0, sinkPixels.Length);
            for (int y = 0; y < h; y++)
            {
                int to = y - down;
                for (int x = 0; x < w; x++)
                {
                    var c = sinkFrom[y * w + x]; if (c.a < 8) continue;
                    int tx = Mathf.RoundToInt(centre + (x - centre) * width);
                    // The line it sinks through wavers by a pixel.
                    if (to < chest - (OpeningInkSeep.Hash01(tx, 5) > .6f ? 1 : 0) || tx < 0 || tx >= w) continue;
                    sinkPixels[to * w + tx] = c;
                }
            }
            sinkTexture.SetPixels32(sinkPixels); sinkTexture.Apply(false);
            return sinkSprite;
        }

        void DestroySink()
        {
            if (sinkSprite) Destroy(sinkSprite);
            if (sinkTexture) Destroy(sinkTexture);
            sinkSprite = null; sinkTexture = null; sinkSource = null; sinkFrom = sinkPixels = null;
        }

        public void DespawnHelper()
        {
            if (helper) Destroy(helper.gameObject);
            if (helperDim) Destroy(helperDim.gameObject);
            DestroySink();
            helperTops.Clear();
            helper = helperDim = null; helperFrames = null; helperStarted = leapStarted = -1;
        }

        void TickHelper()
        {
            if (!helper) return;
            float now = Time.unscaledTime, t = now - helperStarted;
            var player = World.arena.Player;
            if (helperDim)
            {
                var camera = World.View;
                float dim = leapStarted >= 0 ? 0 : Mathf.Min(1, t / .4f) * Mathf.Clamp01((2f - t) / .5f);
                helperDim.color = new Color(24 / 255f, 18 / 255f, 14 / 255f, GatherDim * dim);
                if (camera)
                {
                    float height = camera.orthographicSize * 2, width = height * camera.aspect;
                    helperDim.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0);
                    helperDim.transform.localScale = new Vector3(width * 32 + 64, height * 32 + 64, 1);
                }
            }
            Sprite frame; Vector2 at = new Vector2(helperX, GroundTop);
            if (leapStarted >= 0)
            {
                float u = now - leapStarted; var row = helperFrames[2];
                if (u < LeapCrouch) frame = row[u < .12f ? 0 : 1];
                else if (u < LeapTakeoff) frame = row[2];
                else if (u < LeapLand) { float k = (u - LeapTakeoff) / (LeapLand - LeapTakeoff); frame = row[k < .35f ? 3 : k < .65f ? 4 : 5]; }
                else if (u < LeapGone) frame = SinkFrame(row[5], (u - LeapLand) / (LeapGone - LeapLand));
                else frame = null;
                // Nearly even forwards, falling a little faster at the end; the arc peaks LeapPeak above the line between.
                float tx = player.Position.x + .2f * player.facing, ty = player.Position.y + LeapChest;
                if (u >= LeapLand) at = new Vector2(tx, ty);
                else if (u >= LeapTakeoff)
                {
                    float k = (u - LeapTakeoff) / (LeapLand - LeapTakeoff), e = k * (2 - k) * .35f + k * .65f;
                    at = new Vector2(helperX + (tx - helperX) * e, GroundTop + (ty - GroundTop) * k + LeapPeak * 4 * k * (1 - k));
                }
                // In the air it is drawn over the hero, so the ink is seen sinking in.
                helper.sortingOrder = player.visual.sortingOrder + (u >= LeapTakeoff ? 1 : -1);
            }
            else
            {
                int f = Mathf.FloorToInt(t / SheetFrameSeconds);
                if (f < AppearFrames) frame = helperFrames[0][Mathf.Min(f, helperFrames[0].Length - 1)];
                else
                {
                    int k = Mathf.FloorToInt((t - AppearFrames * SheetFrameSeconds) / IdleStepSeconds), n = IdleFrames - 1;
                    frame = helperFrames[1][k % (n * 3) == n * 3 - 1 ? n : k % n];
                }
            }
            helper.sprite = frame; helper.enabled = frame;
            helper.transform.position = new Vector3(Mathf.Round(at.x * 32) / 32, Mathf.Round(at.y * 32) / 32, 0);
            // The sheet already looks left, at a hero standing left of it.
            helper.flipX = player.Position.x > helperX && leapStarted < 0;
        }

        // ---- Ink specks pulled into the hero as the helper sinks in (prototype drawBurst): 26 specks spiral in over 0.6 s.

        const int BurstSpecks = 26;
        const float BurstSeconds = .6f;
        static readonly Color BurstInk = new Color32(21, 18, 16, 255);
        readonly List<SpriteRenderer> burst = new List<SpriteRenderer>();
        float burstStarted = -1, burstFromX;

        // Specks come from fromX's side (the helper's) and spiral into the hero.
        void StartBurst(float fromX)
        {
            burstStarted = Time.unscaledTime; burstFromX = fromX;
            int order = World.arena.Player.visual.sortingOrder + 2;
            while (burst.Count < BurstSpecks) { var speck = Renderer("Opening ink speck", Pixel(), order); speck.color = BurstInk; burst.Add(speck); }
        }

        void TickBurst()
        {
            if (burstStarted < 0) return;
            float a = (Time.unscaledTime - burstStarted) / BurstSeconds;
            if (a > 1.2f) { EndBurst(); return; }
            var player = World.arena.Player; float rest = Mathf.Max(0, 1 - a);
            for (int i = 0; i < burst.Count; i++)
            {
                float angle = i * 2.399f, r = (30 + (i * 37) % 60) * rest, size = i % 3 == 0 ? 3 : 2;
                float px = Mathf.Cos(angle) * r + (burstFromX - player.Position.x) * 32 * rest * .5f, py = 18 + Mathf.Sin(angle) * r * .6f + 20 * rest;
                burst[i].transform.position = new Vector3(Mathf.Round(player.Position.x * 32 + px) / 32, Mathf.Round(player.Position.y * 32 + py) / 32, 0);
                burst[i].transform.localScale = new Vector3(size, size, 1);
            }
        }

        void EndBurst()
        {
            foreach (var speck in burst) if (speck) Destroy(speck.gameObject);
            burst.Clear(); burstStarted = -1;
        }

        // ---- The splash as the helper hits the chest: nine drops of ink fly out and fall back onto the body within 0.34 s.

        const int SplashDrops = 9;
        const float SplashSeconds = .34f, SplashGravity = 420;   // px/s²
        readonly List<SpriteRenderer> splash = new List<SpriteRenderer>();
        float splashStarted = -1;
        Vector2 splashAt;

        void StartSplash(Vector2 at)
        {
            splashStarted = Time.unscaledTime; splashAt = at;
            int order = World.arena.Player.visual.sortingOrder + 2;
            while (splash.Count < SplashDrops) { var drop = Renderer("Opening ink splash", Pixel(), order); drop.color = BurstInk; splash.Add(drop); }
        }

        void TickSplash()
        {
            if (splashStarted < 0) return;
            float u = Time.unscaledTime - splashStarted;
            if (u > SplashSeconds) { EndSplash(); return; }
            for (int i = 0; i < splash.Count; i++)
            {
                float vx = (OpeningInkSeep.Hash01(i, 40) - .5f) * 110, vy = 40 + OpeningInkSeep.Hash01(i, 41) * 70, size = OpeningInkSeep.Hash01(i, 42) > .6f ? 2 : 1;
                var at = new Vector2(Mathf.Round(splashAt.x * 32 + vx * u) / 32, Mathf.Round(splashAt.y * 32 + vy * u - SplashGravity * u * u) / 32);
                splash[i].enabled = at.y >= GroundTop;
                splash[i].transform.position = new Vector3(at.x, at.y, 0);
                splash[i].transform.localScale = new Vector3(size, size, 1);
            }
        }

        void EndSplash()
        {
            foreach (var drop in splash) if (drop) Destroy(drop.gameObject);
            splash.Clear(); splashStarted = -1;
        }

        // A 1 px white sprite at 32 PPU for flat shapes (the dim layer, the specks).
        static Sprite pixel;
        static Sprite Pixel()
        {
            if (pixel) return pixel;
            var t = new Texture2D(1, 1) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, Color.white); t.Apply();
            return pixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 32);
        }

        void TickOpeningScene()
        {
            TickErasure(World.arena.Controls.Current.SimulationPaused ? 0 : Time.deltaTime);
            TickResidents(); TickHero(); TickHeroFear(); TickHelper(); TickHeroSketch(); TickBurst(); TickSplash(); TickEraseColour(); TickOpeningGuide();
        }

        void EndOpeningScene()
        {
            ClearErasure(); EndHeroPose(); EndHeroSketch(); DespawnHelper(); EndBurst(); EndSplash(); EndOpeningGuide();
            heroTint = Color.white; tintStarted = -1;
            if (World.arena.Player && World.arena.Player.visual) World.arena.Player.visual.color = Color.white;
        }
    }
}
