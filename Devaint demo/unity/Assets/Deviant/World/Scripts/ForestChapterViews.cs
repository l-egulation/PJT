using System;
using System.Collections.Generic;
using Deviant.World.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.World
{
    // Native terrain views only. The common ChapterSession owns which absolute
    // module addresses are loaded; this adapter retains the existing chunk pool.
    public sealed class ForestChapterViews : IChapterViewFactory, IDisposable
    {
        readonly InkForestWorld world;
        readonly ForestLayoutSource source;
        readonly Dictionary<int, InkForestChunk> active;
        readonly Queue<InkForestChunk> pool;
        readonly HashSet<View> views = new HashSet<View>();
        readonly HashSet<int> adopted = new HashSet<int>();
        bool disposed;
        public ForestChapterViews(InkForestWorld world, ForestLayoutSource source, Dictionary<int, InkForestChunk> active, Queue<InkForestChunk> pool)
        {
            this.world = world; this.source = source; this.active = active; this.pool = pool;
            foreach (int index in active.Keys) adopted.Add(index);
        }
        public IChapterView PrepareModule(LayoutModule module, string expectedGeometryHash)
        {
            if (disposed || !world || !source.Layout.Repetition.TryIndex(module.Id, out int index))
                throw new InvalidOperationException("The repeated forest view owner is unavailable.");
            if (!source.TryGetGeometry(module.ContentId, out var geometry, out var error) || geometry.ContentHash != expectedGeometryHash)
                throw new InvalidOperationException("The frozen authored forest geometry changed: " + error);
            InkForestChunk chunk = null;
            bool existing = adopted.Remove(index) && active.TryGetValue(index, out chunk);
            if (!existing)
            {
                chunk = pool.Count != 0 ? pool.Dequeue() : Object.Instantiate(world.chunkPrefab, world.transform);
                chunk.gameObject.SetActive(false);
                try { world.Populate(chunk, index); }
                catch { chunk.gameObject.SetActive(false); pool.Enqueue(chunk); throw; }
            }
            var view = new View(this, index, chunk, existing); views.Add(view); return view;
        }
        public IChapterView PreparePlacement(LayoutPlacement placement, LayoutPoint resolvedPosition)
            => throw new InvalidOperationException("Repeated prototype terrain does not invent entity placement content.");
        void Release(View view)
        {
            if (!views.Remove(view)) return;
            if (active.TryGetValue(view.Index, out var current) && current == view.Chunk) active.Remove(view.Index);
            foreach (var box in view.Chunk.platformRoot.GetComponentsInChildren<BoxCollider2D>()) world.PlatformColliders.Remove(box);
            view.Chunk.gameObject.SetActive(false); pool.Enqueue(view.Chunk); world.RecordRecycledChunk();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var remaining = new View[views.Count]; views.CopyTo(remaining);
            foreach (var view in remaining) view.Dispose();
        }
        sealed class View : IChapterView
        {
            readonly ForestChapterViews owner;
            public readonly int Index;
            public readonly InkForestChunk Chunk;
            readonly bool adopted;
            bool active, disposed;
            public View(ForestChapterViews owner, int index, InkForestChunk chunk, bool adopted)
            { this.owner = owner; Index = index; Chunk = chunk; this.adopted = adopted; }
            public void Activate()
            {
                if (disposed || owner.disposed) throw new ObjectDisposedException("Repeated forest view");
                if (active) return;
                if (!adopted) { Chunk.gameObject.SetActive(true); owner.active.Add(Index, Chunk); }
                active = true;
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                owner.Release(this);
            }
        }
    }
}
