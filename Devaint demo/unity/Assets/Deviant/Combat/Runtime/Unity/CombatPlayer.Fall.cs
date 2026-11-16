using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        readonly struct PlayerFallHost : IPlayerFallRecoveryHost
        {
            readonly CombatPlayer player;
            readonly Vector2? destination;
            public PlayerFallHost(CombatPlayer player) { this.player = player; destination=player.FallDestination?.Invoke(); }
            public void CancelAction() => player.CancelAction();
            public void ClearBufferedJump() => player.ClearBufferedJump(CombatCommandReason.Interrupted);
            public float Health { get => player.health; set => player.health = value; }
            public float MaxHealth => player.maxHealth;
            public bool Dead => player.Dead;
            public void Die() => player.Die();
            public float SpawnX => destination?.x ?? player.spawn.x;
            public float SpawnY => destination?.y ?? player.spawn.y;
            public float Now => player.arena.Now;
            public float LastFallTime { get => player.LastFallTime; set => player.LastFallTime = value; }
            public float LastRestoreTime { get => player.LastRestoreTime; set => player.LastRestoreTime = value; }
            public int Facing => player.facing;
            public bool TrySafeBefore(float cutoff, out SafeGroundSample sample)
            { if(destination.HasValue){sample=default;return false;}return player.floors.TrySafeBefore(cutoff, player.RecoveryQuery, out sample); }
            public void RecordSample(bool found, SafeGroundSample sample) => player.arena.Record("safe floor found=" + found + " at=" + sample.Time + " xy=" + sample.X + "," + sample.Y);
            public void Place(float x, float y) => player.Position = new Vector2(x, y);
            public void SyncFacing(int facing) => player.SyncFacing(facing);
            public void ClearVelocity() => player.velocity = Vector2.zero;
            public void ResetJumps() => player.jumps.Reset();
            public void SetProtectionUntil(float deadline) => player.respawnProtect = deadline;
            public void SetLastRestorePosition(float x, float y) => player.LastRestorePosition = new Vector2(x, y);
            public void ClearHadGround() => player.tick.HadGround = false;
            public void ClearHitMotionLocks() => player.ClearHitMotionLocks();
            public void RecordRestored(float x, float y)
            {
                var restore = new Vector2(x, y);
                player.arena.Record("fall at=" + player.LastFallTime.ToString("0.00") + " cutoff=" + (player.LastFallTime - 3).ToString("0.00") + " sample=" +
                    (player.LastRestoreTime < 0 ? "room entry" : player.LastRestoreTime.ToString("0.00")) + " restore=" + restore + " -10HP");
            }
        }
    }
}
