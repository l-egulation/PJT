using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class BombProjectile
    {
        struct ContactObstacleHost : IContactBombObstacleHost
        {
            readonly BombProjectile bomb;
            readonly Collider2D collider;
            readonly float movementY, normalY;
            ArenaSurface surface;
            HurtRegion region;
            public ContactObstacleHost(BombProjectile bomb, Collider2D collider, Vector2 movement, Vector2 normal)
            {
                this.bomb = bomb; this.collider = collider; movementY = movement.y; normalY = normal.y;
                surface = null; region = null;
            }
            public bool FindSurface() { surface = collider.GetComponent<ArenaSurface>(); return surface; }
            public bool SurfaceIsOneWay => surface.oneWay;
            public float MovementY => movementY;
            public float NormalY => normalY;
            public float BombBottom => bomb.Position.y - bomb.Definition.Radius;
            public float SurfaceTop => collider.bounds.max.y;
            public bool FindRegion() { region = collider.GetComponent<HurtRegion>(); return region; }
            public bool TargetExists => region.actor;
            public bool TargetIsOwner => region.actor == bomb.owner;
            public bool TargetIsPlayer => region.actor.isPlayer;
            public bool TargetIsDecoy => region.actor.isDecoy;
            public bool TargetDead => region.actor.Dead;
        }

        struct ContactSweepHost : IContactBombSweepHost
        {
            readonly BombProjectile bomb;
            readonly Vector2 destination;
            Vector2 delta;
            Collider2D[] overlaps;
            RaycastHit2D[] casts;
            RaycastHit2D current;
            float visualDistance;
            public ContactSweepHost(BombProjectile bomb, Vector2 destination)
            {
                this.bomb = bomb; this.destination = destination; delta = default;
                overlaps = null; casts = null; current = default;
                visualDistance=float.PositiveInfinity;
            }
            public bool Stopped => bomb.timed.Stopped;
            public float FuseSeconds => bomb.Definition.FuseSeconds;
            public void SweepTimed() => bomb.SweepTimed(destination, 0);
            public void SnapshotDelta() { delta=destination-bomb.Position;bomb.impactContact=null; }
            public int QueryOverlaps()
            {
                overlaps = Physics2D.OverlapCircleAll(bomb.Position, bomb.Definition.Radius);
                return overlaps.Length;
            }
            public bool OverlapIsObstacle(int index)
            {
                bool accepted=bomb.Obstacle(overlaps[index],delta,Vector2.up);
                if(accepted)bomb.impactContact=overlaps[index].ClosestPoint(bomb.Position);
                return accepted;
            }
            public float DeltaLength => delta.magnitude;
            public int QueryCast(float length)
            {
                casts = Physics2D.CircleCastAll(bomb.Position, bomb.Definition.Radius, delta / length, length);
                return casts.Length;
            }
            public bool CastIsObstacle(int index)
            {
                current = casts[index];
                bool accepted=bomb.Obstacle(current.collider,delta,current.normal);
                if(accepted && current.distance<visualDistance){visualDistance=current.distance;bomb.impactContact=current.point;}
                return accepted;
            }
            public float CastDistance => current.distance;
            // Read Position here, not at entry: queries/qualification precede the
            // original compound assignment, and an explosion observes the write.
            public void ApplyTravel(float length, float distance) => bomb.Position += delta / length * distance;
            public void Explode() => bomb.Explode();
        }
    }
}
