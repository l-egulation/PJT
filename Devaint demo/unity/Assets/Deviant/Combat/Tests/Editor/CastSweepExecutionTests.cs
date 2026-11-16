using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastSweepExecutionTests
    {
        sealed class Target
        {
            public readonly int Id;
            public float X, Y;
            public bool Owner, Player, Decoy, Dead, Visible = true;
            public Target(int id, float x, float y = 0) { Id = id; X = x; Y = y; }
        }
        readonly struct Sample
        {
            public readonly float X, Y, Width, Height;
            public Sample(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        }
        sealed class Attempt
        {
            public float OwnerX, OwnerY;
            public Target Marked;
            public readonly List<Target> Targets = new List<Target>();
            public readonly List<Sample> Boxes = new List<Sample>();
            public readonly List<Sample> Origins = new List<Sample>();
            public readonly List<int> Applied = new List<int>();
            public readonly List<string> Events = new List<string>();
            public Func<int, Sample, List<Target>> Query;
            public Func<Target, bool> Exposure;
            public Action<Target> Applying;
            public Action Sorting;
            public int Queries, Begins;
            public void Run(CastSweepBox? box = null, int direction = 1, float fromX = 0, float fromY = 0, float toX = 0, float toY = 0,
                bool effectCenter = false, bool nearest = false)
            {
                var host = new Host(this);
                CastSweepExecution.Apply(box ?? new CastSweepBox(0, 0, 1, 1), direction, fromX, fromY, toX, toY, effectCenter, nearest, ref host);
            }
        }
        struct Host : ICastSweepHost
        {
            readonly Attempt attempt;
            List<Target> query, candidates;
            public Host(Attempt attempt) { this.attempt = attempt; query = candidates = null; }
            public void Begin() { attempt.Begins++; attempt.Events.Add("begin"); candidates = new List<Target>(); }
            public void DebugBox(float x, float y, float width, float height)
            { attempt.Events.Add("debug"); attempt.Boxes.Add(new Sample(x, y, width, height)); }
            public int QueryBox(float x, float y, float width, float height)
            {
                attempt.Events.Add("query"); var sample = new Sample(x, y, width, height);
                query = attempt.Query == null ? attempt.Targets : attempt.Query(attempt.Queries, sample); attempt.Queries++; return query.Count;
            }
            bool Fact(int i, string name, bool value) { attempt.Events.Add(name + "/" + query[i].Id); return value; }
            public bool IsOwner(int i) => Fact(i, "owner", query[i].Owner);
            public bool IsPlayer(int i) => Fact(i, "player", query[i].Player);
            public bool IsDecoy(int i) => Fact(i, "decoy", query[i].Decoy);
            public bool IsDead(int i) => Fact(i, "dead", query[i].Dead);
            public bool ContainsCandidate(int i) => Fact(i, "duplicate", candidates.Contains(query[i]));
            public bool Exposed(int i, float originX, float originY, float x, float y, float width, float height)
            {
                attempt.Events.Add("exposed/" + query[i].Id); attempt.Origins.Add(new Sample(originX, originY, x, y));
                return attempt.Exposure == null ? query[i].Visible : attempt.Exposure(query[i]);
            }
            public void AddCandidate(int i) { attempt.Events.Add("add/" + query[i].Id); candidates.Add(query[i]); }
            public void SortCandidates()
            {
                attempt.Events.Add("sort"); attempt.Sorting?.Invoke(); var state = attempt;
                candidates.Sort((first, second) => CastSweepExecution.Compare(
                    CastSweepExecution.Distance(state.OwnerX, state.OwnerY, first.X, first.Y), first.Id,
                    CastSweepExecution.Distance(state.OwnerX, state.OwnerY, second.X, second.Y), second.Id));
            }
            public int CandidateCount => candidates.Count;
            public bool MarkedExists { get { attempt.Events.Add("marked"); return attempt.Marked != null; } }
            public bool IsMarkedCandidate(int i) { attempt.Events.Add("is-marked/" + candidates[i].Id); return candidates[i] == attempt.Marked; }
            public void ApplyTarget(int i)
            { var target = candidates[i]; attempt.Events.Add("apply/" + target.Id); attempt.Applied.Add(target.Id); attempt.Applying?.Invoke(target); }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static float After(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) + 1);
        static CastSweepBox Unit => new CastSweepBox(0, 0, 1, 1);

        [Test] public void NullBoxReturnsBeforeAllocationDebugQueriesOrCallbacks()
        {
            var attempt = new Attempt(); var host = new Host(attempt);
            CastSweepExecution.Apply(null, -1, 2, 3, 8, 9, true, true, ref host);
            Assert.That(attempt.Events, Is.Empty); Assert.That(attempt.Begins, Is.Zero);
            Assert.That(attempt.Boxes, Is.Empty); Assert.That(attempt.Applied, Is.Empty);
        }
        [Test] public void StationarySweepStillQueriesBothEndpointsWithSignedLocalOffset()
        {
            foreach (int direction in new[] { -1, 1 })
            {
                var attempt = new Attempt(); attempt.Run(new CastSweepBox(1.25f, -.375f, 2, 3), direction, 8, 4, 8, 4);
                Assert.That(attempt.Queries, Is.EqualTo(2)); Assert.That(attempt.Begins, Is.EqualTo(1));
                foreach (var box in attempt.Boxes)
                {
                    Assert.That(Bits(box.X), Is.EqualTo(Bits(8 + 1.25f * direction))); Assert.That(Bits(box.Y), Is.EqualTo(Bits(3.625f)));
                    Assert.That(box.Width, Is.EqualTo(2)); Assert.That(box.Height, Is.EqualTo(3));
                }
                Assert.That(attempt.Events, Is.EqualTo(new[] { "begin", "debug", "query", "debug", "query", "sort" }));
            }
        }
        [Test] public void SampleCountUsesCeilingAndIncludesBothEndsAtAdjacentFloatBoundaries()
        {
            foreach (float length in new[] { Before(.45f), .45f, After(.45f), Before(.9f), .9f, After(.9f) })
            {
                var attempt = new Attempt(); attempt.Run(Unit, toX: length);
                int expected = length <= .45f ? 2 : length <= .9f ? 3 : 4;
                Assert.That(attempt.Queries, Is.EqualTo(expected), length.ToString("R"));
                Assert.That(Bits(attempt.Boxes[0].X), Is.EqualTo(Bits(0f)));
                Assert.That(Bits(attempt.Boxes[expected - 1].X), Is.EqualTo(Bits(length)));
            }
        }
        [TestCase(.01f, 2f)]
        [TestCase(2f, .01f)]
        [TestCase(.11111111f, 2f)]
        public void SampleSpacingHasPointZeroFiveFloorAndUsesTheSmallerDimension(float width, float height)
        {
            var attempt = new Attempt(); attempt.Run(new CastSweepBox(0, 0, width, height), toX: .2f);
            Assert.That(attempt.Queries, Is.EqualTo(5));
            for (int i = 0; i < attempt.Boxes.Count; i++) Assert.That(Bits(attempt.Boxes[i].X), Is.EqualTo(Bits(.2f * ((float)i / 4))));
        }
        [Test] public void DiagonalDistanceAndLerpKeepFloatOperationOrderRatherThanIncrementalSteps()
        {
            var attempt = new Attempt(); var box = new CastSweepBox(.3125f, -.1875f, .5f, .75f);
            const float fromX = 13.375f, fromY = -7.125f, toX = 13.875f, toY = -6.75f;
            attempt.Run(box, -1, fromX, fromY, toX, toY);
            int steps = (int)Math.Ceiling(.625f / (.5f * .45f)); Assert.That(attempt.Queries, Is.EqualTo(steps + 1));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Assert.That(Bits(attempt.Boxes[i].X), Is.EqualTo(Bits(fromX + (toX - fromX) * t - .3125f)));
                Assert.That(Bits(attempt.Boxes[i].Y), Is.EqualTo(Bits(fromY + (toY - fromY) * t - .1875f)));
            }
            Assert.That(Bits(CastSweepExecution.Distance(fromX, fromY, toX, toY)), Is.EqualTo(Bits(.625f)));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ExposureOriginRetainsSampleYAndUsesEffectCenterOrRootX(bool effectCenter)
        {
            var attempt = new Attempt(); var target = new Target(7, 3); attempt.Targets.Add(target); target.Visible = false;
            attempt.Run(new CastSweepBox(1.5f, 2.25f, 3, 4), -1, 5, -2, 5, -2, effectCenter);
            Assert.That(attempt.Origins.Count, Is.EqualTo(2));
            foreach (var origin in attempt.Origins)
            {
                Assert.That(Bits(origin.X), Is.EqualTo(Bits(effectCenter ? 3.5f : 5f))); Assert.That(Bits(origin.Y), Is.EqualTo(Bits(.25f)));
                Assert.That(Bits(origin.Width), Is.EqualTo(Bits(3.5f))); Assert.That(Bits(origin.Height), Is.EqualTo(Bits(.25f)));
            }
        }
        [Test] public void NativeOrderIsFilteredWithOriginalShortCircuitGatesAndDuplicatesOnlyAfterExposure()
        {
            var attempt = new Attempt(); var owner = new Target(1, 0) { Owner = true }; var player = new Target(2, 0) { Player = true };
            var decoy = new Target(3, 0) { Decoy = true }; var dead = new Target(4, 0) { Dead = true }; var hidden = new Target(5, 1) { Visible = false };
            var first = new Target(6, 2); var second = new Target(7, 3);
            attempt.Targets.AddRange(new[] { owner, player, decoy, dead, hidden, first, first, second }); attempt.Run();
            Assert.That(attempt.Applied, Is.EqualTo(new[] { 6, 7 }));
            Assert.That(attempt.Events.GetRange(3, 24), Is.EqualTo(new[] { "owner/1", "owner/2", "player/2", "owner/3", "player/3", "decoy/3",
                "owner/4", "player/4", "decoy/4", "dead/4", "owner/5", "player/5", "decoy/5", "dead/5", "duplicate/5", "exposed/5",
                "owner/6", "player/6", "decoy/6", "dead/6", "duplicate/6", "exposed/6", "add/6", "owner/6" }));
            Assert.That(attempt.Events.FindAll(item => item == "exposed/5").Count, Is.EqualTo(2));
            Assert.That(attempt.Events.FindAll(item => item == "exposed/6").Count, Is.EqualTo(1));
            Assert.That(attempt.Events.IndexOf("sort"), Is.LessThan(attempt.Events.IndexOf("apply/6")));
        }
        [Test] public void HiddenCandidateCanBecomeExposedOnLaterSamplesAndIsCollectedOnlyOnce()
        {
            var attempt = new Attempt(); var target = new Target(4, 1); attempt.Targets.Add(target); int exposures = 0;
            attempt.Exposure = _ => ++exposures >= 2; attempt.Run(toX: 1);
            Assert.That(exposures, Is.EqualTo(2)); Assert.That(attempt.Applied, Is.EqualTo(new[] { 4 }));
        }
        [Test] public void SortingUsesLiveOwnerCenterAfterCollectionAndExactDistanceBeforeStableActorId()
        {
            var attempt = new Attempt(); attempt.Targets.AddRange(new[] { new Target(8, 1), new Target(3, 3), new Target(5, 2) });
            attempt.Sorting = () => attempt.OwnerX = 4; attempt.Run();
            Assert.That(attempt.Applied, Is.EqualTo(new[] { 3, 5, 8 }));
            Assert.That(CastSweepExecution.Compare(1, 8, 1, 3), Is.GreaterThan(0));
            Assert.That(CastSweepExecution.Compare(After(1), 1, 1, 8), Is.GreaterThan(0), "No Approximately tie is added to a cast sweep.");
            Assert.That(CastSweepExecution.Compare(float.NaN, 8, float.NaN, 3), Is.GreaterThan(0));
        }
        [Test] public void NearestSkipsNonMarkedCandidatesThenBreaksEvenIfApplyDoesNotAcceptDamage()
        {
            var attempt = new Attempt(); var first = new Target(1, 1); var marked = new Target(2, 2); var last = new Target(3, 3);
            attempt.Targets.AddRange(new[] { first, marked, last }); attempt.Marked = marked; attempt.Run(nearest: true);
            Assert.That(attempt.Applied, Is.EqualTo(new[] { 2 }));
            Assert.That(attempt.Events.FindAll(item => item.StartsWith("is-marked/", StringComparison.Ordinal)), Is.EqualTo(new[] { "is-marked/1", "is-marked/2" }));
            attempt.Applied.Clear(); attempt.Marked = new Target(4, 4); attempt.Run(nearest: true); Assert.That(attempt.Applied, Is.Empty);
            attempt.Marked = null; attempt.Run(nearest: true); Assert.That(attempt.Applied, Is.EqualTo(new[] { 1 }));
        }
        [Test] public void MarkedFactsAreReadForEachCandidateRatherThanCapturedAtPassStart()
        {
            var attempt = new Attempt(); var first = new Target(1, 1); var last = new Target(2, 2);
            attempt.Targets.AddRange(new[] { first, last }); attempt.Marked = last;
            attempt.Sorting = () => attempt.Marked = first; attempt.Run(nearest: true);
            Assert.That(attempt.Applied, Is.EqualTo(new[] { 1 }));
        }
        [Test] public void ReceiveChangesDoNotRecheckCollectedCandidateEligibilityExposureOrDistance()
        {
            var attempt = new Attempt(); var first = new Target(1, 1); var last = new Target(2, 2); attempt.Targets.AddRange(new[] { first, last });
            attempt.Applying = target => { if (target == first) { last.Player = last.Decoy = last.Dead = true; last.Visible = false; last.X = -100; attempt.OwnerX = 100; } };
            attempt.Run(); Assert.That(attempt.Applied, Is.EqualTo(new[] { 1, 2 }));
            int beginApply = attempt.Events.IndexOf("apply/1"); Assert.That(attempt.Events.GetRange(beginApply, attempt.Events.Count - beginApply), Is.EqualTo(new[] { "apply/1", "apply/2" }));
        }
        [Test] public void ReentrantReceiveUsesIndependentPerCallCandidatesAndContinuesOuterOrder()
        {
            var attempt = new Attempt(); attempt.Targets.AddRange(new[] { new Target(1, 1), new Target(2, 2), new Target(3, 3) }); bool nested = false;
            attempt.Applying = target => {
                if (nested) return; nested = true; attempt.OwnerX = 4; attempt.Run();
            };
            attempt.Run(); Assert.That(attempt.Applied, Is.EqualTo(new[] { 1, 3, 2, 1, 2, 3 })); Assert.That(attempt.Begins, Is.EqualTo(2));
        }
        [Test] public void ReceiveExceptionPropagatesAfterPartialApplicationWithoutVisitingLaterCandidates()
        {
            var attempt = new Attempt(); attempt.Targets.AddRange(new[] { new Target(1, 1), new Target(2, 2) });
            var error = new InvalidOperationException("Receive boundary"); attempt.Applying = _ => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => attempt.Run()), Is.SameAs(error)); Assert.That(attempt.Applied, Is.EqualTo(new[] { 1 }));
            attempt.Applying = null; attempt.Run(); Assert.That(attempt.Applied, Is.EqualTo(new[] { 1, 1, 2 }));
        }
        [Test] public void QueryAndExposureExceptionsKeepEnteredDebugBoundsWithoutApplyingPartialCollections()
        {
            var attempt = new Attempt(); var error = new InvalidOperationException("Native query boundary");
            attempt.Query = (_, __) => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => attempt.Run()), Is.SameAs(error)); Assert.That(attempt.Boxes.Count, Is.EqualTo(1)); Assert.That(attempt.Applied, Is.Empty);
            attempt.Query = null; attempt.Targets.Add(new Target(1, 1)); attempt.Exposure = _ => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => attempt.Run()), Is.SameAs(error)); Assert.That(attempt.Boxes.Count, Is.EqualTo(2)); Assert.That(attempt.Applied, Is.Empty);
        }
        [Test] public void StructHostMutationsRemainOnTheCallerWithoutBoxingOrAStoredExecutionCopy()
        {
            var host = new ValueHost(); CastSweepExecution.Apply(Unit, 1, 0, 0, 0, 0, false, false, ref host);
            Assert.That(host.Began, Is.EqualTo(1)); Assert.That(host.Queries, Is.EqualTo(2)); Assert.That(host.Applied, Is.EqualTo(1));
        }
        struct ValueHost : ICastSweepHost
        {
            public int Began, Queries, Applied, Collected;
            public void Begin() { Began++; Collected = 0; }
            public void DebugBox(float x, float y, float width, float height) { }
            public int QueryBox(float x, float y, float width, float height) { Queries++; return 1; }
            public bool IsOwner(int i) => false;
            public bool IsPlayer(int i) => false;
            public bool IsDecoy(int i) => false;
            public bool IsDead(int i) => false;
            public bool ContainsCandidate(int i) => Collected != 0;
            public bool Exposed(int i, float originX, float originY, float x, float y, float width, float height) => true;
            public void AddCandidate(int i) { Collected++; }
            public void SortCandidates() { }
            public int CandidateCount => Collected;
            public bool MarkedExists => false;
            public bool IsMarkedCandidate(int i) => false;
            public void ApplyTarget(int i) { Applied++; }
        }
    }
}
