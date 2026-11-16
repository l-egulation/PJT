using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActionResourceTests
    {
        sealed class Source : IActionResourceSource
        {
            public int Calls;
            public Func<ActionResourceKey, float, ActionResourceResult> Execute;
            public ActionResourceResult TryActivate(ActionResourceKey key, float now) { Calls++; return Execute(key, now); }
        }
        static ActionResourceKey Key(bool basic = false) => new ActionResourceKey(Guid.NewGuid(), Guid.NewGuid(), basic);
        static ActionResourceResult Success(ActionResourceKey key, float now, float multiplier = 1) => new ActionResourceResult(ActionResourceStatus.Succeeded, new ActionResourceSnapshot(key, now, multiplier));
        [Test] public void ResourceContractsAndExecutionHaveNoEngineDependenciesAndRejectInvalidDescriptors()
        {
            foreach (var type in new[] { typeof(IActionResources), typeof(ActionResourceExecution) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor")), Is.False);
            Assert.Throws<ArgumentException>(() => new ActionResourceKey(Guid.Empty, Guid.NewGuid(), false));
            Assert.Throws<ArgumentException>(() => new ActionResourceSnapshot(default, 0, 1));
            Assert.Throws<ArgumentException>(() => new ActionResourceSnapshot(Key(), 0, float.NaN));
            Assert.Throws<ArgumentException>(() => new ActionResourceResult(ActionResourceStatus.Succeeded));
            Assert.Throws<ArgumentException>(() => new ActionResourceResult(ActionResourceStatus.Rejected));
        }
        [Test] public void OneExecutionCachesItsActivationTimeAndStatsAndNeverSpendsTwice()
        {
            float multiplier = 1.4f; var source = new Source { Execute = (key, now) => Success(key, now, multiplier) }; var key = Key();
            using (var resources = new ActionResourceExecution(key, source))
            {
                var result = resources.TryActivate(key.ExecutionId, .18f); multiplier = 2;
                Assert.That(resources.TryActivate(key.ExecutionId, 10), Is.SameAs(result)); Assert.That(result.Snapshot.ActivatedAt, Is.EqualTo(.18f)); Assert.That(result.Snapshot.DamageMultiplier, Is.EqualTo(1.4f));
                Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(resources.Current, Is.SameAs(result));
            }
        }
        [TestCase(ActionResourceStatus.Rejected)] [TestCase(ActionResourceStatus.Cancelled)] [TestCase(ActionResourceStatus.Failed)]
        public void FailedAdmissionIsAnExecutionFactAndDoesNotRetryWhenTheSourceLaterChanges(ActionResourceStatus status)
        {
            var source = new Source { Execute = (key, now) => new ActionResourceResult(status, reason: "fixture admission") }; var key = Key();
            using (var resources = new ActionResourceExecution(key, source))
            {
                var result = resources.TryActivate(key.ExecutionId, 0); source.Execute = (k, t) => Success(k, t);
                Assert.That(resources.TryActivate(key.ExecutionId, 100), Is.SameAs(result)); Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(result.Snapshot, Is.Null);
            }
        }
        [Test] public void InvalidOrStaleRequestsNeverReachTheSourceOrReplaceTheCurrentResult()
        {
            var source = new Source { Execute = (key, now) => Success(key, now) }; var key = Key();
            using (var resources = new ActionResourceExecution(key, source))
            {
                Assert.That(resources.TryActivate(Guid.NewGuid(), 0).Status, Is.EqualTo(ActionResourceStatus.Rejected));
                foreach (float now in new[] { -1f, float.NaN, float.PositiveInfinity }) Assert.That(resources.TryActivate(key.ExecutionId, now).Status, Is.EqualTo(ActionResourceStatus.Rejected));
                Assert.That(source.Calls, Is.Zero); Assert.That(resources.Current, Is.Null); var result = resources.TryActivate(key.ExecutionId, 1);
                Assert.That(resources.TryActivate(Guid.NewGuid(), 2).Status, Is.EqualTo(ActionResourceStatus.Rejected)); Assert.That(resources.Current, Is.SameAs(result));
            }
        }
        [TestCase("key")] [TestCase("time")] [TestCase("null")] [TestCase("throw")]
        public void ProviderErrorsOrMismatchedSnapshotsFailOnceWithoutRetry(string failure)
        {
            var source = new Source { Execute = (key, now) => {
                if (failure == "throw") throw new InvalidOperationException("fixture provider failed");
                if (failure == "null") return null;
                return Success(failure == "key" ? Key() : key, failure == "time" ? now + 1 : now);
            } }; var key = Key();
            using (var resources = new ActionResourceExecution(key, source))
            {
                var result = resources.TryActivate(key.ExecutionId, 0); Assert.That(result.Status, Is.EqualTo(ActionResourceStatus.Failed)); Assert.That(result.Reason, Is.Not.Empty);
                Assert.That(resources.TryActivate(key.ExecutionId, 1), Is.SameAs(result)); Assert.That(source.Calls, Is.EqualTo(1));
            }
        }
        [Test] public void ResourceCallbacksCannotReenterActivationOrDisposeTheirExecution()
        {
            ActionResourceExecution resources = null; var key = Key(); var source = new Source { Execute = (k, now) => {
                Assert.That(resources.TryActivate(k.ExecutionId, now).Status, Is.EqualTo(ActionResourceStatus.Rejected));
                Assert.Throws<InvalidOperationException>(() => resources.Dispose()); return Success(k, now);
            } };
            using (resources = new ActionResourceExecution(key, source)) { Assert.That(resources.TryActivate(key.ExecutionId, 0).Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(source.Calls, Is.EqualTo(1)); }
        }
        [TestCase(false)] [TestCase(true)] public void ClosingAnExecutionKeepsItsResultAndCannotRefundOrStartAnActivation(bool activate)
        {
            var source = new Source { Execute = (key, now) => Success(key, now) }; var key = Key(); var resources = new ActionResourceExecution(key, source);
            var result = activate ? resources.TryActivate(key.ExecutionId, 0) : null; resources.Dispose(); resources.Dispose();
            Assert.That(resources.IsActive, Is.False); Assert.That(resources.Current, Is.SameAs(result)); Assert.That(resources.TryActivate(key.ExecutionId, 2).Status, Is.EqualTo(ActionResourceStatus.Cancelled));
            Assert.That(source.Calls, Is.EqualTo(activate ? 1 : 0)); Assert.That(resources.Current, Is.SameAs(result));
        }
        [Test] public void BasicAndSpecialStatsAreCapturedFromTheOriginalWeaponAndOnlySpecialSpends()
        {
            var weapon = new WeaponState(); weapon.StatSlots[0] = 1; weapon.StatSlots[1] = weapon.StatSlots[2] = 2; weapon.Cooldown.Reset(3, 5);
            var basic = new ActionResourceKey(Guid.NewGuid(), weapon.InstanceId, true); var special = new ActionResourceKey(Guid.NewGuid(), weapon.InstanceId, false); var source = new WeaponActionResourceSource(weapon);
            using (var a = new ActionResourceExecution(basic, source)) using (var b = new ActionResourceExecution(special, source))
            {
                var first = a.TryActivate(basic.ExecutionId, 0); Assert.That(first.Snapshot.DamageMultiplier, Is.EqualTo(1.2f).Within(1e-5f)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(3));
                var second = b.TryActivate(special.ExecutionId, .08f); Assert.That(second.Snapshot.DamageMultiplier, Is.EqualTo(1.44f).Within(1e-5f)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(2)); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(5.08f));
                weapon.StatSlots.Clear(); Assert.That(first.Snapshot.DamageMultiplier, Is.EqualTo(1.2f).Within(1e-5f)); Assert.That(second.Snapshot.DamageMultiplier, Is.EqualTo(1.44f).Within(1e-5f));
                b.Dispose(); Assert.That(weapon.Cooldown.Available, Is.EqualTo(2)); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(5.08f));
            }
        }
        [Test] public void SameFormInstancesAndConcurrentActionKeysKeepIndependentOwnershipAndSequentialRecovery()
        {
            var weapon = new WeaponState(); var other = new WeaponState(); Assert.That(weapon.InstanceId, Is.Not.EqualTo(other.InstanceId)); weapon.Cooldown.Reset(3, 5);
            foreach (float now in new[] { .08f, .46f, .84f })
            {
                var key = new ActionResourceKey(Guid.NewGuid(), weapon.InstanceId, false);
                using (var resources = new ActionResourceExecution(key, new WeaponActionResourceSource(weapon))) Assert.That(resources.TryActivate(key.ExecutionId, now).Status, Is.EqualTo(ActionResourceStatus.Succeeded));
            }
            Assert.That(weapon.Cooldown.Available, Is.Zero); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(5.08f)); Assert.That(other.Cooldown.Available, Is.EqualTo(1));
            var emptyKey = new ActionResourceKey(Guid.NewGuid(), weapon.InstanceId, false);
            using (var empty = new ActionResourceExecution(emptyKey, new WeaponActionResourceSource(weapon)))
            {
                var rejected = empty.TryActivate(emptyKey.ExecutionId, 1); weapon.Cooldown.Tick(5.08f); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1));
                Assert.That(empty.TryActivate(emptyKey.ExecutionId, 5.08f), Is.SameAs(rejected)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1));
            }
            weapon.Cooldown.Tick(15.08f); Assert.That(weapon.Cooldown.Available, Is.EqualTo(3));
            var wrong = new ActionResourceKey(Guid.NewGuid(), weapon.InstanceId, false);
            Assert.That(new WeaponActionResourceSource(other).TryActivate(wrong, 20).Status, Is.EqualTo(ActionResourceStatus.Rejected)); Assert.That(other.Cooldown.Available, Is.EqualTo(1));
        }
    }
}
