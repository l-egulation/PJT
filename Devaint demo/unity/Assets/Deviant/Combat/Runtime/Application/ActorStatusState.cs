using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Absolute deadlines share the caller's simulation clock. Reads do not
    // expire/reset values, accumulate time, or invoke attack callbacks.
    public sealed class ActorStatusState : IActorStatuses
    {
        float stun, root, stagger, inputLock, knock, knockSpeed, stealth, smoke;
        // Eased push curve; any other knock write replaces it.
        bool eased; float easeStart, easeSeconds, easeDistance;
        public ActorStatusSnapshot Read() => new ActorStatusSnapshot(stun, root, stagger, inputLock, knock, knockSpeed, stealth, smoke);
        public void WriteStunUntil(float value) { Check(value); stun = value; }
        public void WriteRootUntil(float value) { Check(value); root = value; }
        public void WriteStaggerUntil(float value) { Check(value); stagger = value; }
        public void WriteInputLockUntil(float value) { Check(value); inputLock = value; }
        public void WriteKnockUntil(float value) { Check(value); knock = value; eased = false; }
        public void WriteKnockSpeed(float value) { Check(value); knockSpeed = value; eased = false; }
        public void WriteStealthUntil(float value) { Check(value); stealth = value; }
        public void WriteSmokeUntil(float value) { Check(value); smoke = value; }
        public void PlayerHit(float now, int direction, bool forcedMovement)
        {
            Time(now); float nextLock = now + .12f; Check(nextLock);
            bool applyKnock = !forcedMovement && now >= knock;
            float end = now + .1f, speed = direction * 5; Check(end); Check(speed);
            inputLock = nextLock;
            if (applyKnock) { knock = end; knockSpeed = speed; eased = false; }
        }
        public void BasicStagger(float now, float duration) { stagger = Refresh(stagger, now, duration); }
        public void RefreshRoot(float now, float duration) { root = Refresh(root, now, duration); }
        public void RefreshStun(float now, float duration) { stun = Refresh(stun, now, duration); }
        public void Push(float now, int direction, float distance, float seconds)
        {
            Time(now); Check(distance); Check(seconds);
            SetKnockMotion(now + seconds, direction * distance / Math.Max(.01f, seconds));
        }
        public void PushEased(float now, int direction, float distance, float seconds)
        {
            Push(now, direction, distance, seconds);
            eased = true; easeStart = now; easeSeconds = Math.Max(.01f, seconds); easeDistance = direction * distance;
        }
        public float KnockSpeedAt(float now, float step)
        {
            Check(now); Check(step);
            float span = Math.Min(step, knock - now);
            if (!eased || span <= 0) return knockSpeed;
            return easeDistance * (EaseOut((now + span - easeStart) / easeSeconds) - EaseOut((now - easeStart) / easeSeconds)) / span;
        }
        static float EaseOut(float k) { k = Math.Max(0, Math.Min(1, k)); float r = 1 - k; return 1 - r * r * r; }
        public void SetKnockMotion(float until, float speed)
        { Check(until); Check(speed); knock = until; knockSpeed = speed; eased = false; }
        public void ClearHitMotionLocks() { inputLock = knock = 0; eased = false; }
        public void ClearConcealment() { stealth = smoke = 0; }
        public void Reset()
        {
            stun = root = stagger = inputLock = knock = stealth = smoke = 0; eased = false;
            // Preserve the legacy inactive speed value; the zero deadline means
            // it cannot become deferred knockback after reset/return.
        }
        static float Refresh(float previous, float now, float duration)
        { Time(now); Check(duration); float end = now + duration; Check(end); return Math.Max(previous, end); }
        static void Time(float now) { Check(now); if (now < 0) throw new ArgumentOutOfRangeException(nameof(now)); }
        static void Check(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "Status values must be finite."); }
    }
}
