using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Plays the approved bomb skill effect frames (Resources/BombSkillFx, pixelfx bakes at PPU32).
    // Presentation only: blast radius, damage, fuse and missile flight stay with the executions.
    //   basic                 bomb_basic_*        round puffy blast at the bomb
    //   triple_0..2           bomb_basic_special  one colour and shape per thrown bomb (hit index)
    //   range                 bomb_range_special  ground blast under the bomb
    //   timed_fuse + timed    bomb_followup_special  heat-up while the fuse burns, then the mushroom blast on the ground
    //   missile_*             bomb_move_special   launch backblast, comet body, fire trail, forward blast
    //   timed_clock           bomb_followup_special  clock ring round the landed timed bomb; the hand goes once round over the fuse (2026-11-10)
    // A clip that is missing (assets not deployed) falls back to the arena's placeholder burst.
    public static class BombSkillFx
    {
        [Serializable] internal sealed class ClipData { public string name, anchor; public bool facing, loop; public float segment, fps; public string[] frames; }
        [Serializable] sealed class Catalog { public string format; public ClipData[] clips; }
        internal sealed class Clip
        {
            public ClipData data; public Sprite[] sprites; public float Duration => sprites.Length / data.fps;
            bool? complete;
            // Frames are read the first time the clip is found (or its weapon is warmed), not with the catalog.
            internal bool Resolve()
            {
                if (complete.HasValue) return complete.Value;
                var frames = new Sprite[data.frames.Length]; bool ok = true;
                for (int i = 0; i < frames.Length; i++) ok &= (frames[i] = Resources.Load<Sprite>(data.frames[i])) != null;
                sprites = frames; complete = ok; return ok;
            }
        }

        const string CatalogPath = "BombSkillFx/clips";
        internal const int BlastOrder = 30, FuseOrder = 27, TrailOrder = 19, LaunchOrder = 29, BodyOrder = 30;
        static Dictionary<string, Clip> clips;
        static readonly List<BombFxFlipbook> live = new List<BombFxFlipbook>();

        static Dictionary<string, Clip> Load() => clips ??= LoadCatalog(CatalogPath, "deviant-bomb-skill-fx-v1");

        // Same clip catalog shape for every weapon's baked skill effects; a clip with a missing frame is left out.
        internal static Dictionary<string, Clip> LoadCatalog(string path, string format)
        {
            var result = new Dictionary<string, Clip>();
            var text = Resources.Load<TextAsset>(path);
            if (!text) return result;
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            if (catalog?.clips == null || catalog.format != format) return result;
            foreach (var data in catalog.clips)
            {
                if (data == null || data.frames == null || data.frames.Length == 0 || data.fps <= 0) continue;
                // A clip with a missing frame is still left out: Find resolves it and rejects it then.
                result[data.name] = new Clip { data = data };
            }
            return result;
        }

        internal static Clip Find(string name) => Resolved(Load(), name);
        internal static Clip Resolved(Dictionary<string, Clip> catalog, string name) => name != null && catalog.TryGetValue(name, out var clip) && clip.Resolve() ? clip : null;
        internal static IEnumerable<bool> WarmCatalog(Dictionary<string, Clip> catalog) { foreach (var clip in new List<Clip>(catalog.Values)) { clip.Resolve(); yield return true; } }
        internal static IEnumerable<bool> WarmSteps() => WarmCatalog(Load());

        // Which blast a bomb plays, from the hit origin the bomb carries.
        public static string BlastClip(CombatHitOrigin origin)
        {
            string id = origin.ActionId;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("bomb_", StringComparison.Ordinal)) return null;
            switch (id)
            {
                case "bomb_basic_special": return "triple_" + Mathf.Clamp(origin.HitIndex, 0, 2);
                case "bomb_range_special": return "range";
                case "bomb_followup_special": return "timed";
                case "bomb_move_special": return "missile_hit";
            }
            return origin.IsBasicAction || id.StartsWith("bomb_basic_", StringComparison.Ordinal) ? "basic" : null;
        }

        static bool Enabled(CombatArena arena) => arena && arena.loadCharacterArt;

        // Replaces the placeholder burst. Returns false when there is nothing approved to play.
        // radius = the blast's hit radius; the basic and triple blasts are drawn to it (SkillFxHitScale).
        internal static bool TryBlast(CombatArena arena, Vector2 at, CombatHitOrigin origin, int facing, float radius = 0)
        {
            if (!Enabled(arena)) return false;
            var clip = Find(BlastClip(origin));
            if (clip == null) return false;
            Play(arena, clip, clip.data.anchor == "ground" ? Ground(at) : at, facing, BlastOrder, null, SkillFxHitScale.BombBlast(clip.data.name, radius));
            return true;
        }

        // scale = uniform display scale round the pivot (1 = the baked PPU32 size).
        internal static BombFxFlipbook Play(CombatArena arena, Clip clip, Vector2 at, int facing, int order, Transform parent, float scale = 1)
        {
            var node = new GameObject("BombSkillFx " + clip.data.name);
            if (parent) node.transform.SetParent(parent, false);
            node.transform.position = new Vector3(at.x, at.y, 0);
            float flip = clip.data.facing && facing < 0 ? -1 : 1;
            if (flip < 0 || scale != 1) node.transform.localScale = new Vector3(flip * scale, scale, 1);
            var renderer = node.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            if (arena.spriteMaterial) renderer.sharedMaterial = arena.spriteMaterial;
            var book = node.AddComponent<BombFxFlipbook>();
            book.Begin(arena, clip, renderer);
            live.RemoveAll(value => !value);
            live.Add(book);
            return book;
        }

        internal static BombFxFlipbook Play(CombatArena arena, string name, Vector2 at, int facing, int order, Transform parent = null, float scale = 1)
        {
            if (!Enabled(arena)) return null;
            var clip = Find(name);
            return clip == null ? null : Play(arena, clip, at, facing, order, parent, scale);
        }

        // The floor under a point: ground blasts and the fuse sit on it even when the bomb went off on a body.
        internal static Vector2 Ground(Vector2 at, float maxDrop = 12)
        {
            float best = float.PositiveInfinity;
            foreach (var hit in Physics2D.RaycastAll(at + Vector2.up * .05f, Vector2.down, maxDrop))
            {
                if (!hit.collider || hit.collider.isTrigger || !hit.collider.GetComponent<ArenaSurface>()) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return float.IsPositiveInfinity(best) ? at : new Vector2(at.x, at.y + .05f - best);
        }

        // Read by tests: the clock frame at an age after landing, the clock's frame count, and a tossed triple-kick bomb's place.
        public static int TimedClockFrame(float age, float fuse) { var clip = Find("timed_clock"); return clip == null ? -1 : BombFuseVisual.ClockFrame(age, fuse, clip.sprites.Length); }
        public static int TimedClockFrames => Find("timed_clock")?.sprites.Length ?? 0;
        public static bool TripleKickBombAt(int index, float age, out Vector2 at) => BombTripleKickBombs.Pose(index, age, out at);

        // Timed bomb shake in game pixels (PPU32) at fuse progress k in 0..1: wider and faster as it runs out.
        public static float ShakePixels(float k, float time)
            => Mathf.Round(Mathf.Sin(time * (30 + 90 * k)) * (.5f + 2 * k * k));

        internal static void Clear(CombatArena arena)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (!live[i]) live.RemoveAt(i);
                else if (live[i].Arena == arena) { UnityEngine.Object.Destroy(live[i].gameObject); live.RemoveAt(i); }
        }
    }

    // One baked clip played on the arena clock, so it freezes with hit-stop and pauses with the game.
    internal sealed class BombFxFlipbook : MonoBehaviour
    {
        CombatArena arena;
        BombSkillFx.Clip clip;
        SpriteRenderer view;
        float started;
        internal CombatArena Arena => arena;
        internal SpriteRenderer View => view;

        internal void Begin(CombatArena value, BombSkillFx.Clip source, SpriteRenderer renderer)
        {
            arena = value; clip = source; view = renderer; started = value.Now;
            Sample();
        }

        // Starts the clip as if it had begun `seconds` earlier (effects drawn from a cast start but shown from activation).
        internal void Backdate(float seconds) { started -= Mathf.Max(0, seconds); Sample(); }

        internal int FrameAt(float age)
        {
            int index = Mathf.FloorToInt(Mathf.Max(0, age) * clip.data.fps + .0001f);
            return clip.data.loop ? index % clip.sprites.Length : index;
        }

        void Sample()
        {
            if (!arena || clip == null || !view) return;
            int index = FrameAt(arena.Now - started);
            if (index >= clip.sprites.Length) { Destroy(gameObject); return; }
            view.sprite = clip.sprites[index];
        }

        void LateUpdate() => Sample();
    }

    // The timed bomb's heat-up once it has landed: the baked fuse frames on the floor under the bomb
    // (sparks, a red glow that pulses faster, a white swell at the end) and a shake that grows with the fuse.
    internal sealed class BombFuseVisual : MonoBehaviour
    {
        BombProjectile bomb;
        BombSkillFx.Clip clip;
        SpriteRenderer view;
        Transform art;
        Vector3 artHome;

        internal static void TryAttach(BombProjectile bomb)
        {
            if (!bomb || !bomb.arena || !bomb.arena.loadCharacterArt || bomb.Definition.FuseSeconds <= 0) return;
            if (BombSkillFx.BlastClip(bomb.HitOrigin) != "timed") return;
            var clip = BombSkillFx.Find("timed_fuse");
            if (clip == null) return;
            var node = new GameObject("BombSkillFx timed_fuse");
            node.transform.SetParent(bomb.transform, false);
            var visual = node.AddComponent<BombFuseVisual>();
            visual.bomb = bomb; visual.clip = clip;
            visual.view = node.AddComponent<SpriteRenderer>();
            visual.view.sortingOrder = BombSkillFx.FuseOrder; visual.view.enabled = false;
            if (bomb.arena.spriteMaterial) visual.view.sharedMaterial = bomb.arena.spriteMaterial;
            visual.clock = BombSkillFx.Find("timed_clock");
            if (visual.clock != null)
            {
                var ring = new GameObject("BombSkillFx timed_clock");
                ring.transform.SetParent(bomb.transform, false);
                visual.clockView = ring.AddComponent<SpriteRenderer>();
                visual.clockView.sortingOrder = ClockOrder; visual.clockView.enabled = false;
                if (bomb.arena.spriteMaterial) visual.clockView.sharedMaterial = bomb.arena.spriteMaterial;
            }
        }

        // Clock ring (2026-11-10): centred .72 above the floor so its rim rests on the ground round the bomb, behind the bomb art (26).
        // Frame 00 pops it in for the first .06 s after landing, then 01..12 step the hand 30 degrees every fuse/12: once round, then the blast.
        internal const float ClockCenterAboveFloor = .72f, ClockPopSeconds = .06f;
        const int ClockOrder = 25;
        BombSkillFx.Clip clock;
        SpriteRenderer clockView;
        internal static int ClockFrame(float age, float fuse, int frames)
        {
            if (frames <= 1 || age < ClockPopSeconds) return 0;
            int steps = frames - 1;
            return 1 + Mathf.Clamp(Mathf.FloorToInt(age / Mathf.Max(.0001f, fuse / steps) + .0001f), 0, steps - 1);
        }


        void LateUpdate()
        {
            if (!bomb) return;
            if (!art) { art = bomb.transform.Find("ApprovedThrown_bomb"); if (art) artHome = art.localPosition; }
            if (!bomb.TimedLanded) { view.enabled = false; if (clockView) clockView.enabled = false; return; }
            float fuse = bomb.Definition.FuseSeconds, k = Mathf.Clamp01(1 - bomb.TimedFuseRemaining / fuse);
            int index = Mathf.Min(clip.sprites.Length - 1, Mathf.FloorToInt(k * fuse * clip.data.fps + .0001f));
            view.enabled = true; view.sprite = clip.sprites[index];
            Vector2 floor = BombSkillFx.Ground(bomb.Position, 2);
            float shake = BombSkillFx.ShakePixels(k, bomb.arena.Now) / 32f;
            transform.position = new Vector3(floor.x + shake, floor.y, 0);
            if (art) art.localPosition = artHome + new Vector3(shake / Mathf.Max(.0001f, Mathf.Abs(bomb.transform.localScale.x)), 0, 0);
            if (clockView)
            {
                clockView.enabled = true;
                clockView.sprite = clock.sprites[ClockFrame(k * fuse, fuse, clock.sprites.Length)];
                var parent = bomb.transform.lossyScale;     // the bomb body is scaled for its collider; keep the ring at PPU32
                clockView.transform.localScale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(parent.x)), 1 / Mathf.Max(.0001f, Mathf.Abs(parent.y)), 1);
                clockView.transform.position = new Vector3(floor.x, floor.y + ClockCenterAboveFloor, 0);
            }
        }
    }

    // Bomb basic special, triple kick (2026-11-10): the three bombs tossed up before the kicks hang in front of the body until
    // each one is kicked. Not in the body art; drawn here on the cast clock. Path per bomb: a curve from the tossing hand over its
    // own apex to the point the game spawns that bomb from, reached exactly at its release. Units from the feet, +x forward.
    internal static class BombTripleKickBombs
    {
        [Serializable] sealed class Path { public string format; public float toss; public float[] from, apexX, apexY, releases; }
        static Path path;
        static Sprite bomb;
        static bool loaded;
        static readonly Dictionary<SpriteRenderer, SpriteRenderer[]> views = new Dictionary<SpriteRenderer, SpriteRenderer[]>();
        static readonly Vector2 Spawn = new Vector2(.5f, 1f);

        static bool Load()
        {
            if (loaded) return path != null && bomb;
            loaded = true;
            var text = Resources.Load<TextAsset>("BombSkillFx/triple_kick");
            if (text) path = JsonUtility.FromJson<Path>(text.text);
            if (path == null || path.format != "deviant-bomb-triple-kick-v1" || path.from == null || path.from.Length < 2 || path.releases == null
                || path.apexX == null || path.apexY == null || path.apexX.Length < path.releases.Length || path.apexY.Length < path.releases.Length) { path = null; return false; }
            var catalog = Resources.Load<TextAsset>("CharacterV2/ApprovedThrownProjectiles");
            var entry = catalog ? Array.Find(JsonUtility.FromJson<ApprovedThrownCatalog>(catalog.text).projectiles, d => d.form == "bomb") : null;
            if (entry != null) { entry.Load(); bomb = entry.At(0, true); }
            return bomb;
        }

        // Position of bomb number index at cast age, or false while it is not in the air.
        internal static bool Pose(int index, float age, out Vector2 at)
        {
            at = default;
            if (!Load() || index < 0 || index >= path.releases.Length) return false;
            float release = path.releases[index];
            if (age < path.toss || age >= release) return false;
            float u = Mathf.Clamp01((age - path.toss) / Mathf.Max(.0001f, release - path.toss));
            var p0 = new Vector2(path.from[0], path.from[1]); var apex = new Vector2(path.apexX[index], path.apexY[index]);
            var c = 2 * apex - (p0 + Spawn) / 2;
            at = (1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * c + u * u * Spawn;
            return true;
        }

        internal static void Draw(SpriteRenderer body, bool on, float age, int facing)
        {
            if (!body) return;
            views.TryGetValue(body, out var own);
            if (!on) { if (own != null) foreach (var v in own) if (v) v.enabled = false; return; }
            if (!Load()) return;
            var root = body.transform.parent ? body.transform.parent : body.transform;
            if (own == null || own.Length != path.releases.Length || Array.Exists(own, v => !v))
            {
                own = new SpriteRenderer[path.releases.Length];
                for (int i = 0; i < own.Length; i++)
                {
                    var node = new GameObject("BombSkillFx triple_kick bomb " + i);
                    node.transform.SetParent(root, false);
                    own[i] = node.AddComponent<SpriteRenderer>();
                    own[i].sprite = bomb; own[i].sharedMaterial = body.sharedMaterial;
                }
                views[body] = own;
            }
            var scale = root.lossyScale; int side = facing < 0 ? -1 : 1;
            for (int i = 0; i < own.Length; i++)
            {
                var v = own[i];
                if (!Pose(i, age, out var at)) { v.enabled = false; continue; }
                v.enabled = true; v.sortingLayerID = body.sortingLayerID; v.sortingOrder = body.sortingOrder + 1;
                v.transform.localScale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1);
                v.transform.position = body.transform.position + new Vector3(at.x * side, at.y, 0);   // the body sprite's pivot is the feet
                v.transform.rotation = Quaternion.Euler(0, 0, -side * 240 * (age - path.toss));
            }
        }
    }
}
