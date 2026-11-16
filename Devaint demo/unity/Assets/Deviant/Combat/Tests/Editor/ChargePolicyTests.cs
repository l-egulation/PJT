using System;
using System.Globalization;
using System.Linq;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ChargePolicyTests
    {
        static ChargeDefinition Definition() => new ChargeDefinition("charge-test", 1, 3, 10, 30, 2.5f);
        static ChargeExecution Execution(ChargeDefinition definition = null, float start = 0) => new ChargeExecution(Guid.NewGuid(), definition ?? Definition(), start);
        [Test] public void PolicyAndExecutionHaveNoEngineOrCombatRuntimeReference()
        {
            foreach (var type in new[] { typeof(ChargeDefinition), typeof(ChargeReleaseTiming), typeof(ChargeExecution) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat"), Is.False);
        }
        [Test] public void EarlyReleaseWaitsForMinimumAndKeepsItsRecoveryInterval()
        {
            var execution = Execution(); Assert.That(execution.TryRelease(.5f, .1f, false), Is.False); Assert.That(execution.Release, Is.Null);
            Assert.That(execution.TryRelease(.95f, .1f, false), Is.True); var release = execution.Release.Value;
            Assert.That(release.ReleaseTime, Is.EqualTo(1)); Assert.That(release.Shift, Is.Zero); Assert.That(release.MotionEnd, Is.EqualTo(2.5f)); Assert.That(release.Damage, Is.EqualTo(10));
        }
        [Test] public void HeldInputWaitsUntilMaximumAndCannotResolveAgain()
        {
            var execution = Execution(); Assert.That(execution.TryRelease(1, .1f, true), Is.False); Assert.That(execution.TryRelease(2.99f, .1f, true), Is.False);
            Assert.That(execution.TryRelease(3, .1f, true), Is.True); var release = execution.Release.Value;
            Assert.That(release.ReleaseTime, Is.EqualTo(3)); Assert.That(release.MotionEnd, Is.EqualTo(4.5f)); Assert.That(release.Damage, Is.EqualTo(30));
            Assert.That(execution.TryRelease(4, .1f, false), Is.True); Assert.That(execution.Release.Value.ReleaseTime, Is.EqualTo(release.ReleaseTime)); Assert.That(execution.Release.Value.Damage, Is.EqualTo(release.Damage));
        }
        [Test] public void MiddleReleaseInterpolatesAndKeepsTheWholeResolvedValue()
        {
            var execution = Execution(); Assert.That(execution.TryRelease(2, .1f, false), Is.True); var release = execution.Release.Value;
            Assert.That(release.Shift, Is.EqualTo(1)); Assert.That(release.MotionEnd, Is.EqualTo(3.5f)); Assert.That(release.Damage, Is.EqualTo(20));
            execution.Cancel(); Assert.That(execution.IsActive, Is.False); Assert.That(execution.TryRelease(3, .1f, true), Is.False); Assert.That(execution.Release.Value.Damage, Is.EqualTo(release.Damage));
        }
        [Test] public void FractionalStartUsesLocalTimeWithoutAccumulatingASecondClock()
        {
            var execution = Execution(start: 2); Assert.That(execution.TryRelease(2.5f, .1f, false), Is.False); Assert.That(execution.TryRelease(3.95f, .1f, false), Is.True);
            Assert.That(execution.Release.Value.ReleaseTime, Is.EqualTo(1.95f).Within(.000001f));
            var future = Execution(start: .015f); Assert.That(future.TryRelease(0, .02f, false), Is.False);
        }
        [Test] public void CancelBeforeReleasePreservesAnUnresolvedTerminalPolicy()
        {
            var execution = Execution(); execution.Cancel(); execution.Cancel(); Assert.That(execution.Release, Is.Null); Assert.That(execution.IsActive, Is.False); Assert.That(execution.TryRelease(3, .1f, false), Is.False);
        }
        [Test] public void InvalidAndBackwardSamplesDoNotResolveOrAdvanceThePolicy()
        {
            var execution = Execution(); Assert.Throws<ArgumentOutOfRangeException>(() => execution.TryRelease(0, 0, false)); Assert.Throws<ArgumentOutOfRangeException>(() => execution.TryRelease(float.NaN, .1f, false));
            Assert.That(execution.TryRelease(.5f, .1f, true), Is.False); Assert.Throws<ArgumentOutOfRangeException>(() => execution.TryRelease(.4f, .1f, false)); Assert.That(execution.Release, Is.Null);
            Assert.That(execution.TryRelease(1, .1f, false), Is.True);
        }
        [Test] public void ProjectionAndHashAreIndependentAndCultureStable()
        {
            var definition = Definition(); var first = definition.Resolve(2); var second = definition.Resolve(3);
            Assert.That(first.Damage, Is.EqualTo(20)); Assert.That(second.Damage, Is.EqualTo(30)); Assert.That(first.ReleaseTime, Is.EqualTo(2));
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(Definition().ContentHash, Is.EqualTo(definition.ContentHash)); }
            finally { CultureInfo.CurrentCulture = culture; }
            Assert.That(new ChargeDefinition("other-charge", 1, 3, 10, 30, 2.5f).ContentHash, Is.Not.EqualTo(definition.ContentHash));
        }
        [Test] public void InvalidDefinitionsAndExecutionBoundsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new ChargeDefinition("", 1, 3, 10, 30, 2.5f)); Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeDefinition("x", 1, 1, 10, 30, 2.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeDefinition("x", -1, 3, 10, 30, 2.5f)); Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeDefinition("x", 1, float.PositiveInfinity, 10, 30, 2.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeDefinition("x", 1, 3, float.NaN, 30, 2.5f)); Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeDefinition("x", 1, 3, 10, 30, .5f));
            Assert.Throws<ArgumentException>(() => new ChargeExecution(Guid.Empty, Definition(), 0)); Assert.Throws<ArgumentOutOfRangeException>(() => Execution(start: float.PositiveInfinity)); Assert.Throws<ArgumentOutOfRangeException>(() => Definition().Resolve(float.NaN));
        }
    }
}
