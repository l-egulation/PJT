using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Plays the user-approved launcher effect frames (Resources/LauncherSkillFx, PPU32, pivots in the .meta).
    // Presentation only: damage, hit shapes, flight and recoil stay with the executions.
    //   ball_*      basic ball shot: muzzle pop, bullet, enemy hit
    //   charge_*    charge shot: orb by step, inward sparks, step-up sparkle, shot by step, big pop, burst by step
    //   laser_*     laser skill: thinner beam, pop, hold, growing head, per-enemy hit, wall hit/hold
    //   shotgun_*   shotgun skill: pellet, pop, recoil dust and wind, pellet hit
    //   grenade_*   grenade skill: shell, smoke trail, pop, fragment, main/fragment blasts in the air or on the ground
    // A clip that is missing (assets not deployed) leaves the previous placeholder picture in place.
    public static class LauncherSkillFx
    {
        [Serializable] internal sealed class ClipData { public string name; public bool loop; public int loopFrom = -1; public float[] seconds; public string[] frames; }
        [Serializable] sealed class Catalog { public string format; public ClipData[] clips; }
        internal sealed class Clip
        {
            public ClipData data; public Sprite[] sprites; public float Duration, LoopStart;
            // Frame at an age, or -1 once a one-shot clip has ended.
            public int FrameAt(float age)
            {
                if (age < 0) age = 0;
                if (age >= Duration)
                {
                    if (!data.loop) return -1;
                    float span = Duration - LoopStart;
                    age = span > 0 ? LoopStart + (age - LoopStart) % span : 0;
                }
                for (int i = 0; i < data.seconds.Length; i++) { if (age < data.seconds[i]) return i; age -= data.seconds[i]; }
                return data.seconds.Length - 1;
            }
            public float StartOf(int frame) { float t = 0; for (int i = 0; i < frame && i < data.seconds.Length; i++) t += data.seconds[i]; return t; }
        }

        const string CatalogPath = "LauncherSkillFx/clips";
        internal const int BackOrder = 24, Order = 26, FrontOrder = 31;
        static Dictionary<string, Clip> clips;
        static readonly List<LauncherFxFlipbook> live = new List<LauncherFxFlipbook>();

        static Dictionary<string, Clip> Load()
        {
            if (clips != null) return clips;
            clips = new Dictionary<string, Clip>();
            var text = Resources.Load<TextAsset>(CatalogPath);
            if (!text) return clips;
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            if (catalog?.clips == null || catalog.format != "deviant-launcher-skill-fx-v1") return clips;
            foreach (var data in catalog.clips)
            {
                if (data?.frames == null || data.seconds == null || data.frames.Length == 0 || data.frames.Length != data.seconds.Length) continue;
                var sprites = new Sprite[data.frames.Length];
                bool complete = true;
                for (int i = 0; i < sprites.Length; i++) complete &= (sprites[i] = Resources.Load<Sprite>(data.frames[i])) != null;
                if (!complete) continue;
                var clip = new Clip { data = data, sprites = sprites };
                foreach (float s in data.seconds) clip.Duration += s;
                clip.LoopStart = data.loopFrom > 0 ? clip.StartOf(data.loopFrom) : 0;
                clips[data.name] = clip;
            }
            return clips;
        }

        internal static Clip Find(string name) => name != null && Load().TryGetValue(name, out var clip) ? clip : null;
        internal static bool Enabled(CombatArena arena) => arena && arena.loadCharacterArt && Load().Count > 0;
        internal static bool Has(CombatArena arena, string name) => Enabled(arena) && Find(name) != null;

        // at: world point, or local point when parented. angle: degrees, applied after the facing mirror.
        internal static LauncherFxFlipbook Play(CombatArena arena, string name, Vector2 at, int facing = 1, int order = Order,
            Transform parent = null, float angle = 0, float delay = 0, float startAge = 0, string objectName = null)
        {
            if (!Enabled(arena)) return null;
            var clip = Find(name);
            if (clip == null) return null;
            var node = new GameObject(objectName ?? "LauncherSkillFx " + name);
            if (parent) node.transform.SetParent(parent, false);
            if (parent) node.transform.localPosition = at; else node.transform.position = new Vector3(at.x, at.y, 0);
            node.transform.localScale = new Vector3(facing < 0 ? -1 : 1, 1, 1);
            if (angle != 0) node.transform.localRotation = Quaternion.Euler(0, 0, angle);
            var renderer = node.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            if (arena.spriteMaterial) renderer.sharedMaterial = arena.spriteMaterial;
            var book = node.AddComponent<LauncherFxFlipbook>();
            book.Begin(arena, clip, renderer, arena.Now + delay - startAge);
            live.RemoveAll(value => !value);
            live.Add(book);
            return book;
        }

        internal static int Count(CombatArena arena)
        {
            int n = 0;
            foreach (var book in live) if (book && book.Arena == arena) n++;
            return n;
        }

        internal static void Clear(CombatArena arena)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (!live[i]) live.RemoveAt(i);
                else if (live[i].Arena == arena) { UnityEngine.Object.Destroy(live[i].gameObject); live.RemoveAt(i); }
            foreach (var shards in UnityEngine.Object.FindObjectsByType<LauncherShards>(FindObjectsSortMode.None))
                if (shards && shards.Arena == arena) UnityEngine.Object.Destroy(shards.gameObject);
            foreach (var trail in UnityEngine.Object.FindObjectsByType<LauncherSmokeTrail>(FindObjectsSortMode.None))
                if (trail && trail.Arena == arena) UnityEngine.Object.Destroy(trail);
        }

        // Charge shot step from the released projectile radius (0.15 / 0.225 / 0.3 of ChargeRadius 0.3).
        public static int ChargeStep(float radius, float chargeRadius)
        {
            float ratio = chargeRadius > 0 ? radius / chargeRadius : 0;
            return ratio < .62f ? 1 : ratio < .87f ? 2 : 3;
        }
    }

    // One clip on the arena clock, so it freezes with hit-stop and pauses with the game.
    internal sealed class LauncherFxFlipbook : MonoBehaviour
    {
        CombatArena arena;
        LauncherSkillFx.Clip clip;
        SpriteRenderer view;
        float started;
        internal CombatArena Arena => arena;
        internal SpriteRenderer View => view;
        // Gate drawn by the owner (a bullet is hidden until it leaves the barrel). The clock keeps running.
        internal bool Visible = true;

        internal void Begin(CombatArena value, LauncherSkillFx.Clip source, SpriteRenderer renderer, float start)
        {
            arena = value; clip = source; view = renderer; started = start;
            Sample();
        }

        void Sample()
        {
            if (!arena || clip == null || !view) return;
            float age = arena.Now - started;
            if (age < 0) { view.enabled = false; return; }
            int index = clip.FrameAt(age);
            if (index < 0) { Destroy(gameObject); return; }
            view.sprite = clip.sprites[index];
            view.enabled = Visible;
        }

        void LateUpdate() => Sample();
    }

    // A projectile's flipbook body: hidden until it has flown past the drawn muzzle, then the hit picture on removal.
    internal sealed class LauncherProjectileVisual : MonoBehaviour
    {
        internal LauncherFxFlipbook Body;
        internal Vector2 Spawn;
        internal float Gap;          // world distance from spawn to the drawn muzzle tip along the flight
        internal Vector2 DrawOffset; // body drawn on the barrel axis, the hit circle unchanged
        internal string HitClip;     // enemy hit
        internal bool TerrainTail;   // terrain hit plays the hit's last frames only
        internal int Step;           // charge shot step (burst size)

        // The body has no parent (the shot's scale is its hit radius), so it is carried along here every frame.
        // Without this it stayed where the shot was created — inside the player before the muzzle sweep — and the
        // flying bullet was never visible (reported 2026-11-08).
        void LateUpdate()
        {
            if (!Body) return;
            Vector3 at = transform.position;
            Body.transform.position = new Vector3(at.x + DrawOffset.x, at.y + DrawOffset.y, Body.transform.position.z);
            Body.Visible = Vector2.Distance(Spawn, (Vector2)at) >= Gap;
        }
    }

    // Smoke puffs left in the world behind a grenade shell or fragment.
    internal sealed class LauncherSmokeTrail : MonoBehaviour
    {
        CombatArena arena; float every, next; int skip;
        internal CombatArena Arena => arena;
        internal void Begin(CombatArena value, float interval, float firstAfter)
        { arena = value; every = interval; next = value.Now + firstAfter; skip = 0; }
        void LateUpdate()
        {
            if (!arena) return;
            if (next - arena.Now > every * 4) next = arena.Now + every; // the arena clock was reset
            while (arena.Now >= next)
            {
                next += every;
                if (skip++ > 8 && arena.Now - next > every * 4) { next = arena.Now + every; break; }
                LauncherSkillFx.Play(arena, "grenade_smoke", transform.position, 1, LauncherSkillFx.BackOrder);
            }
        }
    }

    // Laser vanish: small pieces scatter from the muzzle toward the tip while the beam thins out.
    internal sealed class LauncherShards : MonoBehaviour
    {
        struct Piece { public Transform node; public SpriteRenderer view; public float start, life, dy; public float x; }
        static readonly Color[] Colors = { Color.white, new Color32(73, 191, 255, 255), new Color32(0, 135, 255, 255) };
        CombatArena arena; float started, y; Piece[] pieces;
        internal CombatArena Arena => arena;
        internal void Begin(CombatArena value, Vector2 muzzle, float length, int direction, Sprite square)
        {
            arena = value; started = value.Now; y = muzzle.y;
            var random = new System.Random(3);
            int count = Mathf.Clamp(Mathf.FloorToInt(length / .3f), 1, 80);
            pieces = new Piece[count];
            for (int i = 0; i < count; i++)
            {
                var node = new GameObject("LaserShard").transform; node.SetParent(transform, false);
                var view = node.gameObject.AddComponent<SpriteRenderer>(); view.sprite = square; view.sortingOrder = 26;
                view.color = Colors[random.Next(Colors.Length)];
                if (value.spriteMaterial) view.sharedMaterial = value.spriteMaterial;
                float along = (i + .5f) * .3f + (float)(random.NextDouble() * .2 - .1);
                pieces[i] = new Piece { node = node, view = view, x = muzzle.x + along * direction,
                    start = .06f * i / count + (float)random.NextDouble() * .02f, life = .08f + (float)random.NextDouble() * .08f,
                    dy = (float)(random.NextDouble() * 3.2 - 1.6) };
            }
            LateUpdate();
        }
        void LateUpdate()
        {
            if (!arena || arena.Now < started) { Destroy(gameObject); return; }
            float age = arena.Now - started; bool any = false;
            foreach (var p in pieces)
            {
                float s = age - p.start;
                bool on = s >= 0 && s < p.life; p.view.enabled = on;
                if (s < p.life) any = true;
                if (!on) continue;
                float k = s / p.life;
                p.node.position = new Vector3(p.x, y + p.dy * .12f * k, 0);
                float w = Mathf.Max(1, Mathf.Round(3 * (1 - k) * 2)) * 2 / 32f;
                p.node.localScale = new Vector3(w, 3 / 32f, 1);
            }
            if (!any) Destroy(gameObject);
        }
    }
}
