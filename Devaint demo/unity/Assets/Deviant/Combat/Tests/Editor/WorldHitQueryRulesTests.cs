using System;
using System.Collections.Generic;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class WorldHitQueryRulesTests
    {
        [Test] public void WallCrossingSelectionIsRadialAndStillRejectsTargetsOutsideItsRadius()
        {
            var facts = new Facts { Target = new WorldHitPoint(-6, 8), Occluded = true };
            var host = new TargetHost(facts); float distance = float.PositiveInfinity;
            Assert.That(WorldHitQueryRules.TrySelectCircle(10, ref host, ref distance), Is.False);
            Assert.That(WorldHitQueryRules.TrySelectCircle(10, ref host, ref distance, true), Is.True);
            Assert.That(distance, Is.EqualTo(10));
            facts.Target = new WorldHitPoint(-6, 8.1f); distance = float.PositiveInfinity;
            Assert.That(WorldHitQueryRules.TrySelectCircle(10, ref host, ref distance, true), Is.False);
        }
        sealed class Facts
        {
            public readonly List<string> Reads = new List<string>();
            public bool Exists = true, Player, Decoy, Dead, Occluded, BestExists;
            public int ActorId = 2, BestId = 1;
            public WorldHitPoint Owner = new WorldHitPoint(0, 0), Target = new WorldHitPoint(3, 4);
            public Action OnOccluded;
        }
        struct TargetHost : IWorldNearestTargetHost
        {
            readonly Facts facts;
            public TargetHost(Facts facts) { this.facts = facts; }
            public bool Exists { get { facts.Reads.Add("exists"); return facts.Exists; } }
            public bool IsPlayer { get { facts.Reads.Add("player"); return facts.Player; } }
            public bool IsDecoy { get { facts.Reads.Add("decoy"); return facts.Decoy; } }
            public bool Dead { get { facts.Reads.Add("dead"); return facts.Dead; } }
            public WorldHitPoint OwnerCenter { get { facts.Reads.Add("owner"); return facts.Owner; } }
            public WorldHitPoint TargetCenter { get { facts.Reads.Add("target"); return facts.Target; } }
            public bool Occluded() { facts.Reads.Add("occluded"); facts.OnOccluded?.Invoke(); return facts.Occluded; }
            public bool BestExists { get { facts.Reads.Add("best"); return facts.BestExists; } }
            public int ActorId { get { facts.Reads.Add("id"); return facts.ActorId; } }
            public int BestActorId { get { facts.Reads.Add("best-id"); return facts.BestId; } }
            public float ApproximationEpsilon => float.Epsilon;
        }
        sealed class Visibility
        {
            public readonly List<WorldHitPoint> Origins = new List<WorldHitPoint>();
            public readonly List<WorldHitPoint> Points = new List<WorldHitPoint>();
            public int OpenAt = -1;
            public Exception Failure;
        }
        struct VisibilityHost : IWorldHitVisibilityHost
        {
            readonly Visibility visibility;
            public VisibilityHost(Visibility visibility) { this.visibility = visibility; }
            public bool Occluded(WorldHitPoint origin, WorldHitPoint point)
            {
                visibility.Origins.Add(origin); visibility.Points.Add(point);
                if (visibility.Failure != null) throw visibility.Failure;
                return visibility.Points.Count - 1 != visibility.OpenAt;
            }
        }

        [Test] public void ScalarContractsAndRulesHaveNoEngineOrDefinitionsDependencyInContracts()
        {
            foreach (var type in new[] { typeof(WorldHitPoint), typeof(WorldHitBox), typeof(WorldHitBounds) })
            {
                Assert.That(type.IsValueType, Is.True);
                foreach (var property in type.GetProperties()) Assert.That(property.CanWrite, Is.False);
            }
            foreach (var reference in typeof(WorldHitPoint).Assembly.GetReferencedAssemblies())
                Assert.That(reference.Name, Does.Not.StartWith("Unity").And.Not.EqualTo("Deviant.Definitions"));
            foreach (var reference in typeof(WorldHitQueryRules).Assembly.GetReferencedAssemblies()) Assert.That(reference.Name, Does.Not.StartWith("Unity"));
            Assert.That(typeof(WorldHitQueryRules).GetMethod("TrySelectBox").GetGenericArguments()[0].GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint,
                Is.EqualTo(GenericParameterAttributes.NotNullableValueTypeConstraint));
        }

        [Test] public void BoxReadsDistanceAfterOcclusionButCircleReadsBeforeIt()
        {
            foreach (bool circle in new[] { false, true })
            {
                var facts = new Facts(); facts.OnOccluded = () => facts.Target = new WorldHitPoint(6, 8);
                var host = new TargetHost(facts); float distance = float.PositiveInfinity;
                Assert.That(circle ? WorldHitQueryRules.TrySelectCircle(20, ref host, ref distance) : WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.True);
                Assert.That(distance, Is.EqualTo(circle ? 5 : 10));
                Assert.That(facts.Reads, Is.EqualTo(circle
                    ? new[] { "exists", "dead", "decoy", "player", "owner", "target", "occluded" }
                    : new[] { "player", "decoy", "dead", "occluded", "owner", "target" }));
            }
        }

        [TestCase("player")]
        [TestCase("decoy")]
        [TestCase("dead")]
        public void RejectedBoxCandidatesNeverReadGeometry(string reason)
        {
            var facts = new Facts { Player = reason == "player", Decoy = reason == "decoy", Dead = reason == "dead" };
            var host = new TargetHost(facts); float distance = 20;
            Assert.That(WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.False); Assert.That(distance, Is.EqualTo(20));
            Assert.That(facts.Reads, Is.EqualTo(reason == "player" ? new[] { "player" } : reason == "decoy" ? new[] { "player", "decoy" } : new[] { "player", "decoy", "dead" }));
        }

        [Test] public void MissingCircleCandidateStopsBeforeAnyActorFacts()
        {
            var facts = new Facts { Exists = false }; var host = new TargetHost(facts); float distance = float.PositiveInfinity;
            Assert.That(WorldHitQueryRules.TrySelectCircle(20, ref host, ref distance), Is.False);
            Assert.That(facts.Reads, Is.EqualTo(new[] { "exists" })); Assert.That(distance, Is.EqualTo(float.PositiveInfinity));
        }

        [Test] public void RadiusIncludesExactDistanceAndSkipsOcclusionOutside()
        {
            var facts = new Facts(); var host = new TargetHost(facts); float distance = float.PositiveInfinity;
            Assert.That(WorldHitQueryRules.TrySelectCircle(5, ref host, ref distance), Is.True); Assert.That(distance, Is.EqualTo(5));
            facts.Reads.Clear(); distance = float.PositiveInfinity;
            Assert.That(WorldHitQueryRules.TrySelectCircle(4.9999995f, ref host, ref distance), Is.False);
            Assert.That(facts.Reads, Does.Not.Contain("occluded")); Assert.That(distance, Is.EqualTo(float.PositiveInfinity));
        }

        [Test] public void OccludedCandidatesLeaveBestDistanceUntouched()
        {
            foreach (bool circle in new[] { false, true })
            {
                var facts = new Facts { Occluded = true }; var host = new TargetHost(facts); float distance = 20;
                Assert.That(circle ? WorldHitQueryRules.TrySelectCircle(20, ref host, ref distance) : WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.False);
                Assert.That(distance, Is.EqualTo(20)); Assert.That(facts.Reads, Does.Not.Contain("best"));
                if (!circle) Assert.That(facts.Reads, Does.Not.Contain("owner"));
            }
        }

        [Test] public void SelectionExceptionsKeepTheExistingBestAndLiveEdits()
        {
            foreach (bool circle in new[] { false, true })
            {
                var facts = new Facts(); var failure = new InvalidOperationException("Native closest-point query failed");
                facts.OnOccluded = () => { facts.Target = new WorldHitPoint(6, 8); throw failure; };
                var host = new TargetHost(facts); float distance = 20;
                Assert.That(Assert.Throws<InvalidOperationException>(() =>
                {
                    if (circle) WorldHitQueryRules.TrySelectCircle(20, ref host, ref distance);
                    else WorldHitQueryRules.TrySelectBox(ref host, ref distance);
                }), Is.SameAs(failure));
                Assert.That(distance, Is.EqualTo(20)); Assert.That(facts.Target.X, Is.EqualTo(6)); Assert.That(facts.Target.Y, Is.EqualTo(8));
                Assert.That(facts.Reads, Does.Not.Contain("best"));
            }
        }

        [Test] public void StrictlyCloserWinsBeforeApproximateLowerIdTieRule()
        {
            var facts = new Facts { Target = new WorldHitPoint(2, 0), BestExists = true, ActorId = 20, BestId = 1 };
            var host = new TargetHost(facts); float distance = 2.0000005f;
            Assert.That(WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.True); Assert.That(distance, Is.EqualTo(2));
            Assert.That(facts.Reads, Does.Not.Contain("best"));
            facts.Reads.Clear(); facts.Target = new WorldHitPoint(2.0000005f, 0); facts.ActorId = 1; facts.BestId = 20;
            Assert.That(WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.True); Assert.That(distance, Is.EqualTo(2.0000005f));
            Assert.That(facts.Reads, Does.Contain("best-id"));
        }

        [TestCase(1, 2, true)]
        [TestCase(2, 1, false)]
        [TestCase(2, 2, false)]
        public void EqualDistanceUsesOnlyExistingActorIdTie(int candidateId, int bestId, bool selected)
        {
            var facts = new Facts { BestExists = true, ActorId = candidateId, BestId = bestId };
            var host = new TargetHost(facts); float distance = 5;
            Assert.That(WorldHitQueryRules.TrySelectCircle(20, ref host, ref distance), Is.EqualTo(selected)); Assert.That(distance, Is.EqualTo(5));
        }

        [Test] public void ApproximateThresholdAndZeroEpsilonRemainStrict()
        {
            var facts = new Facts { BestExists = true, ActorId = 1, BestId = 2, Target = new WorldHitPoint(2.000003f, 0) };
            var host = new TargetHost(facts); float distance = 2;
            Assert.That(WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.False); Assert.That(distance, Is.EqualTo(2));
            facts.Target = new WorldHitPoint(0, 0); distance = 0;
            Assert.That(WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.True);
            facts.Target = new WorldHitPoint(float.PositiveInfinity, 0); distance = float.PositiveInfinity;
            Assert.That(WorldHitQueryRules.TrySelectBox(ref host, ref distance), Is.False);
        }

        [Test] public void VisibilityIntersectsBoundsAndVisitsFivePointsInOrder()
        {
            var visibility = new Visibility(); var host = new VisibilityHost(visibility); var origin = new WorldHitPoint(-7, 9);
            Assert.That(WorldHitQueryRules.Exposed(origin, new WorldHitBounds(0, 0, 4, 6), new WorldHitBox(3, 4, 4, 6), ref host), Is.False);
            AssertPoints(visibility, new[] { 2.5f, 3.5f, 1.001f, 1.001f, 1.001f, 5.999f, 3.999f, 1.001f, 3.999f, 5.999f });
            foreach (var actual in visibility.Origins) { Assert.That(actual.X, Is.EqualTo(-7)); Assert.That(actual.Y, Is.EqualTo(9)); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void VisibilityStopsAtFirstExposedPoint(int openAt)
        {
            var visibility = new Visibility { OpenAt = openAt }; var host = new VisibilityHost(visibility);
            Assert.That(WorldHitQueryRules.Exposed(new WorldHitPoint(0, 0), new WorldHitBounds(1, 1, 3, 3), new WorldHitBox(2, 2, 2, 2), ref host), Is.True);
            Assert.That(visibility.Points.Count, Is.EqualTo(openAt + 1));
        }

        [Test] public void TouchingBoundsAndNarrowIntersectionsDoNotClampTheInset()
        {
            var visibility = new Visibility(); var host = new VisibilityHost(visibility);
            Assert.That(WorldHitQueryRules.Exposed(new WorldHitPoint(-1, 0), new WorldHitBounds(0, 0, 2, 2), new WorldHitBox(-1, 1, 2, 2), ref host), Is.False);
            AssertPoints(visibility, new[] { 0f, 1f, .001f, .001f, .001f, 1.999f, -.001f, .001f, -.001f, 1.999f });
            visibility.Points.Clear(); visibility.Origins.Clear();
            Assert.That(WorldHitQueryRules.Exposed(new WorldHitPoint(-1, 0), new WorldHitBounds(0, 0, .0005f, .0005f), new WorldHitBox(0, 0, 2, 2), ref host), Is.False);
            AssertPoints(visibility, new[] { .00025f, .00025f, .001f, .001f, .001f, -.0005f, -.0005f, .001f, -.0005f, -.0005f });
        }

        [Test] public void SeparatedBoundsIssueNoLinecastsAndFailuresStopImmediately()
        {
            var visibility = new Visibility(); var host = new VisibilityHost(visibility);
            Assert.That(WorldHitQueryRules.Exposed(new WorldHitPoint(0, 0), new WorldHitBounds(3, 3, 4, 4), new WorldHitBox(0, 0, 2, 2), ref host), Is.False);
            Assert.That(visibility.Points, Is.Empty);
            var failure = new InvalidOperationException("Native linecast failed"); visibility.Failure = failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => WorldHitQueryRules.Exposed(new WorldHitPoint(0, 0), new WorldHitBounds(0, 0, 4, 4), new WorldHitBox(0, 0, 2, 2), ref host)), Is.SameAs(failure));
            Assert.That(visibility.Points.Count, Is.EqualTo(1));
        }

        static void AssertPoints(Visibility visibility, float[] expected)
        {
            Assert.That(visibility.Points.Count * 2, Is.EqualTo(expected.Length));
            for (int i = 0; i < visibility.Points.Count; i++)
            { Assert.That(visibility.Points[i].X, Is.EqualTo(expected[i * 2])); Assert.That(visibility.Points[i].Y, Is.EqualTo(expected[i * 2 + 1])); }
        }
    }
}
