using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class BombProjectile
    {
        struct BlastHost : IBombBlastHost
        {
            readonly BombProjectile bomb;
            HashSet<int> seen;
            Collider2D[] candidates;
            HurtRegion region;
            Collider2D candidate;
            Vector2 contact;
            BombProjectile fragment;
            public BlastHost(BombProjectile bomb)
            {
                this.bomb = bomb; seen = null; candidates = null; region = null; candidate = null;
                contact = default; fragment = null;
            }
            public void BeginBlast() => seen = new HashSet<int>();
            public int QueryCandidates()
            {
                candidates = Physics2D.OverlapCircleAll(bomb.Position, bomb.Definition.BlastRadius);
                if (bomb.owner == bomb.arena.Player) bomb.arena.PrepareSketchBlast(bomb.HitOrigin, bomb.Position, bomb.Definition.BlastRadius);
                return candidates.Length;
            }
            public void SelectCandidate(int index) { candidate = candidates[index]; region = candidate.GetComponent<HurtRegion>(); }
            public bool RegionExists => region;
            public bool TargetExists => region.actor;
            public bool TargetIsOwner => region.actor == bomb.owner;
            public bool TargetIsPlayer => region.actor.isPlayer;
            public bool TargetIsDecoy => region.actor.isDecoy;
            public bool TargetDead => region.actor.Dead;
            public bool TryClaimTarget() => seen.Add(region.actor.ActorId);
            // Measure to the hurt region the blast touched. An actor with extra regions (the clock boss's
            // extended fist) is otherwise measured to its distant main hurt box and takes no damage.
            public void FindClosestPoint() => contact = candidate.ClosestPoint(bomb.Position);
            public float ContactDistance => Vector2.Distance(bomb.Position, contact);
            public float BlastRadius => bomb.Definition.BlastRadius;
            public bool Occluded() => bomb.arena.Occluded(bomb.Position, contact);
            public void ReceiveBasic()
                => region.actor.Receive(bomb.damage, bomb.owner, region.actor.Center.x < bomb.Position.x ? -1 : 1, true, null, origin: bomb.HitOrigin, contact: CombatHitContacts.Radial(bomb.Position, contact));
            public void BombExploded() => bomb.arena.BombExploded(bomb.Position,bomb.Definition.BlastRadius,bomb.approvedGrenade,bomb.impactContact,bomb.HitOrigin,bomb.HitOrigin.SourceFacing<0||(bomb.HitOrigin.SourceFacing==0&&bomb.velocity.x<0)?-1:1);
            public int FragmentCount => bomb.Definition.FragmentCount;
            public float FragmentAngleAt(int index) => bomb.Definition.FragmentAngleAt(index);
            public void SpawnFragment(float angle)
            {
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                fragment = Spawn(bomb.arena, bomb.owner, bomb.Position + direction * bomb.Definition.FragmentSpawnOffset,
                    direction * bomb.Definition.FragmentSpeed, bomb.Definition.Fragment, bomb.Definition.FragmentDamage * bomb.fragmentMultiplier, bomb.HitOrigin.WithEffect(bomb.HitOrigin.EffectId, CombatHitKind.Fragment),bomb.approvedGrenade);
                fragment.name = "GrenadeFragment";
            }
            // The approved initial-overlap policy is an immediate local explosion.
            public void SweepFragmentAtCurrentPosition() => fragment.SweepTo(fragment.Position);
            public void Remove() { bomb.arena.FinishSketchHit(); bomb.Remove(); }
        }
    }
}
