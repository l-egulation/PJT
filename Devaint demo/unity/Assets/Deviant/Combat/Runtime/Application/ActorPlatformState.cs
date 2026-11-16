using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public sealed class ActorPlatformState : IActorPlatforms
    {
        readonly Dictionary<ActorPlatformKey, ActorIgnoredPlatform> ignored = new Dictionary<ActorPlatformKey, ActorIgnoredPlatform>();
        bool changing;
        public Guid Scope { get; }
        public ActorPlatformState() : this(Guid.NewGuid()) { }
        public ActorPlatformState(Guid scope) { if (scope == Guid.Empty) throw new ArgumentException("An actor platform scope is required."); Scope = scope; }
        public bool Contains(ActorPlatformKey key) => key.Valid && key.Scope == Scope && ignored.ContainsKey(key);
        public ActorPlatformSnapshot Read() => new ActorPlatformSnapshot(Scope, ignored.Values);
        public ActorDropMotion TryDrop(ActorBodyFrame body, float velocityY, float now, IActorSurfaceQuery surfaces, IActorPlatformQuery platforms)
        {
            Enter();
            try
            {
                Check(now); Check(velocityY); Validate(platforms);
                if (!ActorContactResolver.ProbeGround(body, velocityY, surfaces)) return new ActorDropMotion(false, 0, velocityY);
                var contacts = platforms.CastOneWayPlatforms(new ActorBoxCast(body.X + body.OffsetX * body.Facing, body.Y + .03f, body.Width - .08f, .04f, 0, -1, .08f))
                    ?? throw new InvalidOperationException("Platform query returned no contact collection.");
                foreach (var contact in contacts) if (!contact.Key.Valid || contact.Key.Scope != Scope) throw new InvalidOperationException("A foreign platform contact cannot be retained.");
                // A non-droppable support wins even when a droppable one overlaps it.
                foreach (var contact in contacts)
                    if (contact.NormalY > .8f && !contact.CanDropThrough) return new ActorDropMotion(false, 0, velocityY);
                foreach (var contact in contacts)
                {
                    if (contact.NormalY <= .8f) continue;
                    // Validate the whole downward request with the common axis
                    // solver. Only one-way surfaces may be passed; an old ignore
                    // handle cannot grant permission to enter a current solid.
                    if (ActorContactResolver.ResolveSolidAxis(body, -.08f, false, surfaces).Blocked) return new ActorDropMotion(false, 0, velocityY);
                    float until = now + .25f; Check(until); ignored[contact.Key] = new ActorIgnoredPlatform(contact.Key, until, contact.PlaneY);
                    return new ActorDropMotion(true, -.08f, -2);
                }
                return new ActorDropMotion(false, 0, velocityY);
            }
            finally { changing = false; }
        }
        public void Refresh(float now, float bodyY, float bodyHeight, IActorPlatformQuery platforms)
        {
            Enter();
            try
            {
                Check(now); Check(bodyY); Check(bodyHeight); if (bodyHeight <= 0) throw new ArgumentOutOfRangeException(nameof(bodyHeight));
                float top = bodyY + bodyHeight; Check(top); Validate(platforms);
                var released = new List<ActorPlatformKey>();
                foreach (var pair in ignored)
                {
                    var observation = platforms.Observe(pair.Key);
                    if (!observation.Key.Equals(pair.Key)) throw new InvalidOperationException("Platform observation identity changed.");
                    // A low rung can be only one body-height above the solid
                    // floor, so full clearance below it may never be possible.
                    // Jumping back above its top is also safe: one-way surfaces
                    // already permit upward travel and can catch the next fall.
                    bool clear = top <= pair.Value.PlaneY - .02f || bodyY >= pair.Value.PlaneY + .02f;
                    if (!observation.Alive || now >= pair.Value.Until && clear) released.Add(pair.Key);
                }
                // Queries may fail; release only after all observations succeeded.
                foreach (var key in released) ignored.Remove(key);
            }
            finally { changing = false; }
        }
        public void Reset() { Enter(); try { ignored.Clear(); } finally { changing = false; } }
        void Validate(IActorPlatformQuery platforms)
        { if (platforms == null) throw new ArgumentNullException(nameof(platforms)); if (platforms.Scope != Scope) throw new ArgumentException("Platform query belongs to another actor scope."); }
        void Enter() { if (changing) throw new InvalidOperationException("Actor platform state cannot be changed during a query."); changing = true; }
        static void Check(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
