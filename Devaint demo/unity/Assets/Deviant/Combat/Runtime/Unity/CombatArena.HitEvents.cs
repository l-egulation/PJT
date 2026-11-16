using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class CombatArena : ICombatHitEvents
    {
        readonly CombatHitEventStream acceptedHits = new CombatHitEventStream();
        public event Action<CombatAcceptedHit> AcceptedHit;
        public event Action DamageBatchCompleted;
        public void CompleteDamageBatch() => DamageBatchCompleted?.Invoke();
        public ICombatHitEvents HitEvents => this;
        public bool TryGetCursor(out CombatOutcomeCursor cursor)
        {
            cursor = default;
            if (!this || !ReadModel.TryGetSession(out var session)) return false;
            cursor = new CombatOutcomeCursor(session, acceptedHits.LatestSequence); return true;
        }
        public bool TryRead(CombatOutcomeCursor cursor, CombatAcceptedHit[] destination, out CombatHitPage page)
        {
            page = default;
            return this && ReadModel.TryGetSession(out var session) && acceptedHits.TryRead(session, cursor, destination, out page);
        }
        internal CombatHitOrigin DirectHitOrigin(CombatActor source)
        {
            TryBindActor(source, out var handle);
            return new CombatHitOrigin(handle, default, Guid.NewGuid(), 0, null, Guid.Empty, CombatHitKind.Direct);
        }
        internal void RecordAcceptedHit(CombatHitOrigin origin, CombatActorHandle target, float requested,
            ActorVitalsSnapshot before, ActorDamageResult applied, ActorKinematicsSnapshot pose, float now, CombatHitContact contact, bool critical = false)
        {
            // Native damage semantics are unchanged even for a detached fixture.
            // Observations never reinterpret old handles as the current session.
            if (!this || !ReadModel.TryGetSession(out var session) || !target.Session.Equals(session) || target.ActorId <= 0 ||
                !FindReadActor(target.ActorId) || !origin.IsSpecified) return;
            if (origin.Source.ActorId <= 0 || !origin.Source.Session.Equals(session) ||
                origin.Reflector.ActorId > 0 && !origin.Reflector.Session.Equals(session)) return;
            // This boundary represents a committed hit, not an attack input or a candidate overlap.
            var struck = FindReadActor(target.ActorId);
            acceptedHits.Add(SimulationTickCount, now, origin, target, requested, before, applied, pose, contact, critical);
            if (Sketch != null && origin.EffectiveSource.ActorId == Player.ActorId)
            {
                float healthLost = Math.Max(0, before.Health - applied.State.Health);
                Sketch.RecordDamage(origin.Stamp.WeaponId, healthLost);
                SketchDamageResources(origin, struck, healthLost);
            }
            var accepted = new CombatAcceptedHit(0, SimulationTickCount, now, origin, target, requested, before, applied, pose, contact, critical);
            QueueInkHit(accepted);
            AcceptedHit?.Invoke(accepted);
        }
    }
}
