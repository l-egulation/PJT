using System;

namespace Deviant.Combat.Execution
{
    // Boss stun/root internal cooldowns (docs/design/combat/rules.md#보스의-기절-상태와-패턴).
    // A cooldown starts when its control is applied; during it the same control fails.
    // Stun and root keep separate clocks on the caller's simulation time.
    public sealed class BossControlCooldowns
    {
        public float StunReadyAt { get; private set; }
        public float RootReadyAt { get; private set; }
        public float StunSeconds { get; private set; }
        public float RootSeconds { get; private set; }
        public BossControlCooldowns(float stunSeconds, float rootSeconds) => Configure(stunSeconds, rootSeconds);
        public void Configure(float stunSeconds, float rootSeconds)
        {
            Check(stunSeconds); Check(rootSeconds);
            StunSeconds = stunSeconds; RootSeconds = rootSeconds;
        }
        // Peek without spending: callers that must decide before applying (run inks pick mounts first).
        public bool CanStun(float now) => now >= StunReadyAt;
        public bool CanRoot(float now) => now >= RootReadyAt;
        public bool TryStun(float now) { Time(now); if (now < StunReadyAt) return false; StunReadyAt = now + StunSeconds; return true; }
        public bool TryRoot(float now) { Time(now); if (now < RootReadyAt) return false; RootReadyAt = now + RootSeconds; return true; }
        public float StunRemaining(float now) => Math.Max(0, StunReadyAt - now);
        public float RootRemaining(float now) => Math.Max(0, RootReadyAt - now);
        public void Reset() => StunReadyAt = RootReadyAt = 0;
        static void Check(float seconds)
        { if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds)); }
        static void Time(float now)
        { if (float.IsNaN(now) || float.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now)); }
    }
}
