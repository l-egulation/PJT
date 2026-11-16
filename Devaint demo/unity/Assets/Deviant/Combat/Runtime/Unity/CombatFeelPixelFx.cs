using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deviant.Combat
{
    // Test-branch pixel effects (port of 전투테스트/손맛 "새 피격 이펙트"): a per-hit 4-frame burst
    // (crescent cut + uneven spikes + ink splash + twinkles, 4 tones, dithered fade), streak sparks,
    // spinning shards, ink drops, a fast ring for heavy/kill, and blade-tip swing trails.
    // Everything is software-drawn at 32 px per unit into one camera-sized texture so pixels stay
    // on the game grid. Ages use the combat clock, so hit-stop holds the first frame.
    // Presentation only: never reads or writes damage, hit boxes, timing or actor state.
    [DefaultExecutionOrder(800)]
    public sealed class CombatFeelPixelFx : MonoBehaviour
    {
        public struct TrailSample { public Vector2 hilt, tip; public float at; public TrailSample(Vector2 hilt, Vector2 tip, float at) { this.hilt = hilt; this.tip = tip; this.at = at; } }
        const int PPU = 32;
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static readonly Color32 Ink = new Color32(23, 23, 22, 255), Gloss = new Color32(74, 63, 58, 255);

        sealed class Spec { public int S, spikes, shard, spark, drop; public float[] frames; public float R, cresR, cresT, cresL, splat, ring; public bool radial; }
        static readonly Spec Basic = new Spec { S = 128, frames = new[] { 34f, 34, 40, 44 }, R = 30, cresR = 46, cresT = 8, cresL = 44, spikes = 7, splat = 1, shard = 4, spark = 4, drop = 3, ring = 0 };
        static readonly Spec Heavy = new Spec { S = 192, frames = new[] { 52f, 42, 44, 50 }, R = 46, cresR = 66, cresT = 13, cresL = 66, spikes = 9, splat = 1.5f, shard = 7, spark = 7, drop = 6, ring = 50 };
        static readonly Spec Kill = new Spec { S = 176, frames = new[] { 48f, 40, 44, 50 }, R = 54, cresR = 54, cresT = 10, cresL = 54, spikes = 11, splat = 1.7f, shard = 8, spark = 8, drop = 7, ring = 42, radial = true };

        // Frames are baked on a worker thread (pure math, no Unity objects); drawing starts once they are ready.
        sealed class Burst { public Spec spec; public Color32[][] frames; public System.Threading.Tasks.Task<Color32[][]> job; public Vector2 at; public float born; }
        sealed class Ring { public Vector2 at; public float born, radius; }
        sealed class Part { public int type; public Vector2 p, v; public float t, life, floor; public int size, spin; public Color32[] sprite; public bool landed; }
        sealed class Arc
        {
            public CombatCast cast; public bool cleave, started, dead; public float delay, t, lastNow, rx, ry, inner, max, tail, dur, a0, a1, ox, oy; public float? lastA;
            public int face; public Vector2 pivot; public readonly List<TrailSample> hist = new List<TrailSample>();
        }
        struct QueuedTrail { public List<TrailSample> samples; public float now, tail, max, rim; }

        CombatArena arena;
        CombatFeelSettings settings;
        SpriteRenderer view;
        Texture2D texture; Sprite sprite; Color32[] canvas; int W, H; float left, top;
        // Only the rows touched last frame are cleared and the upload is skipped when nothing changed.
        int dirtyMin = int.MaxValue, dirtyMax = -1, drawnCount; bool uploadedEmpty = true;
        readonly List<Burst> bursts = new List<Burst>();
        readonly List<Ring> rings = new List<Ring>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Arc> arcs = new List<Arc>();
        readonly List<QueuedTrail> trails = new List<QueuedTrail>();
        readonly System.Random random = new System.Random(4231);
        float lastNow = -1;
        Color32 body, core, shade, deep;
        public int ActiveCount => bursts.Count + rings.Count + parts.Count + arcs.Count;
        public int BurstCount => bursts.Count;
        public int ArcCount => arcs.Count;
        public int LastTrailSamples { get; private set; }
        public int LastDrawnPixels { get; private set; }
        float R() => (float)random.NextDouble();
        // Baked frames are leased per burst and returned when it ends: a fresh bake allocated 0.7–1.5 MB per hit,
        // so one swing through a crowd forced several GC pauses (~12 ms each) in the fight frames.
        readonly Dictionary<int, Stack<Color32[][]>> framePool = new Dictionary<int, Stack<Color32[][]>>();
        public int PooledFrameSets { get { int n = 0; foreach (var s in framePool.Values) n += s.Count; return n; } }
        Color32[][] LeaseFrames(int size)
        {
            if (framePool.TryGetValue(size, out var stack) && stack.Count > 0) return stack.Pop();
            var frames = new Color32[4][]; for (int f = 0; f < 4; f++) frames[f] = new Color32[size * size]; return frames;
        }
        // A bake still running on the worker keeps writing into its arrays, so only finished bakes return them.
        void ReturnFrames(Burst b)
        {
            if (b.job != null && !b.job.IsCompleted || b.job != null && b.job.IsFaulted) return;
            var frames = b.frames ?? b.job?.Result; if (frames == null) return;
            if (!framePool.TryGetValue(b.spec.S, out var stack)) framePool.Add(b.spec.S, stack = new Stack<Color32[][]>());
            stack.Push(frames); b.frames = null; b.job = null;
        }
        // Per-worker scratch for one bake frame (shape masks and depth); never shared between threads.
        [ThreadStatic] static bool[] scratchM, scratchK;
        [ThreadStatic] static int[] scratchD;
        static T[] Scratch<T>(ref T[] buffer, int size) { if (buffer == null || buffer.Length < size) buffer = new T[size]; else Array.Clear(buffer, 0, size); return buffer; }

        public void Bind(CombatArena owner, CombatFeelSettings configuration)
        {
            arena = owner; settings = configuration;
            body = settings.EffectColor; core = settings.EffectCore; shade = Scale(body, .7f); deep = Scale(body, .52f);
            view = gameObject.AddComponent<SpriteRenderer>(); view.enabled = false;
            if (arena.spriteMaterial) view.sharedMaterial = arena.spriteMaterial;
        }
        static Color32 Scale(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
        public void Clear() { foreach (var b in bursts) ReturnFrames(b); bursts.Clear(); rings.Clear(); parts.Clear(); arcs.Clear(); trails.Clear(); lastNow = -1; if (view) view.enabled = false; }
        void OnDestroy() { if (sprite) Destroy(sprite); if (texture) Destroy(texture); }

        // ── 생성 ──────────────────────────────────────────────────────────────
        public void Hit(CombatActor target, Vector2 origin, bool heavy, bool killed, int direction, float angle, bool slash)
        {
            var spec = killed ? Kill : heavy ? Heavy : Basic;
            float now = arena.Now, rad = angle * Mathf.Deg2Rad;
            int fwd = Mathf.Cos(rad) * direction >= 0 ? 1 : -1;
            var rng = new System.Random(random.Next());
            var leased = LeaseFrames(spec.S);
            bursts.Add(new Burst { spec = spec, job = System.Threading.Tasks.Task.Run(() => BuildBurst(spec, rad, slash, fwd, rng, leased)), at = origin, born = now });
            float ux = Mathf.Cos(rad) * fwd, uy = Mathf.Sin(rad) * fwd, floor = target.Position.y + 2f / PPU, aim = Mathf.Atan2(uy, ux);
            for (int i = 0; i < spec.spark; i++)
            {
                float a = aim + (R() - .5f) * (killed ? Mathf.PI * 2 : 1.2f), s = (300 + R() * 280) * (heavy ? 1.2f : 1) / PPU;
                parts.Add(new Part { type = 0, p = origin, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, life = .08f + R() * .1f });
            }
            for (int i = 0; i < spec.shard; i++)
            {
                float a = aim + (R() - .5f) * (killed ? Mathf.PI * 2 : 1.6f), s = (150 + R() * 220) / PPU;
                parts.Add(new Part { type = 1, p = origin, v = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s + 90f / PPU), life = .32f + R() * .12f, sprite = ShardSprite(out int n), size = n + 2, spin = R() < .5f ? 1 : -1, floor = floor });
            }
            for (int i = 0; i < spec.drop; i++)
                parts.Add(new Part { type = 2, p = origin, v = new Vector2(ux * (90 + R() * 180) + (R() - .5f) * 60, uy * 120 + 100 + R() * 140) / PPU, life = .55f, floor = floor - 1f / PPU, size = 2 + (R() < .5f ? 0 : 1) });
            if (spec.ring > 0) rings.Add(new Ring { at = origin, born = now, radius = spec.ring });
        }
        // Hammer basic: a heavy overhead-to-front arc around the shoulder, finishing at contact.
        public void HammerArc(CombatCast cast)
            => arcs.Add(new Arc { cast = cast, face = cast.FeelFacing, delay = Mathf.Max(0, cast.ImpactTime - .09f - cast.Age), dur = .09f, tail = .13f, max = 26,
                a0 = 125, a1 = -38, rx = 46f / PPU, ry = 46f / PPU, inner = .42f, ox = 4f / PPU, oy = 30f / PPU, lastNow = arena.Now });
        // 강베기: a wide forward crescent that reaches the far edge of the real hit box.
        public void CleaveArc(CombatCast cast)
            => arcs.Add(new Arc { cast = cast, cleave = true, face = cast.FeelFacing, delay = Mathf.Max(0, cast.ImpactTime - .06f - cast.Age), dur = .075f, tail = .2f, max = 40,
                a0 = 105, a1 = -8, inner = .72f, ox = 14f / PPU, oy = 26f / PPU, lastNow = arena.Now });
        // Sword: the blade ribbon's real hilt/tip samples, re-drawn as a pixel crescent each frame.
        public void QueueTrail(List<TrailSample> samples, float now, float tail)
        { if (samples.Count > 1) trails.Add(new QueuedTrail { samples = samples, now = now, tail = tail, max = 22, rim = 3 }); }

        // ── 진행 (전투 시계) ──────────────────────────────────────────────────
        void Step(float now, float dt)
        {
            // A long frame hitch must not skip an arc that has not been drawn yet.
            foreach (var arc in arcs) StepArc(arc, now, Mathf.Min(dt, 1f / 30));
            arcs.RemoveAll(a => a.dead);
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i];
                if ((now - b.born) * 1000 < b.spec.frames[0] + b.spec.frames[1] + b.spec.frames[2] + b.spec.frames[3]) continue;
                ReturnFrames(b); bursts.RemoveAt(i);
            }
            rings.RemoveAll(r => now - r.born >= .1f);
            if (dt <= 0) return;
            foreach (var q in parts)
            {
                q.t += dt;
                if (q.type == 0) { float k = Mathf.Exp(-7 * dt); q.v.x *= k; q.v.y = q.v.y * k - 260f / PPU * dt; }
                else if (q.type == 1) { q.v.y -= 700f / PPU * dt; q.v.x *= Mathf.Exp(-1.5f * dt); if (q.p.y <= q.floor && q.v.y < 0) { q.p.y = q.floor; q.v.y = -q.v.y * .35f; q.v.x *= .6f; } }
                else { q.v.y -= 900f / PPU * dt; if (q.p.y <= q.floor && q.v.y < 0) { q.p.y = q.floor; q.v = Vector2.zero; q.landed = true; } }
                q.p += q.v * dt;
            }
            parts.RemoveAll(q => q.t >= q.life);
        }
        void StepArc(Arc arc, float now, float dt)
        {
            if (!arc.cast || arc.cast.PresentationCancelled && !arc.started) { arc.dead = true; return; }
            arc.t += dt; if (arc.t < arc.delay) return;
            var player = arena.Player;
            if (!arc.started)
            {
                arc.started = true;
                arc.pivot = player.Position + new Vector2(arc.face * arc.ox, arc.oy);
                if (arc.cleave)
                {
                    var bounds = arc.cast.FeelPrimaryBounds;
                    float far = arc.face > 0 ? bounds.max.x : bounds.min.x;
                    arc.rx = Mathf.Max(1.5f, Mathf.Abs(far - arc.pivot.x)); arc.ry = arc.rx * .39f;
                    arc.max = 40 * Mathf.Clamp(arc.rx / 6.25f, .55f, 1);
                }
            }
            if (!arc.cleave) arc.pivot = player.Position + new Vector2(arc.face * arc.ox, arc.oy);
            float u = Mathf.Min(1, (arc.t - arc.delay) / arc.dur), a = arc.a0 + (arc.a1 - arc.a0) * (1 - (1 - u) * (1 - u));
            float from = arc.lastA ?? arc.a0; int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(a - from) / 4));
            if (arc.lastA == null || a != arc.lastA)
                for (int i = arc.lastA == null ? 0 : 1; i <= n; i++)
                {
                    float b = (from + (a - from) * i / n) * Mathf.Deg2Rad, cx = Mathf.Cos(b) * arc.face, sy = Mathf.Sin(b);
                    arc.hist.Add(new TrailSample(arc.pivot + new Vector2(cx * arc.rx * arc.inner, sy * arc.ry * arc.inner), arc.pivot + new Vector2(cx * arc.rx, sy * arc.ry), now - (float)(n - i) / n * dt));
                }
            arc.lastA = a;
            while (arc.hist.Count > 1 && now - arc.hist[0].at > arc.tail) arc.hist.RemoveAt(0);
            if (u >= 1 && arc.hist.Count > 0 && now - arc.hist[arc.hist.Count - 1].at > arc.tail) arc.dead = true;
        }

        // ── 그리기 ────────────────────────────────────────────────────────────
        void LateUpdate() => Refresh();
        // Also called by capture tools that drive the arena manually between frames.
        public void Refresh()
        {
            if (!arena || !arena.ViewCamera || !view) return;
            float now = arena.Now, dt = lastNow < 0 || now < lastNow ? 0 : now - lastNow; lastNow = now;
            Step(now, dt);
            // The canvas is made before the first effect (no hitch on the first hit) and only ever grows,
            // so a camera size change never rebuilds the texture.
            var cam = arena.ViewCamera;
            int w = Mathf.CeilToInt(cam.orthographicSize * 2 * cam.aspect * PPU) + 4, h = Mathf.CeilToInt(cam.orthographicSize * 2 * PPU) + 4;
            if (!texture || w > W || h > H)
            {
                if (sprite) Destroy(sprite); if (texture) Destroy(texture);
                W = w; H = h; texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Combat feel pixel fx" };
                canvas = new Color32[W * H]; sprite = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(.5f, .5f), PPU); view.sprite = sprite;
                dirtyMin = int.MaxValue; dirtyMax = -1; uploadedEmpty = false;
            }
            bool any = ActiveCount > 0 || trails.Count > 0;
            view.enabled = any;
            if (!any) { trails.Clear(); return; }
            var player = arena.Player.visual;
            if (player) { view.sortingLayerID = player.sortingLayerID; view.sortingOrder = player.sortingOrder + 30; }
            float cx = Mathf.Round(cam.transform.position.x * PPU) / PPU, cy = Mathf.Round(cam.transform.position.y * PPU) / PPU;
            left = cx - W / 2f / PPU; top = cy + H / 2f / PPU;
            transform.position = new Vector3(cx, cy, 0); transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
            if (dirtyMax >= dirtyMin) Array.Clear(canvas, dirtyMin * W, (dirtyMax - dirtyMin + 1) * W);
            dirtyMin = int.MaxValue; dirtyMax = -1; drawnCount = 0;
            LastTrailSamples = 0;
            foreach (var trail in trails) { DrawSmear(trail.samples, trail.now, trail.tail, trail.max, trail.rim); LastTrailSamples += trail.samples.Count; }
            foreach (var arc in arcs) if (arc.started) DrawSmear(arc.hist, now, arc.tail, arc.max, arc.cleave ? 5 : 4);
            foreach (var b in bursts) DrawBurst(b, now);
            foreach (var r in rings) DrawRing(r, now);
            foreach (var q in parts) DrawPart(q);
            trails.Clear();
            LastDrawnPixels = drawnCount;
            if (drawnCount == 0 && uploadedEmpty) return;
            texture.SetPixelData(canvas, 0); texture.Apply(false); uploadedEmpty = drawnCount == 0;
        }
        Vector2 ToCanvas(Vector2 world) => new Vector2((world.x - left) * PPU, (top - world.y) * PPU);
        void Plot(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int row = H - 1 - y, k = row * W + x; if (canvas[k].a == 0) drawnCount++; canvas[k] = c;
            if (row < dirtyMin) dirtyMin = row; if (row > dirtyMax) dirtyMax = row;
        }
        void Rect(int x, int y, int w, int h, Color32 c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Plot(x + i, y + j, c); }
        static bool Dither(int x, int y, float density) => Bayer[(x & 3) + (y & 3) * 4] / 16f < density;

        // Capture tools draw right after a hit; they wait for the worker bake instead of skipping a frame.
        public void WaitForBursts() { foreach (var b in bursts) if (b.frames == null && b.job != null) try { b.job.Wait(); } catch (Exception) { } }
        void DrawBurst(Burst b, float now)
        {
            if (b.frames == null)
            {
                if (b.job == null || !b.job.IsCompleted) return;
                if (b.job.IsFaulted) { Debug.LogException(b.job.Exception); b.job = null; return; }
                b.frames = b.job.Result;
            }
            float age = (now - b.born) * 1000; int f = 0;
            while (f < 3 && age >= b.spec.frames[f]) { age -= b.spec.frames[f]; f++; }
            var frame = b.frames[f]; int S = b.spec.S; var c = ToCanvas(b.at);
            int x0 = Mathf.RoundToInt(c.x - S / 2f), y0 = Mathf.RoundToInt(c.y - S / 2f);
            for (int j = 0; j < S; j++) for (int i = 0; i < S; i++) { var p = frame[j * S + i]; if (p.a != 0) Plot(x0 + i, y0 + j, p); }
        }
        void DrawRing(Ring r, float now)
        {
            float u = Mathf.Min(1, (now - r.born) / .1f), rad = r.radius * (1 - (1 - u) * (1 - u)), dens = 1 - u * .7f; int w = u < .5f ? 2 : 1;
            var c = ToCanvas(r.at);
            for (int a = 0; a < 72; a++)
            {
                if (Bayer[a & 15] / 16f >= dens) continue;
                float th = a / 72f * Mathf.PI * 2; int X = Mathf.RoundToInt(c.x + Mathf.Cos(th) * rad), Y = Mathf.RoundToInt(c.y + Mathf.Sin(th) * rad * .8f);
                Rect(X - 1, Y - 1, w + 2, w + 2, Ink); Rect(X, Y, w, w, body);
            }
        }
        void DrawPart(Part q)
        {
            var c = ToCanvas(q.p);
            if (q.type == 0)
            {
                float sp = q.v.magnitude * PPU, len = Mathf.Min(14, sp * .028f), ux = q.v.x * PPU / Mathf.Max(sp, .001f), uy = -q.v.y * PPU / Mathf.Max(sp, .001f);
                int w = q.t / q.life < .5f ? 2 : 1, n = Mathf.Max(1, Mathf.RoundToInt(len));
                for (int i = 0; i <= n; i++) Rect(Mathf.RoundToInt(c.x - ux * i) - 1, Mathf.RoundToInt(c.y - uy * i) - 1, w + 2, w + 2, Ink);
                for (int i = 1; i <= n; i++) Rect(Mathf.RoundToInt(c.x - ux * i), Mathf.RoundToInt(c.y - uy * i), w, w, body);
                Rect(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), w, w, core);
            }
            else if (q.type == 1)
            {
                if (q.t / q.life > .7f && (Mathf.FloorToInt(q.t * 40) & 1) == 1) return;     // 끝날 때 깜빡이며 사라짐
                int turn = ((Mathf.FloorToInt(q.t / .05f) % 4) * q.spin + 4) % 4, n = q.size, x0 = Mathf.RoundToInt(c.x) - n / 2, y0 = Mathf.RoundToInt(c.y) - n / 2;
                for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
                {
                    var p = q.sprite[j * n + i]; if (p.a == 0) continue;
                    int a = i, b = j;
                    for (int k = 0; k < turn; k++) { int t = a; a = n - 1 - b; b = t; }
                    Plot(x0 + a, y0 + b, p);
                }
            }
            else
            {
                float u = q.t / q.life; int x = Mathf.RoundToInt(c.x), y = Mathf.RoundToInt(c.y);
                if (u > .7f && !Dither(x, y, (1 - u) / .3f)) return;
                Rect(x, y, q.landed ? q.size + 2 : q.size, q.landed ? 1 : q.size, Ink);
            }
        }
        Color32[] ShardSprite(out int n)
        {
            n = 3 + (int)(R() * 3); int s = n + 2; var px = new Color32[s * s];
            void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < s && y < s) px[y * s + x] = c; }
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) if (i + j < n) for (int b = -1; b <= 1; b++) for (int a = -1; a <= 1; a++) Set(i + 1 + a, j + 1 + b, Ink);
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) if (i + j < n) Set(i + 1, j + 1, body);
            Set(1, 1, core); return px;
        }

        // ── 피격 버스트 굽기 ──────────────────────────────────────────────────
        struct Spike { public float a, len, w; }
        struct Drop { public float a, d, r; }
        Color32[][] BuildBurst(Spec sp, float rad, bool slash, int fwd, System.Random rng, Color32[][] frames)
        {
            float R() => (float)rng.NextDouble();
            int S = sp.S; float c = S / 2f, dx = Mathf.Cos(rad) * fwd, dy = -Mathf.Sin(rad) * fwd, nx = -dy, ny = dx, aim = Mathf.Atan2(dy, dx);
            bool basic = sp == Basic;
            var spikes = new Spike[sp.spikes];
            float[] bases = { 0, Mathf.PI, Mathf.PI / 2, -Mathf.PI / 2 };
            for (int i = 0; i < spikes.Length; i++)
            {
                float baseA = sp.radial ? i / (float)spikes.Length * Mathf.PI * 2 : bases[i % 4] + (i >= 4 ? (R() - .5f) * 1.6f : 0);
                spikes[i] = new Spike { a = aim + baseA + (R() - .5f) * .35f, len = sp.R * (i < 2 && !sp.radial ? .9f + R() * .25f : .45f + R() * .5f), w = (i < 2 ? 4.5f : 3) * (basic ? 1 : 1.35f) };
            }
            var drops = new Drop[6]; for (int i = 0; i < 6; i++) drops[i] = new Drop { a = aim + (R() - .5f) * 1.9f, d = 10 + R() * 18, r = 1.6f + R() * 2.4f };
            var tw = new (int x, int y, bool[] on)[3];
            for (int i = 0; i < 3; i++) { float a = R() * Mathf.PI * 2, d = sp.R * (.55f + R() * .5f); tw[i] = (Mathf.RoundToInt(c + Mathf.Cos(a) * d), Mathf.RoundToInt(c + Mathf.Sin(a) * d), new[] { false, R() < .7f, R() < .7f, R() < .7f }); }
            float[] tkK = { 1, .62f, .32f, .18f }, shiftK = { 0, 5, 9, 12 }, wideK = { 1, 1.08f, 1.12f, 1.14f }, dens = { 1, 1, .72f, .42f };
            for (int f = 0; f < 4; f++)
            {
                var M = Scratch(ref scratchM, S * S); var K = Scratch(ref scratchK, S * S);
                // ① 초승달 + 가시
                if (slash)
                {
                    float tk = sp.cresT * tkK[f], sh = shiftK[f], ex = c + dx * sh, ey = c + dy * sh;
                    float ox = ex + nx * sp.cresR, oy = ey + ny * sp.cresR;
                    Fill(M, S, ex, ey, Mathf.Max(sp.cresL * wideK[f], sp.cresR * .5f), (x, y) => InEllipse(x, y, ex, ey, sp.cresL * wideK[f], sp.cresR * .5f, aim) && Sq(x - ox, y - oy) <= sp.cresR * sp.cresR && Sq(x - ox - nx * tk, y - oy - ny * tk) > sp.cresR * sp.cresR);
                    if (!basic && sp != Kill && (f == 1 || f == 2))
                    {
                        float t2 = sp.cresT * (f == 1 ? .55f : .3f), ox2 = c + nx * sp.cresR * .75f, oy2 = c + ny * sp.cresR * .75f;
                        Fill(M, S, c, c, Mathf.Max(sp.cresL * .8f, sp.cresR * .5f), (x, y) => InEllipse(x, y, c, c, sp.cresL * .8f, sp.cresR * .5f, aim) && Sq(x - ox2, y - oy2) <= sp.cresR * sp.cresR && Sq(x - ox2 - nx * t2, y - oy2 - ny * t2) > sp.cresR * sp.cresR);
                    }
                }
                foreach (var s in spikes)
                {
                    if (f == 0) Tri(M, S, c, s.a, -s.w * .6f, s.len, s.w);
                    if (f == 1) Tri(M, S, c, s.a, s.len * .2f, s.len * 1.15f, s.w * .55f);
                    if (f == 2) Tri(M, S, c, s.a, s.len * .62f, s.len * 1.25f, s.w * .4f);
                }
                if (f == 0) Fill(M, S, c, c, sp.R * .2f, (x, y) => Sq(x - c, y - c) <= Sq(sp.R * .2f, 0));
                if (f == 1) { float r = sp.R * .16f; for (int i = 0; i < S * S; i++) if (Sq(i % S + .5f - c, i / S + .5f - c) <= r * r) M[i] = false; }
                // ② 먹물 튀김
                if (f <= 2)
                {
                    float k = sp.splat, br = new[] { 8f, 6, 3 }[f] * k, push = new[] { 4f, 7, 9 }[f] * k;
                    Fill(K, S, c + dx * push, c + dy * push, br * 1.25f, (x, y) => InEllipse(x, y, c + dx * push, c + dy * push, br * 1.25f, br * .85f, aim));
                    foreach (var d in drops)
                    {
                        float dist = d.d * k * new[] { 1f, 1.35f, 1.6f }[f], px = c + Mathf.Cos(d.a) * dist, py = c + Mathf.Sin(d.a) * dist, r = d.r * k * new[] { 1f, .85f, .6f }[f];
                        Fill(K, S, px, py, r, (x, y) => Sq(x - px, y - py) <= r * r);
                        if (f < 2)
                        {
                            var tri = new[] { new Vector2(px - Mathf.Sin(d.a) * r * .8f, py + Mathf.Cos(d.a) * r * .8f), new Vector2(c + Mathf.Cos(d.a) * dist * .45f, c + Mathf.Sin(d.a) * dist * .45f), new Vector2(px + Mathf.Sin(d.a) * r * .8f, py - Mathf.Cos(d.a) * r * .8f) };
                            FillPolygon(K, S, tri);
                        }
                    }
                }
                // ③ 4톤 + 디더
                var o = frames[f]; Array.Clear(o, 0, S * S);
                bool At(bool[] m, int i, int j) => i >= 0 && j >= 0 && i < S && j < S && m[j * S + i];
                // Manhattan distance to the nearest pixel outside the shape (canvas edge counts as outside):
                // "every pixel within r steps is inside" is depth > r, without scanning the r-diamond per pixel.
                var D = Depth(M, S, Scratch(ref scratchD, S * S));
                bool Inside(int i, int j, int r) => D[j * S + i] > r;
                for (int j = 0; j < S; j++) for (int i = 0; i < S; i++)
                {
                    if (!Dither(i, j, dens[f])) continue;
                    Color32 col = default; bool set = true;
                    if (At(M, i, j)) col = Inside(i, j, 4) ? core : (!At(M, i + 1, j + 1) || !At(M, i, j + 1) || (!Inside(i, j, 2) && !At(M, i + 2, j + 2))) ? shade : body;
                    else if (At(M, i - 1, j) || At(M, i + 1, j) || At(M, i, j - 1) || At(M, i, j + 1)) col = Ink;
                    else if (At(K, i, j)) col = !At(K, i - 1, j - 1) && f < 2 ? Gloss : Ink;
                    else set = false;
                    if (set) o[j * S + i] = col;
                }
                foreach (var t in tw) if (t.on[f])
                {
                    int L = f == 1 ? 3 : 2;
                    void Box(int x, int y, int w, int h, Color32 col) { for (int b = 0; b < h; b++) for (int a = 0; a < w; a++) if (x + a >= 0 && y + b >= 0 && x + a < S && y + b < S) o[(y + b) * S + x + a] = col; }
                    Box(t.x - L - 1, t.y - 1, L * 2 + 3, 3, Ink); Box(t.x - 1, t.y - L - 1, 3, L * 2 + 3, Ink);
                    Box(t.x - L, t.y, L * 2 + 1, 1, core); Box(t.x, t.y - L, 1, L * 2 + 1, core);
                }
                frames[f] = o;
            }
            return frames;
        }
        static int[] Depth(bool[] m, int S, int[] d)
        {
            for (int j = 0; j < S; j++) for (int i = 0; i < S; i++)
            {
                int k = j * S + i; if (!m[k]) continue;
                d[k] = Mathf.Min(i > 0 ? d[k - 1] : 0, j > 0 ? d[k - S] : 0) + 1;
            }
            for (int j = S - 1; j >= 0; j--) for (int i = S - 1; i >= 0; i--)
            {
                int k = j * S + i; if (!m[k]) continue;
                d[k] = Mathf.Min(d[k], Mathf.Min(i < S - 1 ? d[k + 1] : 0, j < S - 1 ? d[k + S] : 0) + 1);
            }
            return d;
        }
        static float Sq(float x, float y) => x * x + y * y;
        // Each shape only scans its own square (centre ± reach): scanning the whole S×S canvas per shape
        // cost tens of ms per heavy hit.
        static void Fill(bool[] m, int S, float cx, float cy, float reach, Func<float, float, bool> inside)
            => Fill(m, S, cx - reach, cy - reach, cx + reach, cy + reach, inside);
        static void Fill(bool[] m, int S, float x0, float y0, float x1, float y1, Func<float, float, bool> inside)
        {
            int i0 = Mathf.Max(0, Mathf.FloorToInt(x0) - 1), j0 = Mathf.Max(0, Mathf.FloorToInt(y0) - 1), i1 = Mathf.Min(S - 1, Mathf.CeilToInt(x1) + 1), j1 = Mathf.Min(S - 1, Mathf.CeilToInt(y1) + 1);
            for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) if (inside(i + .5f, j + .5f)) m[j * S + i] = true;
        }
        static void FillPolygon(bool[] m, int S, Vector2[] p)
        {
            float x0 = p.Min(v => v.x), y0 = p.Min(v => v.y), x1 = p.Max(v => v.x), y1 = p.Max(v => v.y);
            Fill(m, S, x0, y0, x1, y1, (x, y) => InPolygon(p, x, y));
        }
        static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry, float ang)
        { float c = Mathf.Cos(ang), s = Mathf.Sin(ang), u = (x - cx) * c + (y - cy) * s, v = -(x - cx) * s + (y - cy) * c; return u * u / (rx * rx) + v * v / (ry * ry) <= 1; }
        static void Tri(bool[] m, int S, float c, float a, float r0, float r1, float w0)
        {
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var tri = new[] { new Vector2(c + ca * r0 - sa * w0, c + sa * r0 + ca * w0), new Vector2(c + ca * r1, c + sa * r1), new Vector2(c + ca * r0 + sa * w0, c + sa * r0 - ca * w0) };
            FillPolygon(m, S, tri);
        }
        static bool InPolygon(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
            return inside;
        }

        // ── 휘두름 잔상: 칼끝 길을 따라 꼬리는 길고 가늘게, 앞쪽 ≈80% 에서 가장 두껍게, 칼끝에서 날카롭게 ──
        sealed class Pt { public Vector2 tip, hilt, n; public float rem, s, u, th; }
        // Reused every frame: a swing redraws each LateUpdate, so per-call arrays would churn the GC.
        readonly List<Pt> pts = new List<Pt>(), ptPool = new List<Pt>();
        float[] bufA = new float[0], bufC = new float[0], bufB = new float[0], bufE = new float[0]; bool[] bufS = new bool[0];
        readonly Vector2[] quad = new Vector2[4];
        static float[] Lease(ref float[] buffer, int size) { if (buffer.Length < size) buffer = new float[size]; else Array.Clear(buffer, 0, size); return buffer; }
        void DrawSmear(List<TrailSample> hist, float now, float tail, float max, float rim)
        {
            pts.Clear();
            foreach (var q in hist)
            {
                float rem = 1 - (now - q.at) / tail; if (rem <= 0) continue;
                if (ptPool.Count <= pts.Count) ptPool.Add(new Pt());
                var pt = ptPool[pts.Count]; pt.tip = ToCanvas(q.tip); pt.hilt = ToCanvas(q.hilt); pt.rem = rem; pt.s = 0; pts.Add(pt);
            }
            if (pts.Count < 2) return;
            float total = 0; for (int i = 1; i < pts.Count; i++) { total += Vector2.Distance(pts[i].tip, pts[i - 1].tip); pts[i].s = total; }
            foreach (var q in pts)
            {
                q.u = total > 0 ? q.s / total : 0;
                float prof = Mathf.Pow(q.u, 2.2f) * Mathf.Pow(1 - q.u, .45f) / .3f;
                q.th = max * Mathf.Min(1, prof) * (.6f + .4f * q.rem); q.n = (q.hilt - q.tip).normalized;
            }
            Vector2 Off(Pt q, float d) => q.tip + q.n * d;
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            void Grow(Vector2 v) { x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y); }
            foreach (var q in pts) { Grow(q.tip); Grow(Off(q, q.th + 6)); Grow(Off(q, -3)); }
            int bx = Mathf.FloorToInt(x0) - 3, by = Mathf.FloorToInt(y0) - 3, bw = Mathf.CeilToInt(x1) + 3 - bx, bh = Mathf.CeilToInt(y1) + 3 - by;
            if (bw <= 0 || bh <= 0 || bw * bh > 400000) return;
            // 겹: A 띠 전체(값=최근도) / C 바깥쪽 72% / B 칼끝 가장자리(밝은 심) / S 안쪽 결 선 / E 바깥 잔상 선
            int size = bw * bh;
            var A = Lease(ref bufA, size); var C = Lease(ref bufC, size); var B = Lease(ref bufB, size); var E = Lease(ref bufE, size);
            if (bufS.Length < size) bufS = new bool[size]; else Array.Clear(bufS, 0, size); var Sl = bufS;
            void Band(float[] layer, bool[] flag, Pt p, Pt q, float d0p, float d1p, float d0q, float d1q, float v)
            {
                var poly = quad; poly[0] = Off(p, d0p); poly[1] = Off(p, d1p); poly[2] = Off(q, d1q); poly[3] = Off(q, d0q);
                int ax = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(poly[0].x, poly[1].x), Mathf.Min(poly[2].x, poly[3].x))) - bx), ay = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(poly[0].y, poly[1].y), Mathf.Min(poly[2].y, poly[3].y))) - by);
                int ex = Mathf.Min(bw - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(poly[0].x, poly[1].x), Mathf.Max(poly[2].x, poly[3].x))) - bx), ey = Mathf.Min(bh - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(poly[0].y, poly[1].y), Mathf.Max(poly[2].y, poly[3].y))) - by);
                for (int j = ay; j <= ey; j++) for (int i = ax; i <= ex; i++)
                    if (InPolygon(poly, bx + i + .5f, by + j + .5f)) { int k = j * bw + i; if (flag != null) flag[k] = true; else if (v > layer[k]) layer[k] = v; }
            }
            for (int i = 1; i < pts.Count; i++)
            {
                Pt p = pts[i - 1], q = pts[i]; float v = (p.rem + q.rem) / 2;
                Band(A, null, p, q, 0, p.th, 0, q.th, v);
                Band(C, null, p, q, 0, p.th * .72f, 0, q.th * .72f, v);
                if (v > .45f) Band(B, null, p, q, 0, Mathf.Min(rim, p.th * .4f), 0, Mathf.Min(rim, q.th * .4f), v);
                if (p.th > 7 && q.th > 7) Band(null, Sl, p, q, p.th * .45f, p.th * .45f + 1.1f, q.th * .45f, q.th * .45f + 1.1f, v);
                if (p.u > .22f && q.u < .94f) Band(E, null, p, q, p.th + 3, p.th + 4 + p.th / max, q.th + 3, q.th + 4 + q.th / max, v * .9f);
            }
            float Val(float[] m, int i, int j) => i >= 0 && j >= 0 && i < bw && j < bh ? m[j * bw + i] : 0;
            bool Keep(float[] m, int i, int j) { float r = Val(m, i, j); return r > 0 && (r >= .5f || Dither(bx + i, by + j, r / .5f)); }
            for (int j = 0; j < bh; j++) for (int i = 0; i < bw; i++)
            {
                Color32 col = default; bool set = true;
                if (Keep(A, i, j))
                {
                    if (Val(B, i, j) > .55f) col = core;                                        // 칼끝 가장자리
                    else if (Sl[j * bw + i]) col = deep;                                        // 안쪽 결 선
                    else if (Val(C, i, j) > 0) col = body;                                      // 몸통
                    else if (Dither(bx + i, by + j, .5f)) col = shade;                          // 안쪽 가장자리: 점이 풀어지는 결
                    else set = false;
                }
                else if (Keep(E, i, j)) col = shade;                                            // 바깥 잔상 선
                else if (Val(A, i, j) <= 0 && (Edge(i + 1, j) || Edge(i - 1, j) || Edge(i, j + 1) || Edge(i, j - 1))) col = Ink;   // 바깥쪽 먹 테두리
                else set = false;
                if (set) Plot(bx + i, by + j, col);
            }
            bool Edge(int i, int j) => Keep(C, i, j) && Val(C, i, j) > .4f;
        }
    }
}
