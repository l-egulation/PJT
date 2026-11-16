using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deviant.Presentation.Unity
{
    public sealed class SpriteLightingFrame
    {
        public Sprite Original, Lit, Flat;
        public Vector3[] ShadowVertices;
        public int[] ShadowEdges;
    }
    [DefaultExecutionOrder(1000)]
    public sealed class SpriteLightingBinding : MonoBehaviour
    {
        readonly Dictionary<Sprite, SpriteLightingFrame> frames = new Dictionary<Sprite, SpriteLightingFrame>();
        SpriteRenderer target;
        Material originalMaterial;
        readonly List<SpriteShadowSource> shadows = new List<SpriteShadowSource>();
        readonly Dictionary<SpriteLightingFrame, ShadowCaster2D> casters = new Dictionary<SpriteLightingFrame, ShadowCaster2D>();
        readonly Dictionary<SpriteLightingFrame, Material> litMaterials = new Dictionary<SpriteLightingFrame, Material>();
        readonly Dictionary<SpriteLightingFrame, Material> flatMaterials = new Dictionary<SpriteLightingFrame, Material>();
        readonly Dictionary<Texture2D, Material> normalMaterials = new Dictionary<Texture2D, Material>();
        readonly List<Material> ownedMaterials = new List<Material>();
        SpriteLightingFrame last;
        public bool MissingFrame { get; private set; }
        public bool NormalsEnabled { get; set; } = true;
        public bool ShadowsEnabled { get; set; } = true;
        public void Bind(SpriteRenderer renderer, Material lit, SpriteLightingFrame[] input, SpriteShadowSource source)
        {
            if (!renderer || !lit || !source || !source.GetComponent<ShadowCaster2D>() || input == null || input.Length == 0) throw new ArgumentException("A complete sprite lighting binding is required.");
            if (!lit.HasProperty("_SurfaceNormal")) throw new ArgumentException("Use a lighting shader with an explicit _SurfaceNormal binding.");
            if (target) throw new InvalidOperationException("Dispose the existing lighting binding before rebinding.");
            var candidate = new Dictionary<Sprite, SpriteLightingFrame>();
            foreach (var frame in input)
            {
                if (frame == null || !frame.Original || !Matches(frame.Original, frame.Lit) || !Matches(frame.Original, frame.Flat))
                    throw new ArgumentException("Color and lit frames must preserve rect, pivot and PPU.");
                if (!HasNormalMap(frame.Lit) || !HasNormalMap(frame.Flat)) throw new ArgumentException("Both lit and flat frames require explicit normal-map secondary data.");
                SpriteShadowSource.ValidateFrames(new[] { new SpriteShadowFrame { sprite = frame.Lit, vertices = frame.ShadowVertices, edges = frame.ShadowEdges } });
                candidate.Add(frame.Original, frame);
                if (frame.Lit != frame.Original) candidate.Add(frame.Lit, frame);
                if (frame.Flat != frame.Original && frame.Flat != frame.Lit) candidate.Add(frame.Flat, frame);
            }
            foreach (var frame in input)
            {
                var output = shadows.Count == 0 ? source : Instantiate(source, source.transform.parent, false);
                var shadowFrames = new List<SpriteShadowFrame> { new SpriteShadowFrame { sprite = frame.Lit, vertices = frame.ShadowVertices, edges = frame.ShadowEdges } };
                if (frame.Original != frame.Lit) shadowFrames.Add(new SpriteShadowFrame { sprite = frame.Original, vertices = frame.ShadowVertices, edges = frame.ShadowEdges });
                if (frame.Flat != frame.Original && frame.Flat != frame.Lit) shadowFrames.Add(new SpriteShadowFrame { sprite = frame.Flat, vertices = frame.ShadowVertices, edges = frame.ShadowEdges });
                output.Configure(renderer, shadowFrames.ToArray());
                // Both aliases use the same fixed outline; avoid a second geometry
                // upload when switching the normal-map A/B view.
                shadows.Add(output); casters.Add(frame, output.GetComponent<ShadowCaster2D>());
                litMaterials.Add(frame, MaterialFor(lit, NormalMap(frame.Lit)));
                flatMaterials.Add(frame, MaterialFor(lit, NormalMap(frame.Flat)));
            }
            foreach (var pair in candidate) frames.Add(pair.Key, pair.Value);
            target = renderer; originalMaterial = renderer.sharedMaterial;
            Apply();
        }
        public void Apply()
        {
            if (!isActiveAndEnabled || !target) return;
            MissingFrame = !target.sprite || !frames.TryGetValue(target.sprite, out last);
            foreach (var pair in casters)
            {
                var caster = pair.Value; if (!caster) continue;
                bool active = ShadowsEnabled && !MissingFrame && pair.Key == last && target.enabled && target.gameObject.activeInHierarchy;
                bool wasActive = caster.enabled; caster.enabled = active;
                // Selection happens in LateUpdate, after URP's usual Update phase.
                // Register a newly selected cached caster before this frame renders.
                if (active && !wasActive) caster.Update();
            }
            if (MissingFrame) { if (ownedMaterials.Contains(target.sharedMaterial)) target.sharedMaterial = originalMaterial; return; }
            target.sprite = NormalsEnabled ? last.Lit : last.Flat;
            target.sharedMaterial = NormalsEnabled ? litMaterials[last] : flatMaterials[last];
        }
        void LateUpdate() => Apply();
        void OnDisable()
        {
            foreach (var caster in casters.Values) if (caster) caster.enabled = false;
            if (!target) return;
            if (ownedMaterials.Contains(target.sharedMaterial)) target.sharedMaterial = originalMaterial;
            if (last != null && (target.sprite == last.Lit || target.sprite == last.Flat)) target.sprite = last.Original;
        }
        static bool Matches(Sprite original, Sprite other) => other && original.rect == other.rect && original.pivot == other.pivot && original.pixelsPerUnit == other.pixelsPerUnit;
        static bool HasNormalMap(Sprite sprite) => NormalMap(sprite);
        static Texture2D NormalMap(Sprite sprite)
        {
            var secondary = new SecondarySpriteTexture[sprite.GetSecondaryTextureCount()]; sprite.GetSecondaryTextures(secondary);
            foreach (var entry in secondary) if (entry.name == "_NormalMap" && entry.texture && entry.texture.width == sprite.texture.width && entry.texture.height == sprite.texture.height) return entry.texture;
            return null;
        }
        Material MaterialFor(Material prototype, Texture2D normal)
        {
            if (normalMaterials.TryGetValue(normal, out var cached)) return cached;
            var value = new Material(prototype) { name = prototype.name + " / " + normal.name };
            value.SetTexture("_SurfaceNormal", normal); normalMaterials.Add(normal, value); ownedMaterials.Add(value); return value;
        }
        void OnDestroy()
        {
            foreach (var shadow in shadows) if (shadow) Destroy(shadow.gameObject);
            foreach (var value in ownedMaterials) if (value) Destroy(value);
        }
    }
}
