using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deviant.Presentation.Unity
{
    [Serializable] public sealed class SpriteShadowFrame
    {
        public Sprite sprite;
        public Vector3[] vertices;
        public int[] edges;
    }
    // A pre-authored outline, not an alpha contour extracted from the rendered image.
    // Provider selection is serialized by Unity's Editor when the template is baked.
    public sealed class SpriteShadowSource : MonoBehaviour
    {
        public SpriteShadowFrame[] frames;
        public SpriteRenderer target;
        sealed class Geometry : IDisposable
        {
            internal NativeArray<Vector3> Vertices;
            internal NativeArray<int> Edges;
            public void Dispose() { if (Vertices.IsCreated) Vertices.Dispose(); if (Edges.IsCreated) Edges.Dispose(); }
        }
        readonly Dictionary<Sprite, Geometry> shapes = new Dictionary<Sprite, Geometry>();
        readonly List<Geometry> owned = new List<Geometry>();
        Geometry fallback;
        Geometry uploaded;
        int revision;
        ShadowCaster2D caster;
        public int Revision => revision;
        public int ShapeUploads { get; private set; }
        public bool MissingFrame { get; private set; }
        // Shares the exact cached outline/flip/pose used by point-light casters.
        // Directional lighting reads it, but never modifies the physics or mesh cache.
        bool TryActiveGeometry(out Geometry geometry)
        {
            geometry = null;
            if (!isActiveAndEnabled) return false;
            if (!caster) caster = GetComponent<ShadowCaster2D>();
            if (!caster || !caster.enabled || !caster.castsShadows) return false;
            if (!target) { geometry = fallback; return geometry != null; }
            return target.enabled && target.sprite && shapes.TryGetValue(target.sprite, out geometry);
        }
        public int DirectionalEdgeCount => TryActiveGeometry(out var geometry) ? geometry.Edges.Length / 2 : 0;
        public bool TryGetDirectionalEdge(int index, out Vector3 a, out Vector3 b)
        {
            a = b = default;
            if (!TryActiveGeometry(out var geometry) || index < 0 || index >= geometry.Edges.Length / 2) return false;
            a = geometry.Vertices[geometry.Edges[index * 2]]; b = geometry.Vertices[geometry.Edges[index * 2 + 1]];
            if (target) { if (target.flipX) { a.x = -a.x; b.x = -b.x; } if (target.flipY) { a.y = -a.y; b.y = -b.y; } }
            a = transform.TransformPoint(a); b = transform.TransformPoint(b); return true;
        }
        void Awake() { if (frames != null && frames.Length > 0) Configure(target, frames); }
        public static void ValidateFrames(SpriteShadowFrame[] input)
        {
            if (input == null || input.Length == 0) throw new ArgumentException("Pre-authored shadow geometry is required.");
            // Validate the whole candidate before replacing a live cache.
            var unique = new HashSet<Sprite>();
            foreach (var frame in input)
            {
                if (frame == null || frame.vertices == null || frame.vertices.Length < 3 || frame.edges == null || frame.edges.Length < 6 || frame.edges.Length % 2 != 0)
                    throw new ArgumentException("A shadow outline needs vertices and line pairs.");
                if (frame.sprite && !unique.Add(frame.sprite)) throw new ArgumentException("Duplicate shadow frame.");
                foreach (var vertex in frame.vertices)
                    if (!Finite(vertex.x) || !Finite(vertex.y) || !Finite(vertex.z) || vertex.z != 0) throw new ArgumentException("Finite planar shadow vertices are required.");
                foreach (int index in frame.edges) if (index < 0 || index >= frame.vertices.Length) throw new ArgumentException("Invalid shadow edge.");
            }
        }
        public void Configure(SpriteRenderer renderer, SpriteShadowFrame[] input)
        {
            ValidateFrames(input);
            Release(); target = renderer; frames = input;
            var aliases = new Dictionary<Vector3[], Dictionary<int[], Geometry>>();
            foreach (var frame in input)
            {
                if (!aliases.TryGetValue(frame.vertices, out var outlines)) { outlines = new Dictionary<int[], Geometry>(); aliases.Add(frame.vertices, outlines); }
                if (!outlines.TryGetValue(frame.edges, out var geometry))
                {
                    geometry = new Geometry { Vertices = new NativeArray<Vector3>(frame.vertices, Allocator.Persistent), Edges = new NativeArray<int>(frame.edges, Allocator.Persistent) };
                    outlines.Add(frame.edges, geometry); owned.Add(geometry);
                }
                if (frame.sprite) shapes.Add(frame.sprite, geometry); else fallback = geometry;
            }
            revision++;
        }
        internal bool Upload(ShadowShape2D shape, ref Sprite previous, ref int version)
        {
            if (owned.Count == 0) return false;
            Sprite sprite = target ? target.sprite : null;
            Geometry geometry = null;
            bool known = target ? sprite && shapes.TryGetValue(sprite, out geometry) : fallback != null;
            if (!target) geometry = fallback;
            MissingFrame = !known;
            if (!known) return false;
            if (uploaded != geometry || version != revision)
            {
                // URP builds the mesh once for this fixed-outline caster. Animation
                // selects cached casters instead of rebuilding meshes on sprite changes.
                // This template is renderer-less: include interior geometry so URP
                // can unshadow the caster's own filled silhouette correctly.
                shape.SetShape(geometry.Vertices, geometry.Edges, ShadowShape2D.OutlineTopology.Lines,
                    allowContraction: false, createInteriorGeometry: true);
                uploaded = geometry; previous = sprite; version = revision; ShapeUploads++;
            }
            shape.SetFlip(target && target.flipX, target && target.flipY);
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void Release() { foreach (var geometry in owned) geometry.Dispose(); owned.Clear(); shapes.Clear(); fallback = uploaded = null; }
        void OnDestroy() => Release();
    }

    [Serializable] public sealed class CachedSpriteShadowProvider : ShadowShape2DProvider
    {
        [NonSerialized] Sprite previous;
        [NonSerialized] int version = -1;
        [NonSerialized] ShadowShape2D shapeOwner;
        public override int MenuPriority() => 100;
        public override bool IsRequiredComponentData(Component sourceComponent) => sourceComponent is SpriteShadowSource;
        public override void OnInitialized(Component sourceComponent, ShadowShape2D shape)
        { shape.SetDefaultTrim(0); if (!ReferenceEquals(shapeOwner, shape)) { shapeOwner = shape; version = -1; } }
        public override void OnBeforeRender(Camera camera, Component sourceComponent, Bounds bounds, ShadowShape2D shape)
        { if (sourceComponent is SpriteShadowSource source) source.Upload(shape, ref previous, ref version); }
    }
}
