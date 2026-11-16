using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class PlayerProjectile
    {
        struct PlayerProjectileContactHost : IPlayerProjectileContactHost
        {
            readonly PlayerProjectile shot;
            readonly Collider2D collider;
            ArenaSurface surface;
            HurtRegion region;
            public PlayerProjectileContactHost(PlayerProjectile shot, Collider2D collider)
            { this.shot = shot; this.collider = collider; surface = null; region = null; }
            public bool FindSurface() { surface = collider.GetComponent<ArenaSurface>(); return surface; }
            public bool IgnoresTerrain => shot && shot.Definition.IgnoresTerrain;
            // 큰 관통 수리검(반지름 1, 발사 높이 1)은 바닥을 긁으며 날아간다: 원 아래 절반에만 걸치는 바닥은 통과하고
            // 앞을 막는 벽(원 가운데보다 높이 솟은 면)에만 막힌다(2026-11-11, 바닥에 딱 닿아 위치에 따라 즉시 사라지던 문제).
            public bool SurfaceIsOneWay => surface.oneWay || shot && PlayerProjectile.SkimsFloor(shot.Definition.Type, shot.Position.y, shot.Definition.Radius, collider.bounds.max.y);
            public bool FindRegion() { region = collider.GetComponent<HurtRegion>(); return region; }
            public bool TargetExists => region.actor;
            public bool TargetIsOwner => region.actor == shot.owner;
            public bool TargetIsPlayer => region.actor.isPlayer;
            public bool TargetIsDecoy => region.actor.isDecoy;
            public bool TargetDead => region.actor.Dead;
            public int TargetId => region.actor.ActorId;
        }

        struct PlayerProjectileOverlapHost : IPlayerProjectileOverlapHost
        {
            readonly PlayerProjectile shot;
            readonly Collider2D first, second;
            CombatActor firstActor, secondActor;
            public PlayerProjectileOverlapHost(PlayerProjectile shot, Collider2D first, Collider2D second)
            { this.shot = shot; this.first = first; this.second = second; firstActor = secondActor = null; }
            public string ProjectileType => shot.Definition.Type;
            public bool FirstValid => shot.Valid(first);
            public bool FirstIsSurface => first.GetComponent<ArenaSurface>();
            public bool SecondValid => shot.Valid(second);
            public bool SecondIsSurface => second.GetComponent<ArenaSurface>();
            public void CaptureActors()
            {
                firstActor = first.GetComponent<HurtRegion>().actor;
                secondActor = second.GetComponent<HurtRegion>().actor;
            }
            public float FirstSquaredDistance => (firstActor.Center - shot.Position).sqrMagnitude;
            public float SecondSquaredDistance => (secondActor.Center - shot.Position).sqrMagnitude;
            public int FirstPriority => Priority(first);
            public int SecondPriority => Priority(second);
        }

        struct PlayerProjectileCastOrderHost : IPlayerProjectileCastOrderHost
        {
            readonly RaycastHit2D first, second;
            public PlayerProjectileCastOrderHost(RaycastHit2D first, RaycastHit2D second)
            { this.first = first; this.second = second; }
            public float FirstDistance => first.distance;
            public float SecondDistance => second.distance;
            public int FirstPriority => Priority(first.collider);
            public int SecondPriority => Priority(second.collider);
        }

        static int CompareSweepCasts(RaycastHit2D first, RaycastHit2D second)
        {
            var host = new PlayerProjectileCastOrderHost(first, second);
            return PlayerProjectileSweepExecution.CompareCast(ref host);
        }

        struct PlayerProjectileSweepHost : IPlayerProjectileSweepHost
        {
            readonly PlayerProjectile shot;
            readonly Vector2 destination;
            Vector2 delta, start;
            List<Collider2D> overlaps;
            List<RaycastHit2D> hits;
            RaycastHit2D current;
            public PlayerProjectileSweepHost(PlayerProjectile shot, Vector2 destination)
            {
                this.shot = shot; this.destination = destination; delta = start = default;
                overlaps = null; hits = null; current = default;
            }
            public bool Removed => shot.execution.Removed;
            public int QueryOverlaps()
            {
                overlaps = new List<Collider2D>(Physics2D.OverlapCircleAll(shot.Position, shot.Definition.Radius));
                return overlaps.Count;
            }
            public void SortOverlaps() => overlaps.Sort(shot.CompareOverlap);
            public bool ResolveOverlap(int index) => shot.Resolve(overlaps[index]);
            public void SnapshotDelta() => delta = destination - shot.Position;
            public float DeltaLength => delta.magnitude;
            public void CaptureStart() => start = shot.Position;
            public int QueryCast(float length)
            {
                hits = new List<RaycastHit2D>(Physics2D.CircleCastAll(start, shot.Definition.Radius, delta / length, length));
                return hits.Count;
            }
            public void SortCasts() => hits.Sort(CompareSweepCasts);
            public bool CastValid(int index) { current = hits[index]; return shot.Valid(current.collider); }
            public float CastDistance => current.distance;
            public void ApplyContact(float length, float distance) => shot.Position = start + delta / length * distance;
            public float ContactAge(float rawDistance) => shot.execution.ContactAge(rawDistance, shot.velocity.magnitude);
            public bool ResolveCast(float contactAge) => shot.Resolve(current.collider, contactAge,
                CombatHitContacts.At(current.point, shot.velocity, current.distance > 0 ? HitAnchorKind.SweepContact : HitAnchorKind.OverlapRepresentative));
            public void ApplyDestination() => shot.Position = destination;
        }
    }
}
