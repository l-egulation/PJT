using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class CastSweepExecution
    {
        public static void Apply<T>(CastSweepBox? box, int direction, float fromX, float fromY, float toX, float toY,
            bool effectCenter, bool nearest, ref T host) where T : struct, ICastSweepHost
        {
            if (!box.HasValue) return;
            var shape = box.Value; float offsetX = shape.X * direction, offsetY = shape.Y;
            int steps = Math.Max(1, (int)Math.Ceiling(Distance(fromX, fromY, toX, toY) /
                Max(.05f, Min(shape.Width, shape.Height) * .45f)));
            host.Begin();
            for (int n = 0; n <= steps; n++)
            {
                float amount = (float)n / steps;
                if (amount < 0) amount = 0;
                else if (amount > 1) amount = 1;
                float x = fromX + (toX - fromX) * amount + offsetX;
                float y = fromY + (toY - fromY) * amount + offsetY;
                host.DebugBox(x, y, shape.Width, shape.Height);
                int count = host.QueryBox(x, y, shape.Width, shape.Height);
                for (int i = 0; i < count; i++)
                {
                    if (host.IsOwner(i) || host.IsPlayer(i) || host.IsDecoy(i) || host.IsDead(i) || host.ContainsCandidate(i)) continue;
                    float originX = effectCenter ? x : x - offsetX;
                    if (host.Exposed(i, originX, y, x, y, shape.Width, shape.Height)) host.AddCandidate(i);
                }
            }
            host.SortCandidates();
            for (int i = 0; i < host.CandidateCount; i++)
            {
                if (nearest && host.MarkedExists && !host.IsMarkedCandidate(i)) continue;
                // Collection is the eligibility boundary. Receive callbacks may
                // cancel/move/change later candidates without re-filtering them.
                host.ApplyTarget(i);
                if (nearest) break;
            }
        }

        public static float Distance(float x, float y, float targetX, float targetY)
        {
            float dx = x - targetX, dy = y - targetY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        public static int Compare(float firstDistance, int firstActorId, float secondDistance, int secondActorId)
        {
            int order = firstDistance.CompareTo(secondDistance);
            return order != 0 ? order : firstActorId.CompareTo(secondActorId);
        }
        static float Max(float a, float b) => a > b ? a : b;
        static float Min(float a, float b) => a < b ? a : b;
    }
}
