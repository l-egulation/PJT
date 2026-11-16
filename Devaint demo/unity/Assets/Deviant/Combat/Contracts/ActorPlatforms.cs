using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Deviant.Combat.Contracts
{
    // An actor-binding runtime handle, never a content/save/replay identity.
    public readonly struct ActorPlatformKey : IEquatable<ActorPlatformKey>
    {
        public Guid Scope { get; }
        public Guid Value { get; }
        public bool Valid => Scope != Guid.Empty && Value != Guid.Empty;
        public ActorPlatformKey(Guid scope, Guid value)
        { if (scope == Guid.Empty || value == Guid.Empty) throw new ArgumentException("Platform handles require a scope and identity."); Scope = scope; Value = value; }
        public bool Equals(ActorPlatformKey other) => Scope == other.Scope && Value == other.Value;
        public override bool Equals(object obj) => obj is ActorPlatformKey other && Equals(other);
        public override int GetHashCode() => unchecked(Scope.GetHashCode() * 397 ^ Value.GetHashCode());
    }
    public readonly struct ActorPlatformContact
    {
        public ActorPlatformKey Key { get; }
        public float NormalY { get; }
        public float PlaneY { get; }
        public bool CanDropThrough { get; }
        public ActorPlatformContact(ActorPlatformKey key, float normalY, float planeY, bool canDropThrough)
        { if (!key.Valid || !ActorBodyFrame.Finite(normalY) || !ActorBodyFrame.Finite(planeY)) throw new ArgumentException("Platform contacts require a valid handle, normal, and hit plane."); Key = key; NormalY = normalY; PlaneY = planeY; CanDropThrough = canDropThrough; }
    }
    public readonly struct ActorPlatformObservation
    {
        public ActorPlatformKey Key { get; }
        public bool Alive { get; }
        public ActorPlatformObservation(ActorPlatformKey key, bool alive)
        { if (!key.Valid) throw new ArgumentException("Platform observations require a valid handle."); Key = key; Alive = alive; }
    }
    public interface IActorPlatformQuery
    {
        Guid Scope { get; }
        IReadOnlyList<ActorPlatformContact> CastOneWayPlatforms(ActorBoxCast request);
        ActorPlatformObservation Observe(ActorPlatformKey key);
    }
    public readonly struct ActorIgnoredPlatform
    {
        public ActorPlatformKey Key { get; }
        public float Until { get; }
        public float PlaneY { get; }
        public ActorIgnoredPlatform(ActorPlatformKey key, float until, float planeY)
        { if (!key.Valid || !ActorBodyFrame.Finite(until) || !ActorBodyFrame.Finite(planeY)) throw new ArgumentException("Ignored platform state must remain finite."); Key = key; Until = until; PlaneY = planeY; }
    }
    public sealed class ActorPlatformSnapshot
    {
        public Guid Scope { get; }
        public IReadOnlyList<ActorIgnoredPlatform> Entries { get; }
        public ActorPlatformSnapshot(Guid scope, IEnumerable<ActorIgnoredPlatform> entries)
        {
            if (scope == Guid.Empty || entries == null) throw new ArgumentException("A platform snapshot requires a scope and entries.");
            var copy = new List<ActorIgnoredPlatform>(); var seen = new HashSet<ActorPlatformKey>();
            foreach (var entry in entries)
            { if (!entry.Key.Valid || entry.Key.Scope != scope || !seen.Add(entry.Key)) throw new ArgumentException("Platform snapshot entries must be unique in their scope."); copy.Add(entry); }
            Scope = scope; Entries = new ReadOnlyCollection<ActorIgnoredPlatform>(copy);
        }
    }
    public readonly struct ActorDropMotion
    {
        public bool Applied { get; }
        public float DeltaY { get; }
        public float VelocityY { get; }
        public ActorDropMotion(bool applied, float deltaY, float velocityY)
        { if (!ActorBodyFrame.Finite(deltaY) || !ActorBodyFrame.Finite(velocityY)) throw new ArgumentException("Drop motion must remain finite."); Applied = applied; DeltaY = deltaY; VelocityY = velocityY; }
    }
    public interface IActorPlatforms
    {
        Guid Scope { get; }
        ActorPlatformSnapshot Read();
        bool Contains(ActorPlatformKey key);
        ActorDropMotion TryDrop(ActorBodyFrame body, float velocityY, float now, IActorSurfaceQuery surfaces, IActorPlatformQuery platforms);
        void Refresh(float now, float bodyY, float bodyHeight, IActorPlatformQuery platforms);
        void Reset();
    }
}
