using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.World.Contracts;
using Deviant.World.Streaming;
using NUnit.Framework;

namespace Deviant.World.Tests
{
    public class StreamingPlannerTests
    {
        [Test] public void PlanningAssembliesDoNotReferenceUnityCombatOrUi()
        {
            foreach (var assembly in new[] { typeof(StreamingWindow).Assembly, typeof(RepeatingStripPlanner).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("Deviant.Combat") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [TestCase(-1, new[] { 0, 1, 2 })]
        [TestCase(4.6875f, new[] { 0, 1, 2 })]
        [TestCase(63.999f, new[] { 0, 1, 2 })]
        [TestCase(64, new[] { 0, 1, 2, 3 })]
        [TestCase(511.999f, new[] { 6, 7, 8, 9 })]
        [TestCase(512, new[] { 7, 8, 9, 10 })]
        [TestCase(520, new[] { 7, 8, 9, 10 })]
        public void PrototypeWindowPreservesItsDocumentedBoundaries(float x, int[] expected)
        {
            var plan = new RepeatingStripPlanner(64, 8, 1, 2).Plan(x);
            Assert.That(plan.Placements.Select(p => p.Index), Is.EqualTo(expected));
            foreach (int index in expected) Assert.That(plan.Contains(index), Is.True);
            Assert.That(plan.Contains(-1), Is.False); Assert.That(plan.Contains(expected.Last() + 1), Is.False);
        }
        [Test] public void RepeatedTemplateKeepsDifferentPhysicalPlacementIdentity()
        {
            var planner = new RepeatingStripPlanner(64, 8, 1, 2);
            var zero = planner.Place(0); var eighth = planner.Place(8); var last = planner.Place(7);
            Assert.That(zero.ModuleIndex, Is.Zero); Assert.That(eighth.ModuleIndex, Is.Zero);
            Assert.That(zero.SourceX, Is.Zero); Assert.That(eighth.SourceX, Is.Zero);
            Assert.That(zero.WorldX, Is.Zero); Assert.That(eighth.WorldX, Is.EqualTo(512));
            Assert.That(zero.Index, Is.Not.EqualTo(eighth.Index));
            Assert.That(last.ModuleIndex, Is.EqualTo(7)); Assert.That(last.SourceX, Is.EqualTo(448));
            Assert.That(planner.Place(8), Is.EqualTo(eighth), "View load order cannot change placement identity.");
        }
        [Test] public void ConfiguredFixtureDoesNotInheritEightRoomsOrPrototypeDimensions()
        {
            var planner = new RepeatingStripPlanner(30, 3, 1, 0);
            var plan = planner.Plan(95);
            Assert.That(plan.Placements.Select(p => p.Index), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(plan.Placements.Select(p => p.SourceX), Is.EqualTo(new[] { 60f, 0f }));
            Assert.That(plan.Placements.Select(p => p.WorldX), Is.EqualTo(new[] { 60f, 90f }));
        }
        [Test] public void WindowsAreImmutableAndRejectedInputCannotReplaceCachedPlan()
        {
            var planner = new RepeatingStripPlanner(64, 8, 1, 2); var initial = planner.Plan(4.6875f);
            var copy = initial.Placements.ToArray(); var frozen = new StreamingWindow(copy);
            copy[0] = planner.Place(100);
            Assert.That(frozen.Placements[0].Index, Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((IList<ChunkPlacement>)frozen.Placements)[0] = planner.Place(50));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
                Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(invalid));
            Assert.That(planner.Plan(5), Is.SameAs(initial), "A stationary view window should not allocate another plan.");
            planner.Plan(520); Assert.That(initial.Placements.Select(p => p.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Place(-1));
            Assert.Throws<ArgumentException>(() => new StreamingWindow(new[] { planner.Place(0), planner.Place(0) }));
            Assert.Throws<ArgumentNullException>(() => new StreamingWindow(null));
        }
        [Test] public void InvalidPlannerConfigurationsHaveNoUsablePartialPolicy()
        {
            foreach (float width in new[] { 0, -1, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatingStripPlanner(width, 8, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatingStripPlanner(float.MaxValue, 8, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatingStripPlanner(64, 0, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatingStripPlanner(64, 8, -1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatingStripPlanner(64, 8, 1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatingStripPlanner(64, 8, int.MaxValue, 1));
        }
    }
}
