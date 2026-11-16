using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class EnemyProjectileSweepExecution
    {
        public static void Sweep<T>(float duration, ref T host) where T : struct, IEnemyProjectileSweepHost
        {
            host.ReadPosition(out float previousX, out float previousY);
            float distance = host.VelocityMagnitude * duration;
            host.ReadDirection(out float directionX, out float directionY);
            float allowed = host.SolidTravel(previousX, previousY, .25f, .25f, directionX, directionY, distance);
            // SolidTravel may synchronously change the velocity. The original
            // next position uses a second, current native normalized read.
            host.ReadDirection(out directionX, out directionY);
            float nextX = previousX + directionX * allowed, nextY = previousY + directionY * allowed;
            host.BeginCandidates();
            int steps = Math.Max(1, (int)Math.Ceiling(allowed / .1f));
            for (int n = 0; n <= steps; n++)
            {
                float amount = (float)n / steps;
                if (amount < 0) amount = 0;
                else if (amount > 1) amount = 1;
                float x = previousX + (nextX - previousX) * amount;
                float y = previousY + (nextY - previousY) * amount;
                int count = host.QueryBox(x, y, .25f, .25f);
                for (int i = 0; i < count; i++)
                    if (!host.ContainsCandidate(i)) host.AddCandidate(i);
            }
            host.SortCandidates(previousX, previousY);
            for (int i = 0; i < host.CandidateCount; i++)
            {
                if (!host.AcceptsTarget(i) || host.Occluded(i, previousX, previousY)) continue;
                if (host.TryContact(i, previousX, previousY)) return;
            }
            // An already-entered sweep still finishes these writes after a
            // callback removes the owner. The execution owns the only lifetime.
            host.SetPosition(nextX, nextY);
            if (allowed < distance - .0001f) host.Release();
        }
    }
}
