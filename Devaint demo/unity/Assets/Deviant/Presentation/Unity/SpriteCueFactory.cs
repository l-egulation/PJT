using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Unity
{
    public sealed class SpriteCueFactory : ICueViewFactory, IDisposable
    {
        sealed class Binding { public string version; public Sprite[] sprites; public float scale, rotation; public Func<CueAnchor> follow; }
        readonly Dictionary<string, Binding> latest = new Dictionary<string, Binding>(StringComparer.Ordinal);
        readonly HashSet<View> views = new HashSet<View>();
        readonly Material material;
        readonly int sortingOrder;
        readonly Transform outputRoot;
        readonly bool hasOutputRoot;
        bool disposed;
        public int LiveViewCount => views.Count;
        public SpriteCueFactory(Material material = null, int sortingOrder = 18, Transform outputRoot = null)
        {
            this.material = material; this.sortingOrder = sortingOrder; this.outputRoot = outputRoot;
            hasOutputRoot = !ReferenceEquals(outputRoot, null);
        }
        public void Publish(PresentationCueDefinition definition, Sprite[] sprites, float scale = 1, float rotation = 0, Func<CueAnchor> follow = null)
        {
            Check();
            if (definition == null || sprites == null || sprites.Length != definition.Frames.Count) throw new ArgumentException("A sprite binding must match the cue frames.");
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (float.IsNaN(rotation) || float.IsInfinity(rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));
            latest[definition.Id] = new Binding { version = definition.ContentHash, sprites = (Sprite[])sprites.Clone(), scale = scale, rotation = rotation, follow = follow };
        }
        public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
        {
            Check();
            if (!(definition is PresentationCueDefinition sprite))
            { resolved(CueViewResult.Failure("Sprite factory requires a sprite cue definition.")); return null; }
            if (!latest.TryGetValue(definition.Id, out var binding) || binding.version != definition.ContentHash || binding.sprites.Any(s => !s))
            { resolved(CueViewResult.Failure("Missing sprite binding/frame for cue version: " + definition.Id)); return null; }
            return Begin(sprite, binding.sprites, anchor, resolved, binding.scale, binding.rotation, binding.follow);
        }
        internal Captured Capture(PresentationCueDefinition definition, Sprite[] sprites)
        {
            Check();
            if (definition == null || sprites == null || sprites.Length != definition.Frames.Count || sprites.Any(s => !s))
                throw new ArgumentException("Live sprite references matching every cue frame are required.");
            return new Captured(this, definition, (Sprite[])sprites.Clone());
        }
        IDisposable Begin(PresentationCueDefinition definition, Sprite[] sprites, CueAnchor anchor, Action<CueViewResult> resolved, float scale = 1, float rotation = 0, Func<CueAnchor> follow = null)
        {
            var gameObject = new GameObject(definition.Id); gameObject.SetActive(false);
            if (hasOutputRoot) gameObject.transform.SetParent(outputRoot, false);
            var renderer = gameObject.AddComponent<SpriteRenderer>(); renderer.sortingOrder = sortingOrder;
            if (material) renderer.sharedMaterial = material;
            var view = new View(this, renderer, definition, anchor, sprites, scale, rotation, follow); views.Add(view);
            try { resolved(CueViewResult.Ready(view)); }
            catch { view.Dispose(); throw; }
            return null; // Synchronous preparation owns no outstanding load ticket.
        }
        internal sealed class Captured : ICueViewFactory
        {
            readonly SpriteCueFactory owner;
            readonly PresentationCueDefinition definition;
            readonly Sprite[] sprites;
            internal Captured(SpriteCueFactory owner, PresentationCueDefinition definition, Sprite[] sprites)
            { this.owner = owner; this.definition = definition; this.sprites = sprites; }
            internal void Validate()
            {
                owner.Check();
                if (sprites.Any(s => !s)) throw new InvalidOperationException("Captured sprite media is unavailable: " + definition.Id);
            }
            public IDisposable Begin(ICueDefinition requested, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                Validate();
                if (!(requested is PresentationCueDefinition) || requested.Id != definition.Id || requested.ContentHash != definition.ContentHash)
                { resolved(CueViewResult.Failure("Captured sprite cue version does not match.")); return null; }
                return owner.Begin(definition, sprites, anchor, resolved);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var view in views.ToArray()) view.Dispose(); latest.Clear();
        }
        void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SpriteCueFactory));
            if (hasOutputRoot && !outputRoot) throw new InvalidOperationException("The explicitly supplied Sprite Cue output root is unavailable.");
        }
        sealed class View : ICueView
        {
            readonly SpriteCueFactory owner;
            readonly PresentationCueDefinition definition;
            readonly CueAnchor anchor;
            readonly Sprite[] sprites;
            SpriteRenderer renderer;
            readonly float scale, rotation;
            readonly Func<CueAnchor> follow;
            public View(SpriteCueFactory owner, SpriteRenderer renderer, PresentationCueDefinition definition, CueAnchor anchor, Sprite[] sprites, float scale, float rotation, Func<CueAnchor> follow)
            { this.owner = owner; this.renderer = renderer; this.definition = definition; this.anchor = anchor; this.sprites = sprites; this.scale = scale; this.rotation = rotation; this.follow = follow; }
            public void Sample(float elapsed)
            {
                int frame = definition.FrameAt(elapsed);
                if (!renderer || !sprites[frame]) throw new InvalidOperationException("Cue sprite view/media was destroyed.");
                renderer.sprite = sprites[frame]; renderer.flipX = definition.FlipX;
                var current = follow != null ? follow() : anchor;
                renderer.transform.position = new Vector3(current.X + definition.X[frame] * current.Facing, current.Y + definition.Y[frame], 0);
                renderer.transform.localScale = new Vector3(current.Facing * scale, scale, 1);
                renderer.transform.rotation = Quaternion.Euler(0, 0, current.Facing * rotation);
                renderer.gameObject.SetActive(true);
            }
            public void Dispose()
            {
                if (renderer)
                {
                    var gameObject = renderer.gameObject; gameObject.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(gameObject); else Object.DestroyImmediate(gameObject);
                }
                renderer = null; owner.views.Remove(this);
            }
        }
    }
}
