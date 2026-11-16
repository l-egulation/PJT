using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct CastMotionHost : ICastMotionHost
        {
            readonly CombatCast cast;
            public CastMotionHost(CombatCast cast) { this.cast = cast; }
            public bool HasMeleeAction => cast.meleeAction != null;
            public float MeleeHorizontalVelocity(int axis) => cast.FeelStepVelocity(cast.meleeAction.HorizontalVelocity(cast.InkNow, cast.dt, axis, cast.owner.grounded, cast.DirectMeleeSpecial != null ? 0 : cast.arena.Movement.RunSpeed));
            public bool HasChargedMelee => cast.ChargedMelee != null;
            public bool HasChargedMeleeExecution => cast.chargedMeleeExecution != null;
            public bool HasDirectMeleeSpecial => cast.DirectMeleeSpecial != null;
            public bool Cancelled => cast.CastCancelled;
            public bool MotionDone => cast.CastMotionDone;
            public bool MissileFlying => cast.MissileFlying;
            public float MissileFlightVelocity() => cast.MissileFlightVelocity();
            public bool MissileRecoiling => cast.MissileRecoiling;
            public float MissileRecoilVelocity() => cast.MissileRecoilVelocity();
            public bool Approaching => cast.Approaching;
            public bool Recoiling => cast.Recoiling;
            public float RangedRecoilVelocity() => cast.rangedRecoil.Velocity(cast.arena.Now, cast.dt, cast.owner.grounded, cast.Definition.Ranged.RecoilDistance);
            public bool Moving => cast.Moving;
            public bool ForcedDashMoving => cast.forcedDash.IsMoving(cast.CastActivated, cast.Elapsed, cast.dt);
            public bool TryForcedDashVelocity(out float velocity) => cast.forcedDash.TryHorizontalVelocity(cast.CastActivated, cast.Elapsed, cast.dt, cast.direction, out velocity);
            public bool IsBasic => cast.isBasic;
            public float Elapsed => cast.Elapsed;
            public float StepDuration => cast.dt;
            public bool OwnerGrounded => cast.owner.grounded;
            // Walking during an attack keeps the village move-speed unlock; the inks' buffs still apply only outside it.
            public float RunSpeed => cast.arena.Movement.RunSpeed * (cast.owner && cast.owner.isPlayer ? 1 + cast.arena.PermanentMoveSpeedBonus + cast.arena.AugmentWalkBonus : 1);
        }
    }
}
