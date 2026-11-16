using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class PlayerProjectile
    {
        void DetonateCharge(Vector2 center)
        {
            if (execution.Removed) return;
            int step = launcherVisual ? launcherVisual.Step : 0;
            Release();
            float size = Definition.ImpactBoxSize;
            var origin = HitOrigin.WithEffect(HitOrigin.EffectId, CombatHitKind.Blast);
            var targets = arena.TargetsInBox(center, Vector2.one * size);
            if (owner == arena.Player) arena.PrepareSketchHit(origin, targets.Where(t => t && !arena.Occluded(center, t.hurtCollider.ClosestPoint(center))));
            foreach (var target in targets)
            {
                if (!target || target == owner || target.isPlayer || target.isDecoy || target.Dead) continue;
                Vector2 point = target.hurtCollider.ClosestPoint(center);
                if (arena.Occluded(center, point)) continue;
                target.Receive(damage, owner, velocity.x < 0 ? -1 : 1, !Definition.NoBasicStagger, null,
                    origin: origin, contact: CombatHitContacts.Radial(center, point));
            }
            arena.FinishSketchHit();
            bool selected = arena.Audio != null && arena.Audio.Emit("launcher_charge_impact", center);
            arena.ShowExplosion(center, size / 2, playAudio: !selected, boxSize: size);
            // The approved burst by charge step replaces the orange box picture; the burst record stays for its size.
            if (step > 0 && arena.RangedPresentation != null && arena.RangedPresentation.ChargeBurst(step, center, velocity.x < 0 ? -1 : 1) &&
                arena.bombBursts.Count > 0 && arena.bombBursts[arena.bombBursts.Count - 1] && arena.bombBursts[arena.bombBursts.Count - 1].visual)
                arena.bombBursts[arena.bombBursts.Count - 1].visual.enabled = false;
        }

        struct PlayerProjectileHitHost : IPlayerProjectileHitHost
        {
            readonly PlayerProjectile shot;
            readonly Collider2D collider;
            HurtRegion region;
            CombatPlayer returnPlayer;
            readonly CombatHitContact contact;
            public PlayerProjectileHitHost(PlayerProjectile shot, Collider2D collider, CombatHitContact contact)
            { this.shot = shot; this.collider = collider; region = null; returnPlayer = null; this.contact = contact; }
            public bool ValidContact => shot.Valid(collider);
            public float BlastRadius => shot.Definition.BlastRadius;
            public void Detonate() => shot.Detonate();
            public bool CaptureHitRegion() { region = collider.GetComponent<HurtRegion>(); return region; }
            public bool TryClaimActor(float contactAge)
                => shot.execution.TryClaimActor(region.actor.ActorId, shot.Definition.HitInterval > 0 ? shot.velocity.magnitude : 0, contactAge);
            public bool Receive()
            {
                bool accepted = region.actor.Receive(shot.damage, shot.owner, shot.velocity.x < 0 ? -1 : 1, !shot.Definition.NoBasicStagger, null, origin: shot.HitOrigin, contact: contact);
                if (accepted && shot.owner == shot.arena.Player && shot.HitOrigin.ActionId == "shuriken_move_special")
                    shot.arena.Audio?.EmitSkillEvent("shuriken_move_special", "hit", new Vector2(contact.X, contact.Y));
                if (accepted && shot.Definition.Pierce && shot.launcherVisual)
                    shot.arena.RangedPresentation?.ShotContact(shot.launcherVisual, true, false, new Vector2(contact.X, contact.Y), shot.velocity.x < 0 ? -1 : 1);
                return accepted;
            }
            public float ReturnDelay => shot.Definition.ReturnDelay;
            public bool TargetDead => region.actor.Dead;
            public bool CaptureReturnPlayer()
            {
                if (!(shot.owner is CombatPlayer player) || !shot.execution.TryClaimVisit()) return false;
                returnPlayer = player; return true;
            }
            public void VisitTarget() => returnPlayer.TryShurikenVisit(region.actor, shot.sourceWeapon, shot.Definition.ReturnDelay, shot.Definition.BehindGap);
            public bool Pierce => shot.Definition.Pierce;
            public void Release() => shot.Release();
        }

        struct PlayerProjectileBlastHost : IPlayerProjectileBlastHost
        {
            readonly PlayerProjectile shot;
            HashSet<int> seen;
            Collider2D[] candidates;
            int index;
            HurtRegion region;
            Vector2 contact;
            public PlayerProjectileBlastHost(PlayerProjectile shot)
            { this.shot = shot; seen = null; candidates = null; index = -1; region = null; contact = default; }
            public bool Removed => shot.execution.Removed;
            public float BlastRadius => shot.Definition.BlastRadius;
            public void Release() => shot.Release();
            public void BeginCandidates()
            {
                seen = new HashSet<int>();
                candidates = Physics2D.OverlapCircleAll(shot.Position, shot.Definition.BlastRadius);
                if (shot.owner == shot.arena.Player) shot.arena.PrepareSketchBlast(shot.HitOrigin, shot.Position, shot.Definition.BlastRadius);
            }
            public bool NextCandidate()
            {
                if (++index >= candidates.Length) return false;
                region = candidates[index].GetComponent<HurtRegion>(); return true;
            }
            public bool RegionExists => region;
            public bool ActorExists => region.actor;
            public bool ActorIsOwner => region.actor == shot.owner;
            public bool ActorIsPlayer => region.actor.isPlayer;
            public bool ActorIsDecoy => region.actor.isDecoy;
            public bool ActorDead => region.actor.Dead;
            public bool TryClaimActor() => seen.Add(region.actor.ActorId);
            public void CaptureContact() => contact = region.actor.hurtCollider.ClosestPoint(shot.Position);
            public float ContactDistance => Vector2.Distance(shot.Position, contact);
            public bool ContactOccluded => shot.arena.Occluded(shot.Position, contact);
            public void Receive() => region.actor.Receive(shot.damage, shot.owner, region.actor.Center.x < shot.Position.x ? -1 : 1, !shot.Definition.NoBasicStagger, null, origin: shot.HitOrigin.WithEffect(shot.HitOrigin.EffectId, CombatHitKind.Blast), contact: CombatHitContacts.Radial(shot.Position, contact));
            public void ShowExplosion() => shot.arena.ShowShurikenExplosion(shot.Position, shot.Definition.BlastRadius, shot.velocity.x < 0 ? -1 : 1, shot.owner && shot.owner.isPlayer);
            public void RecordExplosion() => shot.arena.Record("shuriken range explosion damage=" + shot.damage.ToString("0.##") + " radius=" + shot.Definition.BlastRadius);
        }
    }
}
