using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Plays the approved rapier special effects (Resources/RapierSkillFx/clips.json, PPU32, foot-origin pivots) on the cast clock.
    // Presentation only: it never changes motion, hit timing, teleport or hit geometry.
    // Clip time 0 is the cast start; while a charge is held the clock is the cast age (the gather), after the
    // release it follows the hit so a short charge jumps straight to the burst. "character" clips follow the body
    // (the slip arrival plays where the teleport landed), "cast_start" clips stay where the cast began.
    [DefaultExecutionOrder(110)]
    internal sealed class RapierSkillFxVisual : MonoBehaviour
    {
        [Serializable] internal sealed class ClipData { public string name, skill, anchor, layer; public float startTime, fps; public string[] frames; }
        [Serializable] sealed class Catalog { public string format; public ClipData[] clips; }
        sealed class Clip { public ClipData data; public Sprite[] sprites; public float End => data.startTime + data.frames.Length / Mathf.Max(1f, data.fps); }

        const string CatalogPath = "RapierSkillFx/clips";
        // Hit timings the frames were baked against (melee.md): the charge burst at full charge, the others at their first hit.
        static readonly Dictionary<string, float> NominalImpact = new Dictionary<string, float> {
            { "rapier_default", .8f }, { "rapier_range", .3f }, { "rapier_movement", .1f }, { "rapier_follow_up", .15f } };
        static Catalog catalog;
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        CombatCast cast;
        CombatArena arena;
        readonly List<Clip> clips = new List<Clip>();
        readonly List<SpriteRenderer> views = new List<SpriteRenderer>();
        Vector3 anchor;
        int facing;
        float nominalImpact, end, clock = -1, lastNow;
        bool released;

        internal static IReadOnlyList<ClipData> Find(string skill)
        {
            var found = new List<ClipData>();
            foreach (var clip in Load().clips) if (clip.skill == skill) found.Add(clip);
            return found;
        }
        public static bool Supports(CombatCast cast) => cast && !cast.isBasic && Find(cast.Definition.Id).Count > 0;

        public static RapierSkillFxVisual Attach(CombatCast cast, Transform parent)
        {
            var node = new GameObject("Rapier skill FX " + cast.Definition.Id);
            node.transform.SetParent(parent, false);
            var visual = node.AddComponent<RapierSkillFxVisual>();
            visual.Bind(cast);
            return visual;
        }

        static Catalog Load()
        {
            if (catalog != null) return catalog;
            var text = Resources.Load<TextAsset>(CatalogPath);
            catalog = text ? JsonUtility.FromJson<Catalog>(text.text) : null;
            if (catalog?.clips == null) catalog = new Catalog { clips = new ClipData[0] };
            return catalog;
        }

        static Sprite Frame(string path)
        {
            if (!spriteCache.TryGetValue(path, out var sprite)) spriteCache[path] = sprite = Resources.Load<Sprite>(path);
            return sprite;
        }

        void Bind(CombatCast value)
        {
            cast = value; arena = value.arena;
            facing = cast.FeelFacing >= 0 ? 1 : -1;
            var body = cast.owner.visual;
            anchor = body ? body.transform.position : (Vector3)cast.owner.Position;
            NominalImpact.TryGetValue(cast.Definition.Id, out nominalImpact);
            foreach (var data in Find(cast.Definition.Id))
            {
                var clip = new Clip { data = data, sprites = new Sprite[data.frames.Length] };
                for (int i = 0; i < data.frames.Length; i++) clip.sprites[i] = Frame(data.frames[i]);
                clips.Add(clip); end = Mathf.Max(end, clip.End);
                var node = new GameObject(data.name); node.transform.SetParent(transform, false);
                var view = node.AddComponent<SpriteRenderer>();
                if (arena.spriteMaterial) view.sharedMaterial = arena.spriteMaterial;
                view.enabled = false;
                views.Add(view);
            }
        }

        // Effect time: cast age while charging, then hit-relative; after a cancel the already started effect runs out.
        float Tick()
        {
            bool live = cast && cast.owner && !cast.PresentationCancelled;
            if (live)
            {
                bool charging = cast.ChargePolicy != null && !cast.ChargeRelease.HasValue;
                clock = charging ? cast.Age : cast.Age - cast.ImpactTime + nominalImpact;
                released |= !charging && clock >= nominalImpact;
                lastNow = arena.Now;
                return clock;
            }
            if (!released || clock < 0) return float.NaN;
            return clock + (arena.Now - lastNow);
        }

        void LateUpdate()
        {
            float t = Tick();
            bool effects = arena && arena.FeelFeedback && arena.FeelFeedback.EffectsEnabled;
            if (float.IsNaN(t) || t > end || !effects) { Finish(); return; }
            bool live = cast && cast.owner && !cast.PresentationCancelled;
            var body = live ? cast.owner.visual : null;
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i]; var view = views[i];
                bool attached = clip.data.anchor == "character";
                int frame = Mathf.FloorToInt((t - clip.data.startTime) * clip.data.fps + 1e-4f);
                bool show = frame >= 0 && frame < clip.sprites.Length && clip.sprites[frame] && (!attached || body);
                view.enabled = show;
                if (!show) continue;
                view.sprite = clip.sprites[frame];
                view.flipX = attached ? body.flipX : facing < 0;
                view.transform.position = attached ? body.transform.position : anchor;
                var reference = body ? body : (cast && cast.owner ? cast.owner.visual : null);
                if (reference) view.sortingLayerID = reference.sortingLayerID;
                int baseOrder = reference ? reference.sortingOrder : 0;
                view.sortingOrder = clip.data.layer == "back" ? baseOrder - 1 : baseOrder + 2;
            }
        }

        void Finish()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
