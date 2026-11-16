using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Embedded privately by the cast. Mutations use the field directly and a
    // constrained ref host; only the immutable Read snapshot is copied.
    public struct ApproachRecastExecution
    {
        bool consumed, mode;
        float markedUntil, budget, duration;
        public float VisualDuration => Max(.02f, duration);
        public float DeadlineFrom(float startedAt) => markedUntil - startedAt;
        public ApproachRecastSnapshot Read() => new ApproachRecastSnapshot(consumed, mode, markedUntil, budget, duration);
        public bool IsApproaching<T>(ref T host) where T : struct, IApproachRecastHost
            => mode && !host.Cancelled && !host.MotionDone;
        public float Remaining<T>(ref T host) where T : struct, IApproachRecastHost
            => !host.Cancelled && !consumed && host.TargetHit && host.MarkedTargetAlive && host.IsRecastMovement ? Max(0, markedUntil - host.Now) : 0;
        public void MarkAccepted<T>(ref T host) where T : struct, IApproachRecastHost
        {
            if (host.IsRecastMovement) markedUntil = host.Now + host.RecastWindow;
        }
        public bool TryStart<T>(ref T host) where T : struct, IApproachRecastHost
        {
            if (!host.SameWeapon || Remaining(ref host) <= 0) return false;
            // Destination precedes the claim. The player callback can cancel
            // this same cast; only after it returns are motion facts resumed.
            host.CaptureDestination();
            consumed = true;
            host.StartApproach();
            host.ResumeMotion();
            mode = true;
            budget = host.Distance;
            // Zero is an authored unlimited approach, not a zero-length dash.
            if (budget == 0) budget = float.PositiveInfinity;
            duration = Min(budget, host.DistanceToDestination) / host.Speed;
            host.SetMotionEnd(host.Elapsed + Max(.02f, duration));
            host.ClearVerticalSpeed();
            host.RecordApproach();
            return true;
        }
        public void Advance<T>(float step, ref T host) where T : struct, IApproachRecastHost
        {
            if (!host.MarkedTargetAlive) { mode = false; host.SetMotionEnd(host.Elapsed); return; }
            host.CaptureDestination();
            host.CaptureOrigin();
            float distance = Min(host.Speed * step, budget);
            host.MoveTowardDestination(distance);
            float moved = host.MovedDistance; budget -= moved;
            if (host.DistanceToDestination < .005f || moved + .001f < host.RequestedDistance || budget <= .001f)
            { mode = false; host.SetMotionEnd(host.Elapsed + step); }
            else if (float.IsPositiveInfinity(budget))
                host.SetMotionEnd(host.Elapsed + step + Max(.02f, host.DistanceToDestination / host.Speed));
        }
        public void Forget() => consumed = true;
        static float Max(float a, float b) => a > b ? a : b;
        static float Min(float a, float b) => a < b ? a : b;
    }
}
