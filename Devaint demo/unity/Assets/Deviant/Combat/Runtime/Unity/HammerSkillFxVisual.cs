using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Plays the approved hammer skill effect frames (Resources/HammerSkillFx) on the cast clock.
    // Presentation only: it never changes motion, hit timing or hit geometry.
    // Frames were baked against the character's foot pivot at PPU 64 (half of the body's 128),
    // so a renderer placed on the feet with the body's flip lines up with the motion.
    // Ground effects stay where the cast started (the follow-up aftershocks keep playing after a
    // cancel once the first hit has landed); the movement wind follows the body.
    [DefaultExecutionOrder(110)]
    internal sealed class HammerSkillFxVisual : MonoBehaviour
    {
        [Serializable] sealed class ClipData { public string skill, layer, anchor; public float nominalImpact, startTime, fps; public string[] frames; }
        [Serializable] sealed class ZoomData { public float from, to, scale; }
        [Serializable] sealed class ScreenData { public string skill; public float invertFrom, invertTo, darkenFrom, darkenTo, darkenAlpha; public ZoomData[] zooms; }
        [Serializable] sealed class Catalog { public string format; public ClipData[] clips; public ScreenData[] screens; }
        sealed class Clip { public ClipData data; public Sprite[] sprites; public float scale = 1; public SpriteRenderer mirror; public float End => data.startTime + data.frames.Length / data.fps; }

        const string CatalogPath = "HammerSkillFx/clips";
        const int OverlayOrder = 30000;
        static Catalog catalog;
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static Sprite square;
        static Material invertMaterial;

        CombatCast cast;
        CombatArena arena;
        readonly List<Clip> clips = new List<Clip>();
        readonly List<SpriteRenderer> views = new List<SpriteRenderer>();
        ScreenData screen;
        SpriteRenderer invertView, darkView;
        Vector3 anchor;
        int facing;
        float nominalImpact, end, clock = -1, lastNow;
        bool impacted, screenSkipped;

        public static bool Supports(CombatCast cast) => cast && !cast.isBasic && Find(cast.Definition.Id).Count > 0;

        public static HammerSkillFxVisual Attach(CombatCast cast, Transform parent)
        {
            var node = new GameObject("Hammer skill FX " + cast.Definition.Id);
            node.transform.SetParent(parent, false);
            var visual = node.AddComponent<HammerSkillFxVisual>();
            visual.Bind(cast);
            return visual;
        }

        static Catalog Load()
        {
            if (catalog != null) return catalog;
            var text = Resources.Load<TextAsset>(CatalogPath);
            catalog = text ? JsonUtility.FromJson<Catalog>(text.text) : new Catalog { clips = new ClipData[0], screens = new ScreenData[0] };
            return catalog;
        }

        static List<ClipData> Find(string skill)
        {
            var found = new List<ClipData>();
            foreach (var clip in Load().clips) if (clip.skill == skill) found.Add(clip);
            return found;
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
            // 여진은 판정(발 기준 양쪽)에 맞춰 키우고, 뒤쪽까지 덮게 좌우 뒤집은 한 벌을 더 그린다(SkillFxHitScale.HammerAftershock).
            var box = cast.Definition.Id == "hammer_follow_up" && cast.Definition.Hits.Count > 0 ? cast.Definition.Hits[0].Box : null;
            float aftershock = box != null ? SkillFxHitScale.HammerAftershock(box.X, box.Width) : 1;
            bool behind = box != null && SkillFxHitScale.HammerAftershockBehind(box.X, box.Width);
            anchor = body ? body.transform.position : (Vector3)cast.owner.Position;
            foreach (var data in Find(cast.Definition.Id))
            {
                var clip = new Clip { data = data, sprites = new Sprite[data.frames.Length], scale = aftershock };
                for (int i = 0; i < data.frames.Length; i++) clip.sprites[i] = Frame(data.frames[i]);
                clips.Add(clip);
                nominalImpact = data.nominalImpact;
                end = Mathf.Max(end, clip.End);
                var node = new GameObject(data.layer); node.transform.SetParent(transform, false);
                var view = node.AddComponent<SpriteRenderer>();
                if (arena.spriteMaterial) view.sharedMaterial = arena.spriteMaterial;
                view.enabled = false;
                views.Add(view);
                if (behind)
                {
                    var back = new GameObject(data.layer + " behind"); back.transform.SetParent(transform, false);
                    clip.mirror = back.AddComponent<SpriteRenderer>();
                    if (arena.spriteMaterial) clip.mirror.sharedMaterial = arena.spriteMaterial;
                    clip.mirror.enabled = false;
                }
            }
            foreach (var data in Load().screens) if (data.skill == cast.Definition.Id) screen = data;
            if (screen != null)
            {
                end = Mathf.Max(end, screen.darkenTo);
                foreach (var zoom in screen.zooms) end = Mathf.Max(end, zoom.to);
                invertView = Overlay("Hammer impact invert", InvertMaterial());
                darkView = Overlay("Hammer impact darken", arena.spriteMaterial);
                arena.CombatScreenEffectsSuppressed += SkipScreenEffects;
                if (!arena.CombatScreenEffectsAllowed) SkipScreenEffects();
            }
        }

        SpriteRenderer Overlay(string name, Material material)
        {
            if (!square) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
            var node = new GameObject(name); node.transform.SetParent(transform, false);
            var view = node.AddComponent<SpriteRenderer>();
            view.sprite = square;
            if (material) view.sharedMaterial = material;
            view.enabled = false;
            return view;
        }

        static Material InvertMaterial()
        {
            if (invertMaterial) return invertMaterial;
            var shader = Resources.Load<Shader>("CombatScreenInvert");
            if (shader) invertMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            return invertMaterial;
        }

        // Preview time: the frames were baked with the action starting at 0 and the first hit at nominalImpact.
        float Tick()
        {
            bool live = cast && cast.owner && !cast.PresentationCancelled;
            if (live)
            {
                if (cast.ChargePolicy != null && !cast.ChargeRelease.HasValue) return -1;
                clock = cast.Age - cast.ImpactTime + nominalImpact;
                lastNow = arena.Now;
                impacted |= clock >= nominalImpact;
                return clock;
            }
            if (!impacted || clock < 0) return float.NaN;
            return clock + (arena.Now - lastNow);
        }

        void LateUpdate()
        {
            float t = Tick();
            bool effects = arena && arena.FeelFeedback && arena.FeelFeedback.EffectsEnabled;
            if (float.IsNaN(t) || t > end || !effects) { Finish(); return; }
            bool live = cast && cast.owner && !cast.PresentationCancelled;
            var body = live ? cast.owner.visual : null;
            bool darkened = screen != null && !screenSkipped && t >= screen.darkenFrom && t < screen.darkenTo;
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i]; var view = views[i];
                bool attached = clip.data.anchor == "character";
                int frame = t < 0 ? -1 : Mathf.FloorToInt((t - clip.data.startTime) * clip.data.fps + 1e-4f);
                bool show = frame >= 0 && frame < clip.sprites.Length && clip.sprites[frame] && (!attached || body);
                view.enabled = show;
                if (clip.mirror) clip.mirror.enabled = show;
                if (!show) continue;
                view.sprite = clip.sprites[frame];
                view.flipX = attached ? body.flipX : facing < 0;
                view.transform.position = attached ? body.transform.position : anchor;
                var reference = body ? body : (cast && cast.owner ? cast.owner.visual : null);
                if (reference) view.sortingLayerID = reference.sortingLayerID;
                int baseOrder = reference ? reference.sortingOrder : 0;
                view.sortingOrder = darkened && clip.data.layer == "front" ? OverlayOrder + 1
                    : clip.data.layer == "back" ? baseOrder - 1 : baseOrder + 2;
                view.transform.localScale = new Vector3(clip.scale, clip.scale, 1);
                if (clip.mirror)
                {
                    // 발(피벗) 기준 좌우 뒤집기라 같은 자리에 두면 뒤쪽으로 펼쳐진다.
                    clip.mirror.sprite = view.sprite; clip.mirror.flipX = !view.flipX;
                    clip.mirror.transform.position = view.transform.position; clip.mirror.transform.localScale = view.transform.localScale;
                    clip.mirror.sortingLayerID = view.sortingLayerID; clip.mirror.sortingOrder = view.sortingOrder;
                }
            }
            if (screen != null) Screen(t, body);
        }

        void Screen(float t, SpriteRenderer body)
        {
            if (screenSkipped || !arena.CombatScreenEffectsAllowed) { SkipScreenEffects(); return; }
            var view = arena.ViewCamera;
            bool invert = t >= screen.invertFrom && t < screen.invertTo && invertMaterial;
            bool dark = t >= screen.darkenFrom && t < screen.darkenTo;
            foreach (var overlay in new[] { invertView, darkView })
            {
                bool on = overlay == invertView ? invert : dark;
                overlay.enabled = on && view;
                if (!overlay.enabled) continue;
                float height = view.orthographicSize * 2.2f, width = height * view.aspect;
                overlay.transform.position = new Vector3(view.transform.position.x, view.transform.position.y, anchor.z);
                overlay.transform.localScale = new Vector3(width, height, 1);
                if (body) overlay.sortingLayerID = body.sortingLayerID;
                overlay.sortingOrder = OverlayOrder;
                overlay.color = overlay == darkView ? new Color(.043f, .047f, .067f, screen.darkenAlpha) : Color.white;
            }
            var camera = arena.FeelFeedback ? arena.FeelFeedback.CombatCamera : null;
            if (camera) foreach (var zoom in screen.zooms)
                if (t >= zoom.from && t < zoom.to) camera.Punch(zoom.scale, arena.Now + (zoom.to - t));
        }

        void SkipScreenEffects()
        {
            screenSkipped = true;
            if (invertView) invertView.enabled = false;
            if (darkView) darkView.enabled = false;
        }

        void OnDestroy()
        {
            if (arena) arena.CombatScreenEffectsSuppressed -= SkipScreenEffects;
        }

        void Finish()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
