using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct CastLifecycleHost : ICastLifecycleHost
        {
            readonly CombatCast cast;
            public CastLifecycleHost(CombatCast cast) { this.cast = cast; }
            public float Now => cast.InkNow;
            public float Elapsed => cast.Elapsed;
            public bool ChargeHeld => cast.owner.isPlayer && cast.arena.Player.specialHeld;
            public bool Approaching => cast.Approaching;
            public void AdvanceApproach(float step) => cast.AdvanceApproach(step);
            public void Activate() => cast.Activate();
            public void PrepareMissile(float step) => cast.PrepareMissileStep(step);
            public void ReleaseDueBombs() => cast.ReleaseDueBombs();
            public void PrepareTeleport(float step)
            {
                var host = new CastTeleportHost(cast); cast.teleport.Prepare(step, ref host);
            }
            public void ReleaseResources() => cast.ReleaseResources();
            public bool CancelMeleeAction()
            {
                if (cast.meleeAction == null) return false;
                cast.meleeAction.Cancel(); return true;
            }
            public void Failed() { cast.FailCommand(); cast.arena.Record("not activated: " + cast.Definition.Id); }
            public bool MissileEnded => cast.MissileEnded;
            public void Complete() { if (cast.owner.isPlayer) cast.arena.Player.AttackFinished(cast); }
            public float SmokeEnd => cast.smokeEffect.End;
            public float RetainSkillWaveUntil(float last, float elapsed, float step) => cast.skillWave.RetainUntil(last, elapsed, step);
            public float RecastRemaining => cast.RecastRemaining;
            public float RecastDeadline => cast.approachRecast.DeadlineFrom(cast.started);
            public void Dispose() => cast.Dispose();
        }
    }
}
