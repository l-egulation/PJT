using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class TimedBombSweepExecution
    {
        public static void Sweep<T>(ref T host, float duration) where T : struct, ITimedBombSweepHost
        {
            if (host.Stopped || host.Landed) return;
            host.SnapshotDelta();
            float length = host.HorizontalLength;
            if (length > .000001f)
            {
                int sign = host.DeltaX < 0 ? -1 : 1;
                float allowed = length; bool blocked = false;
                int count = host.QueryHorizontal(sign, length);
                for (int i = 0; i < count; i++)
                {
                    if (!host.FindSurface(i) || host.SurfaceIsOneWay) continue;
                    host.SnapshotBounds();
                    if (host.PositionY + host.Radius <= host.BoundsMinY + .0001f ||
                        host.PositionY - host.Radius >= host.BoundsMaxY - .0001f) continue;
                    if (sign > 0 ? host.BoundsMinX < host.PositionX - .0001f :
                        host.BoundsMaxX > host.PositionX + .0001f) continue;
                    float faceDistance = sign > 0 ? host.BoundsMinX - host.PositionX - host.Radius :
                        host.PositionX - host.BoundsMaxX - host.Radius;
                    float distance = host.CastDistance;
                    float face = 0 > faceDistance ? 0 : faceDistance;
                    float contact = distance < face ? distance : face;
                    if (contact <= allowed)
                    {
                        float margin = contact - .001f;
                        allowed = 0 > margin ? 0 : margin; blocked = true;
                    }
                }
                host.ApplyHorizontal(sign, allowed);
                if (blocked) host.ClearHorizontalVelocity();
            }
            length = host.VerticalLength;
            if (length <= .000001f) return;
            int vertical = host.DeltaY < 0 ? -1 : 1;
            float travel = length, contactDistance = length; bool hit = false;
            int verticalCount = host.QueryVertical(vertical, length);
            for (int i = 0; i < verticalCount; i++)
            {
                if (!host.FindSurface(i)) continue;
                host.SnapshotBounds();
                if (host.PositionX + host.Radius <= host.BoundsMinX + .0001f ||
                    host.PositionX - host.Radius >= host.BoundsMaxX - .0001f) continue;
                if (vertical < 0 ? host.BoundsMaxY > host.PositionY + .0001f :
                    host.BoundsMinY < host.PositionY - .0001f) continue;
                if (host.SurfaceIsOneWay && (vertical > 0 ||
                    host.PositionY - host.Radius < host.BoundsMaxY - .03f)) continue;
                if (host.CastDistance <= contactDistance)
                {
                    float distance = host.CastDistance;
                    contactDistance = 0 > distance ? 0 : distance;
                    float margin = host.CastDistance - .001f;
                    travel = 0 > margin ? 0 : margin; hit = true;
                }
            }
            host.ApplyVertical(vertical, travel);
            if (!hit) return;
            host.ClearVerticalVelocity();
            if (vertical < 0)
            {
                host.Land(duration, contactDistance, length);
                host.ClearVelocity();
                host.RecordLanding();
            }
        }
    }
}
