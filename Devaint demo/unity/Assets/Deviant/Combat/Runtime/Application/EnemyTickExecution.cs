using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Stateless ordering only: the existing resolver, attack and actor motor
    // retain their state. In particular, physics cannot replace controlled.
    public static class EnemyTickExecution
    {
        public static void Step<T>(float step, ref T host) where T : struct, IEnemyTickHost
        {
            if (host.Dead) return;
            float horizontal = 0;
            host.CaptureTarget();
            bool controlled = host.Now < host.StunUntil || host.Now < host.StaggerUntil;
            var state = host.ReadAttack();
            var intent = EnemyIntentResolver.Resolve(new EnemyObservation(host.Profile, host.Aggressive, host.HasTarget, controlled, state.Attacking,
                host.Now, state.NextAttackAt, host.HasTarget ? host.TargetDeltaX : 0, host.HasTarget ? host.TargetDeltaY : 0));
            if (intent.Kind != EnemyIntentKind.None) horizontal = host.ApplyIntent(intent);
            host.PhysicsStep(step, horizontal, false);
            host.StepAttack(step, controlled);
        }

        public static void MeleeHit<T>(int hitIndex, ref T host) where T : struct, IEnemyMeleeHitHost
        {
            host.BeginTargets();
            try
            {
                while (host.NextTarget())
                {
                    if (host.TargetIsNotPlayer && !host.TargetIsDecoy || host.TargetDead || host.TargetOccluded) continue;
                    if (host.FirstHit(hitIndex)) host.Receive(host.Profile == 2 ? 20 : 10);
                }
            }
            finally { host.EndTargets(); }
            if (host.ShowBoxes) host.AddFlash();
        }
    }
}
