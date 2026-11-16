using System;
using System.Collections.Generic;

namespace Deviant.Chapter2.RoseBlue
{
    // Host reports actual hazard expiration. Exhausting the cue schedule alone is not enough
    // to start rest: a previously fired projectile can still deal damage after the final cue.
    public sealed class AttackRotation
    {
        readonly Random random;
        readonly HashSet<long> damagingAttacks = new HashSet<long>();
        Phase phase;
        Pattern? previous;
        double restRemaining;
        bool casting;
        long nextId;
        public bool CanMove => phase == Phase.FusedBlue && !casting && damagingAttacks.Count == 0 && restRemaining > 0;
        public double RestRemaining => restRemaining;
        public AttackRotation(int seed) { random = new Random(seed); }
        public void SetPhase(Phase next)
        {
            if (next != Phase.Machine && next != Phase.Duo && next != Phase.FusedBlue)
                throw new ArgumentException("Combat phase required");
            phase = next; previous = null; damagingAttacks.Clear(); restRemaining = 0; casting = false;
        }
        public Pattern? PickNext()
        {
            if (casting || damagingAttacks.Count > 0 || restRemaining > 1e-9) return null;
            Pattern[] pool = phase == Phase.Machine ? new[] { Pattern.LayerShots, Pattern.TrapSnipe, Pattern.MachineAndPlatforms, Pattern.SequenceBlasts }
                : phase == Phase.Duo ? new[] { Pattern.StunAndInk, Pattern.RootAndShots, Pattern.Exchange, Pattern.Swap }
                : phase == Phase.FusedBlue ? new[] { Pattern.Homing, Pattern.Bounce, Pattern.ArrowRain, Pattern.ExpandingFan }
                : throw new InvalidOperationException("Set combat phase first");
            var available = new List<Pattern>(); foreach (var p in pool) if (p != previous) available.Add(p);
            return available[random.Next(available.Count)];
        }
        public long Begin(Pattern pattern)
        {
            if (casting) throw new InvalidOperationException("Caster is already busy");
            previous = pattern; casting = true; restRemaining = 0;
            long id = ++nextId; damagingAttacks.Add(id); return id;
        }
        public void CastingFinished() { casting = false; TryStartRest(); }
        public void DamageEnded(long attackId)
        {
            if (!damagingAttacks.Remove(attackId)) return;
            TryStartRest();
        }
        public void HomingRemoved(long attackId, bool hitBlue)
        {
            if (hitBlue) InterruptCasting();
            DamageEnded(attackId);
        }
        public void InterruptCasting()
        {
            casting = false;
            // Keep IDs for spawned hazards. The host closes IDs that never spawned any attack.
            // Resume-after-stun timing remains a host decision, not an invented duration here.
        }
        public void Advance(double dt)
        {
            Encounter.CheckAmount(dt);
            restRemaining = Math.Max(0, restRemaining - dt);
        }
        void TryStartRest()
        {
            if (!casting && damagingAttacks.Count == 0)
                restRemaining = phase == Phase.Machine ? Tuning.Phase1Rest
                    : phase == Phase.FusedBlue ? Tuning.Rest : Tuning.EarlyPhaseRest;
        }
    }
    public sealed class RestMovement
    {
        double remaining;
        int direction;
        public void Begin(double bossX, double playerX)
        {
            direction = bossX < playerX ? -1 : bossX > playerX ? 1 : 0;
            remaining = Tuning.FusedMaxMoveDistance;
        }
        // Bounds include the actor's body half-width. Stops at the wall without reversing.
        public double Step(double bossX, double dt, double minimumX, double maximumX)
        {
            Encounter.CheckAmount(dt);
            if (!Finite(bossX) || !Finite(minimumX) || !Finite(maximumX) || maximumX < minimumX ||
                bossX < minimumX && bossX != (double)(float)minimumX ||
                bossX > maximumX && bossX != (double)(float)maximumX)
                throw new ArgumentException("Invalid movement bounds");
            // Unity stores the returned position as float. A wall endpoint can
            // round just outside its double bound; normalize that exact round-trip
            // before moving, without accepting arbitrary out-of-arena positions.
            bossX = Math.Max(minimumX, Math.Min(maximumX, bossX));
            double step = Math.Min(remaining, Tuning.FusedMoveSpeed * dt);
            double next = Math.Max(minimumX, Math.Min(maximumX, bossX + direction * step));
            remaining -= Math.Abs(next - bossX);
            return next;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
