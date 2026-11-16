using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        struct PlayerTickHost : IPlayerTickHost
        {
            readonly CombatPlayer player;
            List<BoxCollider2D>.Enumerator pits;
            public PlayerTickHost(CombatPlayer player) { this.player = player; pits = default; }
            public bool InputBlocked => player.InputBlocked;
            public void ClearInputState() => player.ClearInputState();
            // WeaponLoadoutView is the stable occupied pair; indexing avoids its
            // interface enumerator allocation without capturing a tick clock.
            public int WeaponCount => player.weapons.Count;
            public void TickWeaponCooldown(int index) => player.weapons[index].Cooldown.Tick(player.arena.Now);
            public bool Dead => player.Dead;
            public void ResolveShurikenReturns() => player.ResolveShurikenReturns();
            public CombatInput CurrentInput => player.InputIngress.ReadForArbitration();
            public void EvaluateActions(CombatInput input) => player.actionArbiter.Evaluate(input, player.InputIngress.Origins);
            public void ApplyJumpRelease(CombatInput input) => player.ApplyJumpRelease(input);
            public void ClearPressed() => player.ClearInputs();
            public bool Dashing => player.Dashing;
            public int Axis => player.axis;
            public float RunSpeed => player.arena.Movement.RunSpeed * player.arena.InkMovementScale(player);
            public float VelocityX => player.VelocityX;
            public float GroundAccelerationSeconds => player.arena.Movement.GroundAccelerationSeconds;
            public float GroundDecelerationSeconds => player.arena.Movement.GroundDecelerationSeconds;
            public float AirAccelerationSeconds => player.arena.Movement.AirAccelerationSeconds;
            public float AirDecelerationSeconds => player.arena.Movement.AirDecelerationSeconds;
            public bool HasCurrentCast => player.currentCast;
            public bool CastIsBasic => player.currentCast.isBasic;
            public void SyncFacing(int direction) => player.SyncWallFacing(direction);
            public void FollowBasicFacing() => player.currentCast.FollowBasicFacing(player.facing);
            public void BeforeCastMovement(float step) => player.currentCast.BeforeMovement(step);
            public float CastHorizontalVelocity(int axis) => player.currentCast.HorizontalVelocity(axis);
            public bool CastFreezeVertical => player.currentCast.FreezeVertical;
            public bool CastHasDirectMeleeSpecial => player.currentCast.DirectMeleeSpecial != null;
            public bool CastHasChargePolicy => player.currentCast.ChargePolicy != null;
            public bool Grounded => player.grounded;
            public string CastVerticalPolicy => player.currentCast.VerticalPolicy;
            public bool CastRecoiling => player.currentCast.Recoiling;
            public bool CastApproaching => player.currentCast.Approaching;
            public bool CastMissileFlying => player.currentCast.MissileFlying;
            public void ApplyDashMotion(float step, ref float horizontal, ref bool freezeVertical)
                => player.dash.ApplyMotion(player.arena.Now, player.arena.Movement, ref horizontal, ref freezeVertical, step);
            public void PhysicsStep(float step, float horizontal, bool freezeVertical)
            {
                player.arena.BeginInkMovement();
                player.WallPhysicsStep(step,horizontal,freezeVertical);
                player.arena.EndInkMovement();
            }
            public bool EndDashAtWall() => player.dash.EndAtWall(player.arena.Now, player.wallHit);
            public void ClearVerticalVelocity() => player.VelocityY = 0;
            public void AfterCastMovement() => player.currentCast.AfterMovement();
            public void Landed() => player.Landed();
            public bool ProbeSafeFloor(out float safeY) => player.ProbeSafeFloor(out safeY);
            public bool RecoveryRecordingSafe() => player.RecoveryRecordingSafe();
            public void RecordSafeFloor(float safeY) => player.floors.Record(player.arena.Now, player.Position.x, safeY, player.facing);
            public void BeginPits() => pits = player.arena.pits.GetEnumerator();
            public bool NextPit() => pits.MoveNext();
            public bool CurrentPitIntersects() => pits.Current.bounds.Intersects(new Bounds(player.Center, player.size));
            public void EndPits() => pits.Dispose();
            public void Fall() => player.Fall();
            public void AfterMovementActions() => player.actionArbiter.AfterMovement();
        }
    }
}
