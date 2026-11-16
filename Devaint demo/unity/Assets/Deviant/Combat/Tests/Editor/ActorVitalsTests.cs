using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorVitalsTests
    {
        [Test] public void VitalsContractsAndStateDoNotReferenceEngineOrOtherRuntime()
        {
            foreach (var type in new[] { typeof(IActorVitals), typeof(ActorVitalsSnapshot), typeof(ActorVitalsState) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void GuardThenShieldThenHealthPreservesActualDamageAndAcceptedCount()
        {
            IActorVitals state = new ActorVitalsState(100); state.WriteShield(8); state.WriteGuardUntil(2); state.WriteGuardReduction(.5f);
            var hit = state.ApplyDamage(20, 1, false);
            Assert.That(hit.Accepted, Is.True); Assert.That(hit.Damage, Is.EqualTo(2)); Assert.That(hit.Absorbed, Is.EqualTo(8)); Assert.That(hit.State.Health, Is.EqualTo(98)); Assert.That(hit.State.Shield, Is.Zero); Assert.That(hit.State.ReceivedHits, Is.EqualTo(1));
        }
        [Test] public void ZeroAndFullyAbsorbedHitsAreStillAcceptedAndRecorded()
        {
            var state = new ActorVitalsState(100); state.WriteShield(30); state.WriteGuardUntil(5);
            Assert.That(state.ApplyDamage(0, 0, false).Accepted, Is.True); var hit = state.ApplyDamage(10, 0, false);
            Assert.That(hit.Damage, Is.Zero); Assert.That(hit.State.Health, Is.EqualTo(100)); Assert.That(hit.State.Shield, Is.EqualTo(20)); Assert.That(hit.State.ReceivedHits, Is.EqualTo(2));
        }
        [Test] public void BypassSkipsReductionAndAbsorptionWithoutConsumingTheShield()
        {
            var state = new ActorVitalsState(100); state.WriteShield(8); state.WriteGuardUntil(2); state.WriteGuardReduction(.5f); var hit = state.ApplyDamage(20, 1, true);
            Assert.That(hit.Damage, Is.EqualTo(20)); Assert.That(hit.Absorbed, Is.Zero); Assert.That(hit.State.Health, Is.EqualTo(80)); Assert.That(hit.State.Shield, Is.EqualTo(8));
        }
        [Test] public void GuardExpiryIsStrictAndShieldExpiryHappensOnlyAtTheExplicitTick()
        {
            var state = new ActorVitalsState(100); state.WriteGuardUntil(2); state.WriteGuardReduction(.5f); Assert.That(state.ApplyDamage(20, 2, false).Damage, Is.EqualTo(20));
            state.WriteShield(5); var before = state.Read(); state.ExpireShield(1.999f); Assert.That(state.Read().Shield, Is.EqualTo(5)); state.ExpireShield(2); Assert.That(state.Read().Shield, Is.Zero); Assert.That(before.Shield, Is.EqualTo(5));
        }
        [Test] public void LethalDamageKeepsOverkillAndRejectsLaterHits()
        {
            var state = new ActorVitalsState(7); var hit = state.ApplyDamage(20, 0, false); Assert.That(hit.Damage, Is.EqualTo(20)); Assert.That(hit.State.Health, Is.Zero); Assert.That(hit.State.Dead, Is.True);
            var later = state.ApplyDamage(1, 1, false); Assert.That(later.Accepted, Is.False); Assert.That(later.State.ReceivedHits, Is.EqualTo(1));
        }
        [Test] public void ResetKeepsMaximumAndLegacyReductionButClearsTransientDamageState()
        {
            var state = new ActorVitalsState(100); state.WriteMaximum(140); state.WriteGuardReduction(.2f); state.WriteGuardUntil(5); state.WriteShield(12); state.ApplyDamage(30, 0, false); var before = state.Read(); state.Reset(); var reset = state.Read();
            Assert.That(reset.Health, Is.EqualTo(140)); Assert.That(reset.Maximum, Is.EqualTo(140)); Assert.That(reset.Shield, Is.Zero); Assert.That(reset.GuardUntil, Is.Zero); Assert.That(reset.ReceivedHits, Is.Zero); Assert.That(reset.GuardReduction, Is.EqualTo(.2f));
            Assert.That(before.ReceivedHits, Is.EqualTo(1)); Assert.That(before.Health, Is.EqualTo(88));
        }
        [Test] public void CompatibilityWritesAndDifferentOwnersDoNotShareStorageOrClampHealth()
        {
            var a = new ActorVitalsState(100); var b = new ActorVitalsState(100); a.WriteMaximum(20); a.WriteHealth(30); a.WriteShield(4); var old = a.Read(); a.WriteHealth(-1);
            Assert.That(a.Read().Dead, Is.True); Assert.That(old.Health, Is.EqualTo(30)); Assert.That(old.Maximum, Is.EqualTo(20)); Assert.That(b.Read().Health, Is.EqualTo(100)); Assert.That(b.Read().Shield, Is.Zero);
            foreach (var type in new[] { typeof(ActorVitalsSnapshot), typeof(ActorDamageResult) }) Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
        }
        [Test] public void InvalidCommandsLeavePreviousValuesAndCountersUntouched()
        {
            var state = new ActorVitalsState(100); state.WriteShield(4); var before = state.Read();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteHealth(float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteMaximum(float.PositiveInfinity)); Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteShield(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteGuardUntil(float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteGuardReduction(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyDamage(float.NaN, 0, false)); Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyDamage(1, -1, false)); Assert.Throws<ArgumentOutOfRangeException>(() => state.ExpireShield(float.PositiveInfinity));
            Assert.That(state.Read().Health, Is.EqualTo(before.Health)); Assert.That(state.Read().Shield, Is.EqualTo(before.Shield)); Assert.That(state.Read().ReceivedHits, Is.EqualTo(before.ReceivedHits));
        }
    }
}
