using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class BombProjectile
    {
        struct TimedSweepHost : ITimedBombSweepHost
        {
            readonly BombProjectile bomb;
            readonly Vector2 destination;
            Vector2 delta;
            RaycastHit2D[] casts;
            RaycastHit2D current;
            ArenaSurface surface;
            Bounds bounds;
            public TimedSweepHost(BombProjectile bomb, Vector2 destination)
            {
                this.bomb = bomb; this.destination = destination; delta = default;
                casts = null; current = default; surface = null; bounds = default;
            }
            public bool Stopped => bomb.timed.Stopped;
            public bool Landed => bomb.TimedLanded;
            public void SnapshotDelta() => delta = destination - bomb.Position;
            public float HorizontalLength => Mathf.Abs(delta.x);
            public float VerticalLength => Mathf.Abs(delta.y);
            public float DeltaX => delta.x;
            public float DeltaY => delta.y;
            public int QueryHorizontal(int direction, float length)
            {
                casts = Physics2D.CircleCastAll(bomb.Position, bomb.Definition.Radius, Vector2.right * direction, length);
                return casts.Length;
            }
            public int QueryVertical(int direction, float length)
            {
                casts = Physics2D.CircleCastAll(bomb.Position, bomb.Definition.Radius, Vector2.up * direction, length);
                return casts.Length;
            }
            public bool FindSurface(int index)
            {
                current = casts[index]; surface = current.collider.GetComponent<ArenaSurface>(); return surface;
            }
            public bool SurfaceIsOneWay => surface.oneWay;
            public void SnapshotBounds() => bounds = current.collider.bounds;
            public float PositionX => bomb.Position.x;
            public float PositionY => bomb.Position.y;
            public float Radius => bomb.Definition.Radius;
            public float BoundsMinX => bounds.min.x;
            public float BoundsMaxX => bounds.max.x;
            public float BoundsMinY => bounds.min.y;
            public float BoundsMaxY => bounds.max.y;
            public float CastDistance => current.distance;
            // These remain native compound assignments, after every candidate.
            // Y starts from the current position after the complete X response.
            public void ApplyHorizontal(int direction, float distance) => bomb.Position += Vector2.right * direction * distance;
            public void ClearHorizontalVelocity() => bomb.velocity.x = 0;
            public void ApplyVertical(int direction, float distance) => bomb.Position += Vector2.up * direction * distance;
            public void ClearVerticalVelocity() => bomb.velocity.y = 0;
            public void Land(float duration, float contactDistance, float sweepLength) => bomb.timed.Land(duration, contactDistance, sweepLength);
            public void ClearVelocity() => bomb.velocity = Vector2.zero;
            public void RecordLanding() => bomb.arena.Record("timed bomb landed; fuse=" + bomb.Definition.FuseSeconds);
        }
    }
}
