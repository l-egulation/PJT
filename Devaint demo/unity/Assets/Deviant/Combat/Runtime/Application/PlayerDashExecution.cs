using System;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // One owner for basic dash/carry state. The adapter retains attack cancel,
    // facing correction, jump admission and native motor/contact ordering.
    public sealed class PlayerDashExecution
    {
        float startedAt = -10, endsAt = -10, readyAt, jumpCarryUntil;
        int facing, jumpCarryFacing, used, maxCharges = 1;
        // Dash charges share one recovery: the first dash of a full set starts it, and when it ends every charge is
        // back at once, however many were spent (village extra-dash unlock, user decision 2026-11-05). With one charge
        // this is the original dash: spent at its start, ready again DashRecovery later.
        public int MaxCharges
        {
            get => maxCharges;
            set { if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "A dash holds at least one charge."); maxCharges = value; }
        }
        public int Available(float now) => now < readyAt ? Math.Max(0, maxCharges - used) : maxCharges;
        public PlayerDashSnapshot Read() => new PlayerDashSnapshot(startedAt, endsAt, readyAt, facing, jumpCarryUntil, jumpCarryFacing);
        public bool IsDashing(float now) => now < endsAt;
        public bool IsInvulnerable(float now, LocomotionProfile movement) => IsDashing(now) && now < startedAt + movement.DashInvulnerability;
        public float Remaining(float now)
        {
            if (Available(now) > 0) return 0;
            float remaining = readyAt - now;
            return 0 > remaining ? 0 : remaining;
        }
        public bool CanStart(float now) => !IsDashing(now) && Available(now) > 0;
        // Admission is checked before the adapter cancels its current action.
        // Begin receives the final facing after the native SyncFacing correction.
        public void Begin(float now, int correctedFacing, LocomotionProfile movement)
        {
            facing = correctedFacing;
            startedAt = now; endsAt = now + movement.DashDuration;
            if (!(now < readyAt)) { used = 0; readyAt = now + movement.DashRecovery; }
            used++;
            jumpCarryUntil = 0;
        }
        // Called only after a valid jump has cancelled the dash. Cancel retains
        // its facing so the carry follows the dash, not the current input axis.
        public void BeginJumpCarry(float now, LocomotionProfile movement)
        { jumpCarryFacing = facing; jumpCarryUntil = now + movement.DashJumpCarryDuration; }
        public void ClearCarry() { jumpCarryUntil = 0; }
        // A shaped curve returns the average speed over [now, now + step], so the
        // fixed-step sum lands on DashSpeed * DashDuration whatever the curve.
        public void ApplyMotion(float now, LocomotionProfile movement, ref float horizontal, ref bool freezeVertical, float step = 0)
        {
            if (IsDashing(now)) { horizontal = facing * CurveSpeed(now, movement, step); freezeVertical = true; }
            else if (now < jumpCarryUntil) horizontal = jumpCarryFacing * movement.DashJumpCarrySpeed;
        }
        float CurveSpeed(float now, LocomotionProfile movement, float step)
        {
            if (movement.DashConstantSpeed) return movement.DashSpeed;
            if (!(step > 0)) return movement.DashSpeedAt((now - startedAt) / movement.DashDuration);
            float duration = movement.DashDuration;
            float from = movement.DashDistanceFraction((now - startedAt) / duration);
            float to = movement.DashDistanceFraction((now + step - startedAt) / duration);
            return (to - from) * movement.ExpectedDashDistance / step;
        }
        // Contacts are evaluated after the unchanged full native movement tick;
        // a true result tells the adapter to clear vertical speed immediately.
        public bool EndAtWall(float now, bool wallHit)
        {
            if (!IsDashing(now) || !wallHit) return false;
            endsAt = now; return true;
        }
        public void Cancel(float now) { endsAt = now; }
        public void Reset()
        {
            startedAt = endsAt = -10; readyAt = jumpCarryUntil = 0; used = 0;
        }
    }
}
