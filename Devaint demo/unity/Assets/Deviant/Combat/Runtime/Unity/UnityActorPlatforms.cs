using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Scene-reference registry only; the pure owner holds deadlines and the
    // original hit planes, independently of later collider movement/bounds.
    public sealed class UnityActorPlatforms : IActorPlatformQuery
    {
        readonly ArenaSurfaceQueryBuffer query = new ArenaSurfaceQueryBuffer();
        readonly IActorPlatforms owner;
        readonly Dictionary<Collider2D, ActorPlatformKey> keys = new Dictionary<Collider2D, ActorPlatformKey>();
        readonly Dictionary<ActorPlatformKey, Collider2D> colliders = new Dictionary<ActorPlatformKey, Collider2D>();
        readonly List<ActorPlatformKey> unused = new List<ActorPlatformKey>();
        public Guid Scope => owner.Scope;
        public int BindingCount => colliders.Count;
        public UnityActorPlatforms(IActorPlatforms owner) { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
        public bool Contains(Collider2D collider) => collider && keys.TryGetValue(collider, out var key) && owner.Contains(key);
        public IReadOnlyList<ActorPlatformContact> CastOneWayPlatforms(ActorBoxCast request)
        {
            if (request.Width <= 0 || request.Height <= 0) throw new ArgumentException("A validated drop query is required.", nameof(request));
            var contacts = new List<ActorPlatformContact>();
            query.BoxCast(new Vector2(request.X, request.Y), new Vector2(request.Width, request.Height), new Vector2(request.DirectionX, request.DirectionY), request.Distance);
            for(int i=0;i<query.Hits.Count;i++)
            {
                var hit=query.Hits[i];
                if (!hit.collider) continue;
                var surface = hit.collider.GetComponent<ArenaSurface>(); if (!surface || !surface.oneWay) continue;
                if (!keys.TryGetValue(hit.collider, out var key))
                { key = new ActorPlatformKey(Scope, Guid.NewGuid()); keys.Add(hit.collider, key); colliders.Add(key, hit.collider); }
                contacts.Add(new ActorPlatformContact(key, hit.normal.y, hit.point.y, !surface.blockDropThrough));
            }
            return contacts.AsReadOnly();
        }
        public ActorPlatformObservation Observe(ActorPlatformKey key)
        {
            if (!key.Valid || key.Scope != Scope) throw new ArgumentException("Platform handle belongs to another actor scope.", nameof(key));
            return new ActorPlatformObservation(key, colliders.TryGetValue(key, out var collider) && collider);
        }
        public void PruneUnused()
        {
            if (colliders.Count == 0) return;
            unused.Clear(); foreach (var key in colliders.Keys) if (!owner.Contains(key)) unused.Add(key);
            foreach (var key in unused) { var collider = colliders[key]; colliders.Remove(key); keys.Remove(collider); }
            unused.Clear();
        }
    }
}
