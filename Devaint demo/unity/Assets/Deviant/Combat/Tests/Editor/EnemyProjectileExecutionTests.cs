using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EnemyProjectileExecutionTests
    {
        sealed class Target
        {
            public int Id;
            public bool Parry, RejectDamage;
            public float Health = 100, LastDamage;
            public int Hits;
        }
        sealed class Projectile : IEnemyProjectileExecutionHost
        {
            public readonly EnemyProjectileExecution Owner;
            public float Now { get; set; }
            public bool SourceExists { get; set; } = true;
            public bool SourceDead { get; set; }
            public bool SourceIsPlayer { get; set; }
            public float Position = 5, Velocity = -8, Bonus = .4f;
            public int Source = 10, Removals;
            public readonly List<float> Sweeps = new List<float>();
            public readonly List<string> Events = new List<string>();
            public Action DuringSweep, DuringParry, DuringFlip, DuringBonus, DuringRestore, DuringReceive, DuringRemove;
            public Projectile(float damage = 8, float expires = 3) { Owner = new EnemyProjectileExecution(damage, expires); }
            public void Step(float duration) => Owner.Step(duration, this);
            public void Sweep(float duration) { Events.Add("sweep"); Sweeps.Add(duration); DuringSweep?.Invoke(); }
            public void Remove() { Events.Add("remove"); Removals++; DuringRemove?.Invoke(); }
            public bool Contact(Target target, bool player = true)
            {
                var contact = new ContactHost(this, target, Position);
                return Owner.TryContact(target.Id, player, ref contact);
            }
        }
        readonly struct ContactHost : IEnemyProjectileContactHost
        {
            readonly Projectile projectile;
            readonly Target target;
            readonly float origin;
            public ContactHost(Projectile projectile, Target target, float origin)
            { this.projectile = projectile; this.target = target; this.origin = origin; }
            public bool TryParry()
            { projectile.Events.Add("parry"); projectile.DuringParry?.Invoke(); return target.Parry; }
            public void ReflectSourceAndVelocity()
            {
                projectile.Events.Add("flip"); projectile.Source = target.Id; projectile.Velocity = -projectile.Velocity;
                projectile.DuringFlip?.Invoke();
            }
            public float SpecialBonus
            {
                get { projectile.Events.Add("bonus"); projectile.DuringBonus?.Invoke(); return projectile.Bonus; }
            }
            public void RestoreOrigin()
            { projectile.Events.Add("restore"); projectile.Position = origin; projectile.DuringRestore?.Invoke(); }
            public void Receive(float damage)
            {
                projectile.Events.Add("receive"); target.LastDamage = damage;
                if (!target.RejectDamage) { target.Health -= damage; target.Hits++; }
                projectile.DuringReceive?.Invoke();
            }
            public void Remove() => projectile.Remove();
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static float After(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) + 1);

        [Test] public void EnemyDamageUnitConversionOnlyAppliesOnceAfterParry()
        {
            var shot = new Projectile(); var player = new Target { Id = 1, Parry = true };
            var contact = new ContactHost(shot, player, shot.Position);
            shot.Owner.TryContact(player.Id, true, ref contact, 5);
            Assert.That(player.Health, Is.EqualTo(100));
            Assert.That(shot.Owner.Read().Damage, Is.EqualTo(56).Within(.001));
            var enemy = new Target { Id = 2 }; contact = new ContactHost(shot, enemy, shot.Position);
            shot.Owner.TryContact(enemy.Id, false, ref contact, 5);
            Assert.That(enemy.Health, Is.EqualTo(44).Within(.001));
            var incoming = new Projectile(); var unguarded = new Target { Id = 3 };
            contact = new ContactHost(incoming, unguarded, incoming.Position);
            incoming.Owner.TryContact(unguarded.Id, true, ref contact, 5);
            Assert.That(unguarded.Health, Is.EqualTo(92), "Incoming player damage is not scaled");
        }

        [Test] public void ExpiryUsesTheCurrentExternalClockAtEntryNotDurationOrAnAccumulatedAge()
        {
            var shot = new Projectile(expires: .7f) { Now = Before(.7f) };
            shot.Step(50); shot.Step(0); shot.Step(-1);
            Assert.That(shot.Sweeps, Is.EqualTo(new[] { 50f, 0f, -1f }));
            Assert.That(shot.Owner.Read().Removed, Is.False);
            shot.Now = .7f; shot.Step(0);
            Assert.That(shot.Owner.Read().Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
            shot.Now = 0; shot.Step(1); Assert.That(shot.Sweeps.Count, Is.EqualTo(3));
            var after = new Projectile(expires: .7f) { Now = After(.7f) }; after.Step(.01f);
            Assert.That(after.Sweeps, Is.Empty); Assert.That(after.Removals, Is.EqualTo(1));
        }
        [TestCase(false, false, false, true)]
        [TestCase(false, true, true, true)]
        [TestCase(true, false, false, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, true)]
        public void MissingAndDeadPlayerSourcesStopButDeadNonplayerSourcesContinue(bool exists, bool dead, bool player, bool removed)
        {
            var shot = new Projectile { SourceExists = exists, SourceDead = dead, SourceIsPlayer = player };
            shot.Step(.125f);
            Assert.That(shot.Owner.Read().Removed, Is.EqualTo(removed));
            if (removed) Assert.That(shot.Sweeps, Is.Empty);
            else Assert.That(shot.Sweeps, Is.EqualTo(new[] { .125f }));
            Assert.That(shot.Removals, Is.EqualTo(removed ? 1 : 0));
        }
        [TestCase(false, false, false, false, false, false)]
        [TestCase(false, false, false, true, false, true)]
        [TestCase(false, false, false, false, true, true)]
        [TestCase(false, false, false, true, true, true)]
        [TestCase(false, true, false, true, false, false)]
        [TestCase(false, false, true, false, true, false)]
        [TestCase(true, false, false, false, false, true)]
        [TestCase(true, false, false, true, false, false)]
        [TestCase(true, false, false, false, true, false)]
        [TestCase(true, false, false, true, true, false)]
        [TestCase(true, true, false, false, false, false)]
        [TestCase(true, false, true, false, false, false)]
        public void TargetSidesChangeOnlyAfterReflection(bool reflected, bool dead, bool source, bool player, bool decoy, bool accepted)
        {
            var shot = new Projectile();
            if (reflected) shot.Contact(new Target { Id = 1, Parry = true });
            Assert.That(shot.Owner.AcceptsTarget(dead, source, player, decoy), Is.EqualTo(accepted));
        }
        [Test] public void ReflectionCapturesLiveBonusAfterFlagSourceAndVelocityChangeInNativeOrder()
        {
            var shot = new Projectile(7.3f) { Position = 3, Bonus = .2f }; var initial = shot.Owner.Read();
            var target = new Target { Id = 22, Parry = true };
            shot.DuringParry = () => {
                Assert.That(shot.Owner.Read().Reflected, Is.False); Assert.That(shot.Source, Is.EqualTo(10));
                Assert.That(shot.Velocity, Is.EqualTo(-8)); Assert.That(Bits(shot.Owner.Read().Damage), Is.EqualTo(Bits(7.3f)));
                shot.Position = 99;
            };
            shot.DuringFlip = () => {
                Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Source, Is.EqualTo(22));
                Assert.That(shot.Velocity, Is.EqualTo(8)); Assert.That(Bits(shot.Owner.Read().Damage), Is.EqualTo(Bits(7.3f)));
                shot.Bonus = .6f;
            };
            shot.DuringBonus = () => {
                Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Source, Is.EqualTo(22));
                Assert.That(shot.Velocity, Is.EqualTo(8)); Assert.That(shot.Position, Is.EqualTo(99));
            };
            shot.DuringRestore = () => Assert.That(Bits(shot.Owner.Read().Damage), Is.EqualTo(Bits(7.3f * (1 + .6f))));
            Assert.That(shot.Contact(target), Is.True);
            Assert.That(shot.Events, Is.EqualTo(new[] { "parry", "flip", "bonus", "restore" }));
            Assert.That(shot.Position, Is.EqualTo(3)); Assert.That(target.Health, Is.EqualTo(100));
            Assert.That(initial.Reflected, Is.False); Assert.That(initial.Damage, Is.EqualTo(7.3f)); Assert.That(initial.Removed, Is.False);
            var reflected = shot.Owner.Read(); shot.Bonus = 10;
            var enemy = new Target { Id = 23 }; shot.Contact(enemy, false);
            Assert.That(Bits(enemy.LastDamage), Is.EqualTo(Bits(7.3f * (1 + .6f))));
            Assert.That(reflected.Removed, Is.False); Assert.That(shot.Owner.Read().Removed, Is.True);
            Assert.That(shot.Owner.Read().Expires, Is.EqualTo(3));
        }
        [Test] public void ReflectionClearsOlderClaimsThenMarksTheParrierAndCannotApplyTheMultiplierTwice()
        {
            var shot = new Projectile(); var earlier = new Target { Id = 1 };
            shot.DuringParry = () => throw new InvalidOperationException("native parry failure");
            Assert.Throws<InvalidOperationException>(() => shot.Contact(earlier));
            shot.DuringParry = null; var parrier = new Target { Id = 2, Parry = true };
            shot.Contact(parrier); Assert.That(shot.Contact(parrier), Is.False);
            float reflectedDamage = shot.Owner.Read().Damage; shot.Bonus = 8;
            Assert.That(shot.Contact(earlier), Is.True);
            Assert.That(shot.Events, Is.EqualTo(new[] { "parry", "parry", "flip", "bonus", "restore", "receive", "remove" }));
            Assert.That(earlier.Hits, Is.EqualTo(1)); Assert.That(earlier.LastDamage, Is.EqualTo(reflectedDamage));
            Assert.That(shot.Source, Is.EqualTo(2)); Assert.That(shot.Velocity, Is.EqualTo(8));
        }
        [Test] public void SameTargetReentryDuringParryCannotReclaimTheProjectile()
        {
            var shot = new Projectile(); var target = new Target { Id = 7, Parry = true };
            shot.DuringParry = () => {
                Assert.That(shot.Contact(target), Is.False); shot.Step(.125f);
            };
            shot.Contact(target);
            Assert.That(shot.Sweeps, Is.EqualTo(new[] { .125f })); Assert.That(shot.Owner.Read().Reflected, Is.True);
            Assert.That(shot.Source, Is.EqualTo(7)); Assert.That(shot.Removals, Is.Zero); Assert.That(target.Hits, Is.Zero);
        }
        [Test] public void ReceiveRunsBeforeRemovalAndAllowsEnteredNativeReentryWithoutDoubleRemoving()
        {
            var shot = new Projectile(); var first = new Target { Id = 1 }; var second = new Target { Id = 2 };
            bool nested = false;
            shot.DuringReceive = () => {
                if (nested) return; nested = true;
                Assert.That(shot.Owner.Read().Removed, Is.False); Assert.That(shot.Contact(first), Is.False);
                shot.Step(.01f); shot.Contact(second, false);
            };
            shot.Contact(first);
            Assert.That(first.Hits, Is.EqualTo(1)); Assert.That(second.Hits, Is.EqualTo(1));
            Assert.That(first.Health, Is.EqualTo(92)); Assert.That(second.Health, Is.EqualTo(92));
            Assert.That(shot.Events, Is.EqualTo(new[] { "parry", "receive", "sweep", "receive", "remove" }));
            Assert.That(shot.Removals, Is.EqualTo(1)); Assert.That(shot.Owner.Read().Removed, Is.True);
        }
        [Test] public void RemoveClaimsTerminalStateBeforeNativeReentryAndBeforeItsException()
        {
            var shot = new Projectile(); var target = new Target { Id = 1 };
            shot.DuringRemove = () => {
                Assert.That(shot.Owner.Read().Removed, Is.True); shot.Step(1);
                Assert.That(shot.Contact(new Target { Id = 2 }), Is.False);
                shot.Owner.Release(shot); throw new InvalidOperationException("native remove failure");
            };
            Assert.Throws<InvalidOperationException>(() => shot.Contact(target));
            Assert.That(target.Hits, Is.EqualTo(1)); Assert.That(shot.Removals, Is.EqualTo(1)); Assert.That(shot.Sweeps, Is.Empty);
            shot.Step(1); shot.Owner.Release(shot); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void ReceiveRejectionStillConsumesTheProjectile()
        {
            var shot = new Projectile(); var target = new Target { Id = 1, RejectDamage = true };
            shot.Contact(target);
            Assert.That(target.Health, Is.EqualTo(100)); Assert.That(target.Hits, Is.Zero);
            Assert.That(shot.Owner.Read().Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void ReceiveExceptionRetainsDamageAndClaimWithoutInventingRemoval()
        {
            var shot = new Projectile(); var target = new Target { Id = 1 };
            shot.DuringReceive = () => throw new InvalidOperationException("native receive failure");
            Assert.Throws<InvalidOperationException>(() => shot.Contact(target));
            Assert.That(target.Health, Is.EqualTo(92)); Assert.That(target.Hits, Is.EqualTo(1));
            Assert.That(shot.Owner.Read().Removed, Is.False); Assert.That(shot.Removals, Is.Zero);
            Assert.That(shot.Contact(target), Is.False);
            shot.DuringReceive = null; var other = new Target { Id = 2 }; shot.Contact(other);
            Assert.That(other.Health, Is.EqualTo(92)); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void SourceFlipExceptionKeepsTheReflectionFlagAndNativePartialEffectsWithoutReadingTheBonus()
        {
            var shot = new Projectile(); var parrier = new Target { Id = 1, Parry = true };
            shot.DuringFlip = () => throw new InvalidOperationException("native source flip failure");
            Assert.Throws<InvalidOperationException>(() => shot.Contact(parrier));
            Assert.That(shot.Events, Is.EqualTo(new[] { "parry", "flip" }));
            Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Owner.Read().Damage, Is.EqualTo(8));
            Assert.That(shot.Owner.Read().Removed, Is.False); Assert.That(shot.Source, Is.EqualTo(1));
            Assert.That(shot.Velocity, Is.EqualTo(8)); Assert.That(shot.Contact(parrier), Is.False);
            shot.DuringFlip = null; var next = new Target { Id = 2 }; shot.Contact(next, false);
            Assert.That(next.Health, Is.EqualTo(92)); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void BonusReadReentrySeesTheOldDamageBeforeTheOuterMultiplyAndLedgerReset()
        {
            var shot = new Projectile(); var parrier = new Target { Id = 1, Parry = true }; var other = new Target { Id = 2 };
            shot.DuringBonus = () => {
                Assert.That(shot.Source, Is.EqualTo(1)); Assert.That(shot.Velocity, Is.EqualTo(8));
                Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Owner.Read().Damage, Is.EqualTo(8));
                shot.Contact(other, false);
            };
            shot.Contact(parrier);
            Assert.That(other.LastDamage, Is.EqualTo(8)); Assert.That(shot.Owner.Read().Removed, Is.True);
            Assert.That(Bits(shot.Owner.Read().Damage), Is.EqualTo(Bits(8 * (1 + .4f))));
            Assert.That(shot.Events, Is.EqualTo(new[] { "parry", "flip", "bonus", "receive", "remove", "restore" }));
            Assert.That(shot.Removals, Is.EqualTo(1)); Assert.That(parrier.Hits, Is.Zero);
        }
        [Test] public void BonusExceptionRetainsTheFlippedSourceAndFlagButNotTheUnfinishedDamageAndLedgerReset()
        {
            var shot = new Projectile(); var target = new Target { Id = 1, Parry = true };
            shot.DuringBonus = () => throw new InvalidOperationException("native bonus failure");
            Assert.Throws<InvalidOperationException>(() => shot.Contact(target));
            Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Source, Is.EqualTo(1));
            Assert.That(shot.Velocity, Is.EqualTo(8)); Assert.That(shot.Owner.Read().Damage, Is.EqualTo(8));
            Assert.That(shot.Owner.Read().Removed, Is.False); Assert.That(shot.Contact(target), Is.False);
            shot.DuringBonus = null; var next = new Target { Id = 2, Parry = true }; shot.Contact(next);
            Assert.That(next.Health, Is.EqualTo(92)); Assert.That(shot.Source, Is.EqualTo(1)); Assert.That(shot.Velocity, Is.EqualTo(8));
        }
        [Test] public void RestoreExceptionKeepsTheCompletedMultiplierAndNewLedger()
        {
            var shot = new Projectile(); var earlier = new Target { Id = 1 }; var parrier = new Target { Id = 2, Parry = true };
            shot.DuringParry = () => throw new InvalidOperationException("earlier failure");
            Assert.Throws<InvalidOperationException>(() => shot.Contact(earlier)); shot.DuringParry = null;
            shot.DuringRestore = () => throw new InvalidOperationException("restore failure");
            Assert.Throws<InvalidOperationException>(() => shot.Contact(parrier));
            Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Owner.Read().Removed, Is.False);
            Assert.That(Bits(shot.Owner.Read().Damage), Is.EqualTo(Bits(8 * (1 + .4f))));
            Assert.That(shot.Contact(parrier), Is.False); shot.Contact(earlier, false);
            Assert.That(earlier.LastDamage, Is.EqualTo(shot.Owner.Read().Damage)); Assert.That(earlier.Hits, Is.EqualTo(1));
        }
        [Test] public void EnteredParryFinishesItsOriginalAssignmentOrderAfterAnotherContactRemovesTheProjectile()
        {
            var shot = new Projectile(); var parrier = new Target { Id = 1, Parry = true }; var other = new Target { Id = 2 };
            shot.DuringParry = () => { shot.DuringParry = null; shot.Contact(other, false); };
            shot.Contact(parrier);
            Assert.That(other.Health, Is.EqualTo(92)); Assert.That(parrier.Health, Is.EqualTo(100));
            Assert.That(shot.Owner.Read().Removed, Is.True); Assert.That(shot.Owner.Read().Reflected, Is.True);
            Assert.That(shot.Source, Is.EqualTo(1)); Assert.That(shot.Velocity, Is.EqualTo(8));
            Assert.That(shot.Events, Is.EqualTo(new[] { "parry", "receive", "remove", "flip", "bonus", "restore" }));
            Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void SweepExceptionsAndReentryUseTheSameOwnerAndDoNotCreateAClock()
        {
            var shot = new Projectile(); bool nested = false;
            shot.DuringSweep = () => {
                if (nested) return; nested = true; shot.Step(.01f);
                shot.Now = 3; throw new InvalidOperationException("native sweep failure");
            };
            Assert.Throws<InvalidOperationException>(() => shot.Step(.02f));
            Assert.That(shot.Sweeps, Is.EqualTo(new[] { .02f, .01f })); Assert.That(shot.Owner.Read().Removed, Is.False);
            shot.Step(.01f); Assert.That(shot.Owner.Read().Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void SeparateExecutionsDoNotShareClaimsReflectionOrTerminalState()
        {
            var first = new Projectile(); var second = new Projectile(); var target = new Target { Id = 1, Parry = true };
            first.Contact(target); target.Parry = false; second.Contact(target);
            Assert.That(first.Owner.Read().Reflected, Is.True); Assert.That(first.Owner.Read().Removed, Is.False);
            Assert.That(second.Owner.Read().Reflected, Is.False); Assert.That(second.Owner.Read().Removed, Is.True);
            Assert.That(target.Health, Is.EqualTo(92)); first.Step(.02f);
            Assert.That(first.Sweeps, Is.EqualTo(new[] { .02f })); Assert.That(second.Sweeps, Is.Empty);
        }
        [Test] public void ExplicitReleaseStopsFutureStepsAndContactsButDoesNotUndoAnEnteredReceive()
        {
            var shot = new Projectile(); var target = new Target { Id = 1 };
            shot.DuringReceive = () => shot.Owner.Release(shot);
            shot.Contact(target); var state = shot.Owner.Read(); shot.Step(1); shot.Contact(new Target { Id = 2 }); shot.Owner.Release(shot);
            Assert.That(state.Removed, Is.True); Assert.That(target.Health, Is.EqualTo(92)); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.Sweeps, Is.Empty); Assert.That(shot.Owner.Read().Damage, Is.EqualTo(state.Damage));
        }
    }
}
