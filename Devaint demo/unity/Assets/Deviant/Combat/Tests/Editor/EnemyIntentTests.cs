using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EnemyIntentTests
    {
        static EnemyObservation O(int profile = 0, float x = 2, float y = 0, float now = .5f, float ready = .5f,
            bool aggressive = true, bool target = true, bool controlled = false, bool attacking = false)
            => new EnemyObservation(profile, aggressive, target, controlled, attacking, now, ready, x, y);
        [Test] public void ContractsAndDecisionsHaveNoEngineOrSceneDependencies()
        {
            foreach (var type in new[] { typeof(EnemyObservation), typeof(EnemyIntent), typeof(EnemyIntentResolver) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void AttackAndChaseUseExistingInclusiveAndStrictBoundaries()
        {
            Assert.That(EnemyIntentResolver.Resolve(O(x: 2.2f)).Kind, Is.EqualTo(EnemyIntentKind.BeginAttack));
            Assert.That(EnemyIntentResolver.Resolve(O(x: 2.20001f)).Kind, Is.EqualTo(EnemyIntentKind.Chase));
            Assert.That(EnemyIntentResolver.Resolve(O(y: 2)).Kind, Is.EqualTo(EnemyIntentKind.Chase));
            Assert.That(EnemyIntentResolver.Resolve(O(y: 1.99999f)).Kind, Is.EqualTo(EnemyIntentKind.BeginAttack));
            Assert.That(EnemyIntentResolver.Resolve(O(1, 8)).Kind, Is.EqualTo(EnemyIntentKind.BeginAttack));
            Assert.That(EnemyIntentResolver.Resolve(O(1, 8.00001f)).Kind, Is.EqualTo(EnemyIntentKind.Chase));
            Assert.That(EnemyIntentResolver.Resolve(O(x: 10)).Kind, Is.EqualTo(EnemyIntentKind.None));
            Assert.That(EnemyIntentResolver.Resolve(O(x: 1.5f, ready: 1)).Kind, Is.EqualTo(EnemyIntentKind.None));
            Assert.That(EnemyIntentResolver.Resolve(O(1, 5, ready: 1)).Kind, Is.EqualTo(EnemyIntentKind.None));
            Assert.That(EnemyIntentResolver.Resolve(O(now: .49999f)).Kind, Is.EqualTo(EnemyIntentKind.Chase));
            Assert.That(EnemyIntentResolver.Resolve(O(now: .5f)).Kind, Is.EqualTo(EnemyIntentKind.BeginAttack));
        }
        [Test] public void SuppressedDecisionsDoNotCancelOrReplaceAnExistingAttack()
        {
            foreach (var observation in new[] { O(aggressive: false), O(target: false), O(controlled: true), O(attacking: true), default })
            {
                var intent = EnemyIntentResolver.Resolve(observation); Assert.That(intent.Kind, Is.EqualTo(EnemyIntentKind.None)); Assert.That(intent.Facing, Is.Zero); Assert.That(intent.Speed, Is.Zero);
            }
        }
        [Test] public void ProfileSpeedDirectionAndUnknownProfileCompatibilityArePreserved()
        {
            foreach (int profile in new[] { -1, 0, 1, 2, 3 })
            {
                var intent = EnemyIntentResolver.Resolve(O(profile, -9)); Assert.That(intent.Kind, Is.EqualTo(EnemyIntentKind.Chase)); Assert.That(intent.Facing, Is.EqualTo(-1)); Assert.That(intent.Speed, Is.EqualTo(profile == 2 ? 1.5f : 2.5f));
                Assert.That(EnemyIntentResolver.Resolve(O(profile, 0)).Facing, Is.EqualTo(1), "Exactly aligned actors use the existing right-facing tie.");
            }
        }
        [Test] public void ObservationsAndResultsRemainIndependentAcrossActorsAndLaterResolutions()
        {
            var first = O(x: -2); var attack = EnemyIntentResolver.Resolve(first);
            var other = O(2, 9); var chase = EnemyIntentResolver.Resolve(other);
            Assert.That(attack.Kind, Is.EqualTo(EnemyIntentKind.BeginAttack)); Assert.That(attack.Facing, Is.EqualTo(-1)); Assert.That(attack.Speed, Is.Zero);
            Assert.That(chase.Kind, Is.EqualTo(EnemyIntentKind.Chase)); Assert.That(chase.Facing, Is.EqualTo(1)); Assert.That(chase.Speed, Is.EqualTo(1.5f));
            Assert.That(first.Profile, Is.Zero); Assert.That(first.DeltaX, Is.EqualTo(-2)); Assert.That(first.Now, Is.EqualTo(.5f));
            Assert.That(EnemyIntentResolver.Resolve(O(target: false)).Kind, Is.EqualTo(EnemyIntentKind.None));
            var repeated = EnemyIntentResolver.Resolve(first); Assert.That(repeated.Kind, Is.EqualTo(attack.Kind)); Assert.That(repeated.Facing, Is.EqualTo(attack.Facing));
            foreach (var type in new[] { typeof(EnemyObservation), typeof(EnemyIntent) })
                Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False, "Public observations/results expose no mutable storage.");
        }
        [Test] public void InvalidObservationAndIntentValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => O(now: -1)); Assert.Throws<ArgumentOutOfRangeException>(() => O(now: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => O(ready: float.PositiveInfinity)); Assert.Throws<ArgumentOutOfRangeException>(() => O(x: float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => O(y: float.NegativeInfinity));
            Assert.Throws<ArgumentException>(() => new EnemyIntent(EnemyIntentKind.None, 1, 0)); Assert.Throws<ArgumentException>(() => new EnemyIntent(EnemyIntentKind.BeginAttack, 1, 1));
            Assert.Throws<ArgumentException>(() => new EnemyIntent(EnemyIntentKind.Chase, 0, 2)); Assert.Throws<ArgumentException>(() => new EnemyIntent(EnemyIntentKind.Chase, 1, 0)); Assert.Throws<ArgumentException>(() => new EnemyIntent((EnemyIntentKind)8, 1, 0));
        }
    }
}
