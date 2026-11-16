using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct CastActivationHost : ICastActivationHost
        {
            readonly CombatCast cast;
            public CastActivationHost(CombatCast cast) { this.cast = cast; }
            public bool ProbeGround() => cast.owner.ProbeGround();
            public void SelectTeleportTargetInCircle(float radius) => cast.marked = cast.arena.NearestInCircle(cast.owner, radius,
                cast.Definition.Movement.WallPolicy == "teleport_endpoint_clear");
            public void SelectTeleportTargetInBox() => cast.marked = cast.arena.NearestInBox(cast.owner, cast.Definition.Movement.TargetBox);
            public void SelectFirstHitTarget() => cast.marked = cast.arena.NearestInBox(cast.owner, cast.Definition.Hits[0].Box);
            public bool MarkedTargetExists => cast.marked;
            public void SaveOwnerPose() => cast.savedPose = cast.owner.Position;
            public void CaptureInstallCenter(float x, float y) => cast.installCenter = cast.owner.Position + new Vector2(x * cast.direction, y);
            public float? FindInstallFloor(float distance) => cast.arena.FindFloor(cast.installCenter, distance);
            public void SetInstallHeight(float height) => cast.installCenter = new Vector2(cast.installCenter.x, height);
            public bool InstallationOverlapsSolid(float width, float height, float inset)
            {
                var size = new Vector2(width, height) - Vector2.one * inset;
                // An ordinary supporting floor touches the lower edge of the
                // throw box. Require interior terrain overlap for this skill.
                return (cast.Definition.Id == "greatsword_follow_up" || cast.Definition.Id == "shield_follow_up")
                    ? cast.arena.SkillProjectileBlocked(cast.installCenter, size)
                    : cast.arena.SolidOverlap(cast.installCenter, size);
            }
            public bool InstallationOccluded => cast.arena.Occluded(cast.owner.Center, cast.installCenter);
            public bool CommitResources() => cast.CommitResources();
            public void Fail() => cast.Fail();
            public void ClaimActivation() => cast.lifecycle.ClaimActivation();
            public void LaunchMissile() => cast.LaunchMissile();
            public void ReleaseDueBombs() => cast.ReleaseDueBombs();
            public void FireRanged()
            {
                var launch = cast.Definition.Ranged; float damage = cast.Definition.Hits[0].Damage;
                // A charged shot fires with the damage and size reached by the held release.
                var release = cast.charge?.Release;
                if (release.HasValue && launch.ChargeRadius > 0)
                {
                    var policy = cast.charge.Definition; damage = release.Value.Damage;
                    float amount = policy.MaximumDamage > policy.MinimumDamage ? (damage - policy.MinimumDamage) / (policy.MaximumDamage - policy.MinimumDamage) : 0;
                    launch = launch.WithChargeAmount(amount);
                }
                cast.arena.FirePlayerRanged(cast.owner, launch, damage * cast.damageMultiplier, cast.direction, cast.weapon, cast.HitOrigin(0, CombatHitKind.Projectile));
            }
            public bool OwnerGrounded => cast.owner.grounded;
            public float BeginRangedRecoil(float duration, float hopHeight) => cast.rangedRecoil.Begin(cast.arena.Now, cast.direction, duration, hopHeight, cast.arena.Movement.RiseGravity, cast.arena.Movement.FallGravity);
            public void SetOwnerVerticalVelocity(float velocity) => cast.owner.VelocityY = velocity;
            public void ActivateSkillWave() => cast.ActivateSkillWave();
            public float Now => cast.arena.Now;
            public void SetStealthUntil(float until) => cast.owner.stealthUntil = until;
            public void SetShield(float hp) => cast.owner.shield = hp;
            public float GuardUntil => cast.owner.guardUntil;
            public void SetGuardUntil(float until) => cast.owner.guardUntil = until;
            public void SetGuardReduction(float reduction) => cast.owner.guardReduction = reduction;
            public void ReplaceSmoke() => cast.arena.ReplaceSmoke(cast);
            public float Activation => cast.lifecycle.Activation;
            public void ActivateSmoke(float activation) => cast.smokeEffect.Activate(activation);
            public void SpawnDecoy(float centerToFeet) => cast.arena.SpawnDecoy(cast.owner, cast.installCenter - Vector2.up * centerToFeet, cast.Definition.Effect, cast.DefinitionVersion, cast.Definition.Authored != null);
            public void RecordActivation()
            {
                cast.arena.Record("activate " + cast.Definition.Id);
                DaggerSkillFx.Activated(cast.arena, cast, cast.owner, cast.Definition.Id, cast.savedPose, cast.direction, cast.lifecycle.Activation, cast.MotionDuration, cast.Definition.Effect?.Duration ?? 0, cast.Definition.Effect?.Box?.Width ?? 0);
            }
        }
    }
}
