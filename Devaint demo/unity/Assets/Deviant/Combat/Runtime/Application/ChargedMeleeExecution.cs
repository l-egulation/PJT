using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Owns the charged single-hit lifecycle, not a second charge policy/clock.
    // Native callback exceptions and entered-callback cancellation keep their
    // existing partial effects; there is no automatic rollback or retry policy.
    public sealed class ChargedMeleeExecution
    {
        readonly ChargeExecution charge;
        readonly float hitDuration;
        readonly IChargedMeleeExecutionHost host;
        readonly HashSet<int> targets = new HashSet<int>();
        float lastEvaluated = -.000001f;
        float? failureEnd;
        public bool Activated { get; private set; }
        public bool Cancelled { get; private set; }
        public bool MotionDone { get; private set; }
        public float ImpactTime => charge.Definition.Minimum + (charge.Release?.Shift ?? 0) + charge.Definition.ReleaseWindup;
        float MotionEnd => failureEnd ?? charge.Release?.MotionEnd ?? charge.Definition.BaseEnd;
        public float EndTime => charge.StartedAt + MotionEnd;
        float Elapsed => host.Now - charge.StartedAt;

        public ChargedMeleeExecution(ChargeExecution charge, float hitDuration, IChargedMeleeExecutionHost host)
        {
            this.charge = charge ?? throw new ArgumentNullException(nameof(charge));
            if (float.IsNaN(hitDuration) || float.IsInfinity(hitDuration) || hitDuration <= 0) throw new ArgumentOutOfRangeException(nameof(hitDuration));
            this.hitDuration = hitDuration;
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }
        public void BeforeMovement(float step, bool held)
        {
            if (Cancelled || MotionDone) return;
            if (!charge.Release.HasValue && !charge.TryRelease(host.Now, step, held)) return;
            if (!Activated && Elapsed + step >= charge.Release.Value.ReleaseTime)
            {
                // No target at release still swings. The active sweep hits every
                // exposed enemy in range, including enemies entering afterward.
                host.SelectTarget();
                if (!host.CommitResources()) { Fail(); return; }
                Activated = true;
                host.Activated();
            }
        }
        public void Step(float step)
        {
            if (Activated && !Cancelled && !MotionDone && IntersectsTime(lastEvaluated, Elapsed + step, ImpactTime, hitDuration))
                host.Sweep();
            lastEvaluated = Elapsed + step;
            // Re-read the live clock after callbacks and compare the addition
            // directly, rather than reusing the rounded sweep observation.
            if (!MotionDone && (charge.Release.HasValue || Cancelled) && Elapsed + step >= MotionEnd)
            {
                MotionDone = true;
                host.Complete();
            }
            if (MotionDone && Elapsed + step >= MotionEnd) host.Dispose();
        }
        // The old ledger claims before Receive and never retracts a claim when
        // Receive rejects/throws, or cancellation occurs inside an entered sweep.
        public bool TryClaimTarget(int targetId) => targets.Add(targetId);
        public bool HasClaimedTarget(int targetId) => targets.Contains(targetId);
        public void Fail()
        {
            ReleaseResources();
            Cancelled = true;
            failureEnd = Elapsed + .1f;
            host.Failed();
        }
        public void Cancel()
        {
            ReleaseResources();
            Cancelled = true;
            MotionDone = true;
        }
        public void ReleaseResources()
        {
            host.ReleaseResources();
            charge.Cancel();
        }
        static bool IntersectsTime(float previous, float now, float start, float duration)
            => now >= start && previous < start + duration;
    }
}
