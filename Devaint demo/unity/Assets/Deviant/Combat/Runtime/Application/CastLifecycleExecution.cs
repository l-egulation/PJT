using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embed privately and invoke by reference: callbacks may reenter this same
    // owner. Charge, resources, movement policies and the clock stay with their
    // existing owners; releasing resources alone creates no terminal state.
    public struct CastLifecycleExecution
    {
        public bool Activated { get; private set; }
        public bool Cancelled { get; private set; }
        public bool MotionDone { get; private set; }
        public float Activation { get; private set; }
        public float MotionEnd { get; private set; }
        public CastLifecycleExecution(float activation, float motionEnd)
        {
            Activated = Cancelled = MotionDone = false;
            Activation = activation; MotionEnd = motionEnd;
        }
        // Native admission and resource commit precede the activation claim;
        // effects and their callbacks follow it without another terminal guard.
        public void ClaimActivation() => Activated = true;
        public void SetMotionEnd(float elapsed) => MotionEnd = elapsed;
        public void ResumeMotion() { Cancelled = false; MotionDone = false; }
        public void BeforeMovement<T>(float step, SkillExecutionDefinition definition, ChargeExecution charge, ref T host) where T : struct, ICastLifecycleHost
        {
            if (Cancelled || MotionDone) return;
            if (host.Approaching) { host.AdvanceApproach(step); return; }
            if (charge != null && !charge.Release.HasValue)
            {
                if (!charge.TryRelease(host.Now, step, host.ChargeHeld)) return;
                Activation = charge.Release.Value.ReleaseTime;
                MotionEnd = charge.Release.Value.MotionEnd;
            }
            if (!Activated && (definition.Kind == "bomb" || definition.Kind == "ranged" || definition.Kind == "missile" ? host.Elapsed + .000001f >= Activation : host.Elapsed + step >= Activation)) host.Activate();
            if (Cancelled || !Activated) return;
            if (definition.Kind == "missile") host.PrepareMissile(step);
            if (definition.Kind == "bomb") host.ReleaseDueBombs();
            host.PrepareTeleport(step);
        }
        public void Fail<T>(ref T host) where T : struct, ICastLifecycleHost
        {
            host.ReleaseResources();
            Cancelled = true;
            MotionEnd = host.Elapsed + .1f;
            host.Failed();
        }
        // False preserves the melee runner's existing early return, including
        // skipping the generic missile/vertical cleanup in Native Cancel.
        public bool Cancel<T>(ref T host) where T : struct, ICastLifecycleHost
        {
            host.ReleaseResources();
            if (host.CancelMeleeAction()) return false;
            Cancelled = true; MotionDone = true;
            return true;
        }
        // Called after the hit owner's final evaluation commit. Read live time
        // after hits, claim completion before its callback, then reread all tail
        // facts. Reentry does not roll back or short-circuit an entered callback.
        public void AfterHits<T>(float step, SkillExecutionDefinition definition, ChargeExecution charge, ref T host) where T : struct, ICastLifecycleHost
        {
            bool releaseReady = Cancelled || (definition.Kind != "ranged" && definition.Kind != "bomb") || Activated;
            if (definition.Kind == "missile") releaseReady = Cancelled || host.MissileEnded;
            if (!MotionDone && releaseReady && (charge == null || charge.Release.HasValue || Cancelled) && host.Elapsed + step >= MotionEnd)
            {
                MotionDone = true;
                host.Complete();
            }
            float last = MotionEnd;
            if (Activated)
            {
                for (int i = 0; i < definition.Hits.Count; i++)
                {
                    var hit = definition.Hits[i];
                    if (hit.TrackAnchor.HasValue ? hit.TrackAnchor.Value != ActionHitAnchor.Actor : hit.Anchor != "actor") last = Max(last, HitTime(hit, charge) + hit.Active);
                }
                last = Max(last, host.SmokeEnd);
                if (definition.HasProjectileWave) last = host.RetainSkillWaveUntil(last, host.Elapsed, step);
                if (host.RecastRemaining > 0) last = Max(last, host.RecastDeadline);
            }
            if (MotionDone && host.Elapsed + step >= last) host.Dispose();
        }
        static float HitTime(SkillHitDefinition hit, ChargeExecution charge) => hit.Time + (charge?.Release?.Shift ?? 0);
        static float Max(float a, float b) => a > b ? a : b;
    }
}
