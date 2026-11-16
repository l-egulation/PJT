using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Public queries retain immutable snapshots. CombatActor's internal adapter
    // borrows per-kind views, consumed synchronously before the next query.
    public sealed class UnityActorSurfaceQuery : IActorPlacementQuery
    {
        readonly Func<Collider2D, bool> ignored;
        readonly PhysicsScene2D? scene;
        readonly bool buffered;
        readonly ArenaSurfaceQueryBuffer scratch;
        readonly List<ActorSurfaceContact> contacts = new List<ActorSurfaceContact>(16);
        readonly List<ActorSurfaceBounds> bounds = new List<ActorSurfaceBounds>(16);
        readonly List<ActorRayContact> rays = new List<ActorRayContact>(16);
        readonly ReadOnlyCollection<ActorSurfaceContact> contactView;
        readonly ReadOnlyCollection<ActorSurfaceBounds> boundsView;
        readonly ReadOnlyCollection<ActorRayContact> rayView;
        IReadOnlyList<ActorSurfaceContact> lastContacts = Array.Empty<ActorSurfaceContact>();
        IReadOnlyList<ActorSurfaceBounds> lastBounds = Array.Empty<ActorSurfaceBounds>();
        IReadOnlyList<ActorRayContact> lastRays = Array.Empty<ActorRayContact>();
        int readDepth;
        readonly struct QueryRead : IDisposable
        {
            readonly UnityActorSurfaceQuery owner;
            public readonly ArenaSurfaceQueryBuffer Native;
            public readonly List<ActorSurfaceContact> Contacts;
            public readonly List<ActorSurfaceBounds> Bounds;
            public readonly List<ActorRayContact> Rays;
            public QueryRead(UnityActorSurfaceQuery owner)
            {
                this.owner = owner;
                bool nested = owner.readDepth++ != 0;
                Native = nested ? new ArenaSurfaceQueryBuffer(owner.scene) : owner.scratch;
                Contacts = nested ? new List<ActorSurfaceContact>() : owner.contacts;
                Bounds = nested ? new List<ActorSurfaceBounds>() : owner.bounds;
                Rays = nested ? new List<ActorRayContact>() : owner.rays;
            }
            public void Dispose() => owner.readDepth--;
        }


        public UnityActorSurfaceQuery(Func<Collider2D, bool> ignored) : this(ignored, null, false) { }
        public UnityActorSurfaceQuery(Func<Collider2D, bool> ignored, PhysicsScene2D scene) : this(ignored, scene, false) { }
        UnityActorSurfaceQuery(Func<Collider2D, bool> ignored, PhysicsScene2D? scene, bool buffered)
        {
            if (scene.HasValue && !scene.Value.IsValid()) throw new ArgumentException("A valid explicit physics scene is required.", nameof(scene));
            this.ignored = ignored ?? throw new ArgumentNullException(nameof(ignored));
            this.scene = scene; this.buffered = buffered;
            scratch = new ArenaSurfaceQueryBuffer(scene);
            contactView = contacts.AsReadOnly(); boundsView = bounds.AsReadOnly(); rayView = rays.AsReadOnly();
        }
        internal static IActorPlacementQuery ForActor(Func<Collider2D, bool> ignored) => new UnityActorSurfaceQuery(ignored, null, true);

        public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request)
        {
            if (request.Width <= 0 || request.Height <= 0) throw new ArgumentException("A validated body cast is required.", nameof(request));
            // Snapshot callers may reenter through their ignored predicate. Only
            // the actor-owned, callback-free binding borrows reusable storage.
            using var read = new QueryRead(this);
            var query = read.Native; var result = read.Contacts; result.Clear();
            query.BoxCast(new Vector2(request.X, request.Y), new Vector2(request.Width, request.Height), new Vector2(request.DirectionX, request.DirectionY), request.Distance);
            for (int i = 0; i < query.Hits.Count; i++)
            {
                var hit = query.Hits[i];
                if (!hit.collider || !hit.collider.TryGetComponent<ArenaSurface>(out var surface)) continue;
                result.Add(new ActorSurfaceContact(hit.distance, hit.normal.x, hit.normal.y, hit.collider.bounds.max.y, surface.oneWay, ignored(hit.collider)));
            }
            return buffered ? contactView : Publish(result, ref lastContacts, (a, b) => a.Distance == b.Distance && a.NormalX == b.NormalX && a.NormalY == b.NormalY && a.Top == b.Top && a.OneWay == b.OneWay && a.Ignored == b.Ignored);
        }
        public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request)
        {
            if (request.Width <= 0 || request.Height <= 0) throw new ArgumentException("A validated overlap box is required.", nameof(request));
            using var read = new QueryRead(this);
            var query = read.Native; var result = read.Bounds; result.Clear();
            query.OverlapBox(new Vector2(request.X, request.Y), new Vector2(request.Width, request.Height));
            for (int i = 0; i < query.Overlaps.Count; i++)
            {
                var collider = query.Overlaps[i];
                if (!collider || !collider.TryGetComponent<ArenaSurface>(out var surface)) continue;
                var b = collider.bounds; var min = b.min; var max = b.max;
                result.Add(new ActorSurfaceBounds(min.x, min.y, min.z, max.x, max.y, max.z, surface.oneWay));
            }
            return buffered ? boundsView : Publish(result, ref lastBounds, (a, b) => a.MinX == b.MinX && a.MinY == b.MinY && a.MinZ == b.MinZ && a.MaxX == b.MaxX && a.MaxY == b.MaxY && a.MaxZ == b.MaxZ && a.OneWay == b.OneWay);
        }
        public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request)
        {
            if (request.DirectionX == 0 && request.DirectionY == 0) throw new ArgumentException("A validated ray is required.", nameof(request));
            using var read = new QueryRead(this);
            var query = read.Native; var result = read.Rays; result.Clear();
            query.Raycast(new Vector2(request.X, request.Y), new Vector2(request.DirectionX, request.DirectionY), request.Distance);
            for (int i = 0; i < query.Hits.Count; i++)
            {
                var hit = query.Hits[i];
                if (hit.collider && hit.collider.TryGetComponent<ArenaSurface>(out _)) result.Add(new ActorRayContact(hit.point.y, hit.normal.y, ignored(hit.collider)));
            }
            return buffered ? rayView : Publish(result, ref lastRays, (a, b) => a.Y == b.Y && a.NormalY == b.NormalY && a.Ignored == b.Ignored);
        }
        static IReadOnlyList<T> Publish<T>(List<T> values, ref IReadOnlyList<T> previous, Func<T, T, bool> equal)
        {
            bool same = values.Count == previous.Count;
            for (int i = 0; same && i < values.Count; i++) same = equal(values[i], previous[i]);
            if (!same) previous = Array.AsReadOnly(values.ToArray());
            return previous;
        }
    }
}
