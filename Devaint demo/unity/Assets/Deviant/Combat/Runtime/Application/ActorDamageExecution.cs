using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Vitals/statuses retain ownership. This flow holds only the entered hit's
    // clock and request, and observes live actor/control facts after callbacks.
    public static class ActorDamageExecution
    {
        public static bool Receive<T>(float damage, int hitDirection, bool basic, HitData sourceControl, SkillHitDefinition control,
            bool bypass, bool projectileHit, ref T host) where T : struct, IActorDamageHost
        {
            if (host.Vitals.Read().Dead) return false;
            var policy = host is IActorDamagePolicy p ? p : null;
            if (policy != null && !policy.AcceptsIncomingDamage) return false;
            float now = host.Now;
            if (!bypass && host.IsPlayer && host.PlayerInvulnerable) return false;
            if (!bypass && !projectileHit && host.IsPlayer && host.TryParryMelee()) return false;
            var before = host.Vitals.Read();
            var applied = host.Vitals.ApplyDamage(damage, now, bypass);
            if (!applied.Accepted) return false;
            if (policy != null && policy.MinimumHealthAfterDamage > 0 && applied.State.Health < policy.MinimumHealthAfterDamage)
            {
                float floor = policy.MinimumHealthAfterDamage < before.Health ? policy.MinimumHealthAfterDamage : before.Health;
                host.Vitals.WriteHealth(floor);
                applied = new ActorDamageResult(true, before.Health - floor, applied.Absorbed, host.Vitals.Read());
            }
            host.AcceptedHit(damage, before, applied, now);
            if (host.IsPlayer) host.Statuses.PlayerHit(now, hitDirection, host.IsForcedMoving);
            else
            {
                // A tuned action push (attack_feel_motion.json) gives the basic reaction to that
                // action, including a non-basic skill, and replaces the ordinary basic push.
                bool actionPush = host.SourceIsPlayer && host.ActionPushDistance > 0;
                if ((basic || actionPush) && !host.IsBoss && !host.IsDecoy)
                {
                    if (host.BasicStaggerSeconds > 0)
                    {
                        host.Statuses.BasicStagger(now, host.BasicStaggerSeconds);
                        host.CancelAttack();
                    }
                    // Cancellation may edit the source or mutable control. An
                    // explicit attack push replaces, rather than stacks with,
                    // the ordinary player-basic reaction.
                    if (host.SourceIsPlayer && (control != null ? control.Push <= 0 : sourceControl == null || sourceControl.push <= 0))
                    {
                        if (actionPush) host.Statuses.PushEased(now, hitDirection, host.LimitAutomaticPush(host.ActionPushDistance), host.ActionPushSeconds);
                        else if (host.BasicPushDistance > 0)
                        {
                            float seconds = host.BasicPushSeconds;
                            host.Statuses.SetKnockMotion(now + seconds, hitDirection * host.LimitAutomaticPush(host.BasicPushDistance) / seconds);
                        }
                    }
                }
                if ((policy == null || policy.AcceptsIncomingControl) && (control != null || sourceControl != null))
                {
                    // Do not snapshot the mutable DTO: basic/stun cancellation
                    // can edit the control values used by the following phase.
                    var gate = host is IActorControlGate g ? g : null;
                    float root = control != null ? control.Root : sourceControl.root;
                    if (root <= 0 || gate == null || gate.TryAcceptRoot(now))
                        host.Statuses.RefreshRoot(now, root);
                    if ((control != null ? control.Stun : sourceControl.stun) > 0 && (gate == null || gate.TryAcceptStun(now)))
                    {
                        host.Statuses.RefreshStun(now, control != null ? control.Stun : sourceControl.stun);
                        host.CancelAttack();
                    }
                    if ((control != null ? control.Push : sourceControl.push) > 0 && !host.IsBoss && !host.IsDecoy)
                        host.Statuses.Push(now, hitDirection, control != null ? control.Push : sourceControl.push, control != null ? control.PushSeconds : sourceControl.pushSeconds);
                }
            }
            host.RecordDamage(applied.Damage);
            if (host.Vitals.Read().Dead)
            {
                if (host.IsPlayer && host is IActorDeathInterceptor interceptor && interceptor.TryPreventDeath() && !host.Vitals.Read().Dead)
                    return true;
                host.CancelAttack();
                if (host.IsPlayer) host.DiePlayer();
                host.ApplyDeathVisual();
            }
            return true;
        }
    }
}
