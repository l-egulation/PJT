using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Embedded privately by Cast and invoked directly, never copied or boxed.
    // Only shove membership and the primary end-hit claim belong to this owner.
    public struct CastHitResponseExecution
    {
        HashSet<int> shoved;
        bool endHitDone;
        public bool PrimaryEndClaimed => endHitDone;

        public void ApplyTarget<T>(ref T host) where T : struct, ICastHitResponseHost
        {
            if (!host.TryClaimTarget()) return;
            if (!host.ReceiveControl(host.HitDamage * host.DamageMultiplier)) return;
            if (host.TrackPrimaryTarget)
            {
                host.MarkTarget();
                host.MarkApproachAccepted();
            }
            bool primaryMovement = host.HasResponseMovement && host.PrimaryPush;
            if (primaryMovement && !host.MarkedTargetExists)
            {
                host.MarkTarget();
                if (host.TargetIsBoss)
                {
                    host.SetMotionEnd(host.EndForcedDashAtBoss());
                    EndPrimary(ref host);
                }
            }
            // EndPrimary may synchronously change the target's classification
            // or mark. Re-read both before committing the final shove member.
            if (!host.TargetIsBoss && (host.PushWithCaster || primaryMovement))
            {
                if (shoved == null) shoved = new HashSet<int>();
                shoved.Add(host.TargetActorId);
            }
        }

        public void EndPrimary<T>(ref T host) where T : struct, ICastHitResponseHost
        {
            if (endHitDone || host.Cancelled || !host.MovementExists || !host.PrimaryPush) return;
            endHitDone = true;
            if (host.MarkedTargetAlive) host.ReceiveMarked(host.EndDamage * host.DamageMultiplier);
        }

        public void PushTargets<T>(float step, ref T host) where T : struct, ICastPushHost
        {
            if (!host.Moving || host.Cancelled) return;
            host.BeginEnemies();
            try
            {
                while (host.NextEnemy())
                    if (host.EnemyExists && !host.EnemyDead && shoved != null && shoved.Contains(host.EnemyActorId))
                        host.SetEnemyKnockMotion(host.Now + step, host.MovementSpeed * host.Direction);
            }
            finally { host.EndEnemies(); }
        }
    }
}
