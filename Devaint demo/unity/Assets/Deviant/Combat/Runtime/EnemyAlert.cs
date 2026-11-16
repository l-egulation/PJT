using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Deterministic enemy randomness: every value an enemy draws comes from the stage seed, its species id, its
    // spawn position and a stream number, so the same seed replays the same individuals.
    public static class EnemyRandom
    {
        public const int Variance = 1, Hop = 2, Alert = 3;
        static readonly Dictionary<CombatArena, int> seeds = new Dictionary<CombatArena, int>();
        // Worlds call this with the seed that generated the stage before they spawn enemies. Unset arenas use 0.
        public static void SetSeed(CombatArena arena, int seed)
        {
            if (!arena) return;
            var stale = new List<CombatArena>(); foreach (var k in seeds.Keys) if (!k) stale.Add(k);
            foreach (var k in stale) seeds.Remove(k);
            seeds[arena] = seed;
        }
        public static int SeedOf(CombatArena arena) => arena && seeds.TryGetValue(arena, out int s) ? s : 0;
        public static System.Random For(CombatArena arena, string id, Vector2 spawn, int stream)
        {
            unchecked
            {
                uint h = 2166136261;
                void Mix(int v) { for (int i = 0; i < 4; i++) { h ^= (uint)(v >> (8 * i)) & 0xff; h *= 16777619; } }
                Mix(SeedOf(arena)); Mix(Mathf.RoundToInt(spawn.x * 100)); Mix(Mathf.RoundToInt(spawn.y * 100)); Mix(stream);
                foreach (char c in id ?? "") Mix(c);
                return new System.Random((int)(h & 0x7fffffff));
            }
        }
        public static float Range(System.Random r, float min, float max) => max <= min ? min : min + (float)r.NextDouble() * (max - min);
    }

    // Discovery reaction and the "!" chain alert (data/combat/enemy_pursuit.json `alert`). The first enemy that
    // spots the target shows "!", holds still for its reaction time, and alerts the registered enemies within the
    // radius (sight not required) one step only; they show "!" in order of distance, then react the same way.
    public sealed class EnemyAlert
    {
        public readonly CombatActor actor;
        readonly EnemyPursuitSettings settings;
        readonly System.Random random;
        // Where this individual sits in the reaction range (0..1); seconds are read from settings when it reacts.
        readonly float reactionT;
        public bool Alerted { get; private set; }
        public bool Pending => !float.IsNaN(pendingAt);
        public float ReadyAt { get; private set; }
        float pendingAt = float.NaN, markUntil;
        SpriteRenderer mark;
        static Sprite markSprite;
        static readonly Dictionary<CombatArena, List<EnemyAlert>> registry = new Dictionary<CombatArena, List<EnemyAlert>>();

        public EnemyAlert(CombatActor actor, EnemyPursuitSettings settings, string id)
        {
            this.actor = actor; this.settings = settings;
            random = EnemyRandom.For(actor.arena, id, actor.Position, EnemyRandom.Alert);
            reactionT = (float)random.NextDouble();
            if (!registry.TryGetValue(actor.arena, out var list)) registry.Add(actor.arena, list = new List<EnemyAlert>());
            list.Add(this);
        }
        public void Reset() { Alerted = Muted = false; pendingAt = float.NaN; ReadyAt = 0; markUntil = 0; if (mark) mark.enabled = false; }
        // Optional discovery area of the receiver (for example "the target stands in my own room"). A chain alert is
        // scheduled and raised only while it holds, so an alert does not pass into another room. Null: always.
        public Func<bool> Receives { get; set; }
        bool CanReceive => Receives == null || Receives();
        // A walker returning home after giving up off screen ignores chain alerts until it arrives or re-detects.
        public bool Muted { get; private set; }
        public void Mute() { Muted = true; pendingAt = float.NaN; markUntil = 0; if (mark) mark.enabled = false; }
        // True while the enemy should stand still facing the target before it acts.
        public bool Reacting(float now) => Alerted && now < ReadyAt;
        public bool Ready(float now) => Alerted && now >= ReadyAt;

        // The enemy saw the target itself: react and, as the first spotter, alert the others.
        public void Spot(float now)
        {
            if (Alerted) return;
            Raise(now);
            if (!settings.alertEnabled) return;
            if (!registry.TryGetValue(actor.arena, out var list)) return;
            list.RemoveAll(a => a == null || !a.actor);
            foreach (var other in list)
            {
                if (other == this || other.Alerted || other.Pending || other.Muted || other.actor.Dead || !other.actor.isActiveAndEnabled || !other.CanReceive) continue;
                float d = Vector2.Distance(other.actor.Position, actor.Position);
                if (d > settings.alertRadius) continue;
                other.pendingAt = now + d * settings.alertDelayPerUnit + EnemyRandom.Range(other.random, 0, settings.alertJitter);
            }
        }
        void Raise(float now)
        {
            Alerted = true; pendingAt = float.NaN; markUntil = now + settings.alertMarkSeconds;
            ReadyAt = now + Mathf.Lerp(settings.reactionDelayMin, settings.reactionDelayMax, reactionT);
        }
        // Advances a scheduled alert (received ones do not alert further) and draws the mark.
        public void Tick(float now)
        {
            if (Pending && now >= pendingAt && !actor.Dead) { if (CanReceive) Raise(now); else pendingAt = float.NaN; }
            DrawMark(now);
        }
        void DrawMark(float now)
        {
            bool show = Alerted && now < markUntil && !actor.Dead;
            if (!show) { if (mark) mark.enabled = false; return; }
            if (!mark)
            {
                // Temporary code-drawn "!" until approved alert art exists.
                if (!markSprite) markSprite = BuildMarkSprite();
                var go = new GameObject("Alert mark (temporary)"); go.transform.SetParent(actor.transform, false);
                mark = go.AddComponent<SpriteRenderer>(); mark.sprite = markSprite; mark.sortingOrder = 40;
                if (actor.arena && actor.arena.spriteMaterial) mark.sharedMaterial = actor.arena.spriteMaterial;
                go.AddComponent<AlertMarkGuard>().owner = this;
            }
            mark.enabled = true;
            float pop = Mathf.Clamp01((now - (markUntil - settings.alertMarkSeconds)) / .12f);
            mark.transform.position = (Vector3)(actor.Position + new Vector2(actor.offset.x * actor.facing, actor.size.y + .55f + .15f * (1 - pop)));
            mark.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, pop);
        }
        static Sprite BuildMarkSprite()
        {
            // 7x16 px: dark outline, warm yellow fill, the stroke and the dot.
            const int w = 7, h = 16; var t = new Texture2D(w, h) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0); var edge = new Color(.12f, .08f, .05f, 1); var fill = new Color(1f, .82f, .2f, 1);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, clear);
            void Box(int x0, int y0, int x1, int y1, Color c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) t.SetPixel(x, y, c); }
            Box(1, 5, 5, 15, edge); Box(2, 6, 4, 14, fill); Box(1, 0, 5, 4, edge); Box(2, 1, 4, 3, fill);
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, 0), 16);
        }
        // Enemies stop calling Tick once dead (and on some early-return paths), so the mark hides itself when its
        // owner dies or the mark time runs out instead of waiting for the next Tick.
        internal bool MarkExpired => !actor || actor.Dead || !actor.arena || actor.arena.Now >= markUntil;
        internal void HideMark() { if (mark) mark.enabled = false; }
    }

    sealed class AlertMarkGuard : MonoBehaviour
    {
        internal EnemyAlert owner;
        void LateUpdate() { if (owner == null || owner.MarkExpired) owner?.HideMark(); }
    }
}
