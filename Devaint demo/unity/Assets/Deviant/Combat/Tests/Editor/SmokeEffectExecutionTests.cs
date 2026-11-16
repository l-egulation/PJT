using Deviant.Combat;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class SmokeEffectExecutionTests
    {
        static SkillEffectDefinition Definition(float duration = .5f) => new SkillEffectDefinition(new EffectData { type = "smoke", duration = duration });

        [Test] public void EffectLifetimeUsesTheAuthoredActivationBoundaryAndDoesNotOwnActivationAdmission()
        {
            var effect = new SmokeEffectExecution(Definition());
            Assert.That(effect.IsActive(0), Is.False);
            Assert.That(effect.End, Is.Zero);
            effect.Activate(.125f);
            Assert.That(effect.End, Is.EqualTo(.625f));
            Assert.That(effect.IsActive(.125f), Is.True);
            Assert.That(effect.IsActive(.62499994f), Is.True);
            Assert.That(effect.IsActive(.625f), Is.False);
            Assert.That(effect.IsActive(1), Is.False);
            Assert.That(effect.TryRefresh(.625f, true, 10, .125f, out _), Is.False);
            Assert.That(effect.End, Is.EqualTo(.625f), "Queries cannot advance or extend effect lifetime.");
        }

        [Test] public void LeavingReturnsAClearedSmokeDeadlineAndReentryGrantsAgain()
        {
            var effect = new SmokeEffectExecution(Definition()); effect.Activate(.125f);
            var owner = new ActorStatusState();
            Assert.That(effect.TryRefresh(.125f, true, 8, .125f, out float until), Is.True); owner.WriteSmokeUntil(until);
            Assert.That(owner.Read().SmokeUntil, Is.EqualTo(8.1875f));
            Assert.That(effect.TryRefresh(.25f, true, 8.015625f, .015625f, out until), Is.True); owner.WriteSmokeUntil(until);
            Assert.That(owner.Read().SmokeUntil, Is.EqualTo(8.0390625f), "Refresh replaces the deadline; it is not a maximum/stacking merge.");
            Assert.That(effect.TryRefresh(.265625f, false, 8.03125f, .015625f, out until), Is.False); owner.WriteSmokeUntil(until);
            Assert.That(owner.Read().HiddenAt(8.03125f), Is.False);
            Assert.That(owner.Read().SmokeUntil, Is.Zero);
            Assert.That(effect.TryRefresh(.28125f, true, 8.046875f, .03125f, out until), Is.True); owner.WriteSmokeUntil(until);
            Assert.That(owner.Read().SmokeUntil, Is.EqualTo(8.09375f));
            Assert.That(owner.Read().HiddenAt(8.046875f), Is.True, "Unspent smoke grants again on re-entry.");
        }

        [Test] public void AttackSpendsThisEffectWithoutShorteningItOrClearingAnotherConcealmentSource()
        {
            var effect = new SmokeEffectExecution(Definition()); effect.Activate(.125f);
            var owner = new ActorStatusState(); owner.WriteStealthUntil(20);
            Assert.That(effect.TryRefresh(.25f, true, 10, .125f, out float until), Is.True); owner.WriteSmokeUntil(until);
            owner.ClearConcealment(); effect.MarkSpent(); effect.MarkSpent();
            Assert.That(owner.Read().HiddenAt(10), Is.False);
            Assert.That(effect.TryRefresh(.25f, true, 10, .125f, out _), Is.False);
            Assert.That(effect.TryRefresh(.375f, false, 10.125f, .125f, out _), Is.False);
            Assert.That(effect.TryRefresh(.5f, true, 10.25f, .125f, out _), Is.False, "Leaving and re-entry never restore a spent instance.");
            Assert.That(effect.IsActive(.5f), Is.True, "Spent smoke keeps its independent visual/lifetime interval.");
            Assert.That(effect.End, Is.EqualTo(.625f));
            owner.WriteStealthUntil(21);
            Assert.That(owner.Read().HiddenAt(20), Is.True, "The smoke owner is not a second actor-status authority.");
            Assert.That(effect.Read().Spent, Is.True);
        }

        [Test] public void SpendingDuringPreparationSurvivesActivationButDoesNotAffectAReplacementInstance()
        {
            var definition = Definition(); var old = new SmokeEffectExecution(definition);
            old.MarkSpent(); old.Activate(.125f);
            Assert.That(old.TryRefresh(.25f, true, 1, .125f, out _), Is.False, "Activation does not undo a pre-activation attack break.");
            var beforeReplacement = old.Read(); var replacement = new SmokeEffectExecution(definition); replacement.Activate(1);
            Assert.That(replacement.End, Is.EqualTo(1.5f));
            Assert.That(replacement.TryRefresh(1.25f, true, 2, .125f, out float until), Is.True);
            Assert.That(until, Is.EqualTo(2.1875f));
            Assert.That(old.Read().End, Is.EqualTo(beforeReplacement.End));
            Assert.That(old.Read().Spent, Is.True);
            var beforeAttack = replacement.Read(); replacement.MarkSpent();
            Assert.That(beforeAttack.Spent, Is.False, "Read-only snapshots are values, not writable aliases to the effect.");
            Assert.That(replacement.TryRefresh(1.375f, true, 2.125f, .125f, out _), Is.False);
        }

        [Test] public void ExpiryReturnsAClearedSmokeDeadlineWithoutClearingBasicStealth()
        {
            var effect = new SmokeEffectExecution(Definition()); effect.Activate(.125f);
            var owner = new ActorStatusState();
            Assert.That(effect.TryRefresh(.5625f, true, 4, .0625f, out float until), Is.True); owner.WriteSmokeUntil(until);
            Assert.That(owner.Read().SmokeUntil, Is.EqualTo(4.09375f));
            Assert.That(effect.TryRefresh(.625f, true, 4.0625f, .015625f, out until), Is.False); owner.WriteSmokeUntil(until);
            Assert.That(owner.Read().HiddenAt(4.0625f), Is.False);
            Assert.That(owner.Read().SmokeUntil, Is.Zero);
            owner.WriteStealthUntil(5);
            Assert.That(owner.Read().HiddenAt(4.09375f), Is.True);
            Assert.That(effect.End, Is.EqualTo(.625f));
        }
    }
}
