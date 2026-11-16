using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class TimedBombSweepExecutionTests
    {
        sealed class Contact
        {
            public bool Surface = true, OneWay;
            public float MinX = -10, MaxX = 10, MinY = -10, MaxY = 10, Distance;
            public int DistanceReads;
            public readonly List<string> Reads = new List<string>();
        }
        readonly struct Query
        {
            public readonly bool Horizontal;
            public readonly int Direction;
            public readonly float X, Y, Length;
            public Query(bool horizontal, int direction, float x, float y, float length)
            { Horizontal = horizontal; Direction = direction; X = x; Y = y; Length = length; }
        }
        // This consumer uses the existing embedded fuse owner, not a second
        // landing/elapsed-time model. Effects expose already-applied writes.
        sealed class Bomb : ITimedBombHost
        {
            TimedBombExecution timed;
            public float X, Y, VelocityX = 7, VelocityY = -3, Radius = .25f;
            public float FlightLifetime { get; set; } = 5;
            public float FuseSeconds { get; set; } = 1.5f;
            public Contact[] Horizontal = Array.Empty<Contact>(), Vertical = Array.Empty<Contact>();
            public readonly List<string> Events = new List<string>();
            public readonly List<Query> Queries = new List<Query>();
            public readonly List<float> Flights = new List<float>();
            public Action<bool> Queried;
            public Action<bool, int> Selecting;
            public Action<string> AfterEffect;
            public Action<float> DuringFlight;
            public int Snapshots, HorizontalWrites, VerticalWrites, LandCalls, Records, Explosions;
            public TimedBombSnapshot Read() => timed.Read();
            public bool Stopped => timed.Stopped;
            public bool Landed => timed.IsLanded(FuseSeconds);
            public float Remaining => timed.FuseRemaining(FuseSeconds);
            public bool Stop() => timed.Stop();
            public void Step(float duration) => timed.Step(duration, this);
            public void AdvanceFlight(float duration) { Flights.Add(duration); DuringFlight?.Invoke(duration); }
            public void Detonate() { Explosions++; Effect("explode"); }
            public void Land(float duration, float distance, float length)
            { LandCalls++; timed.Land(duration, distance, length); Effect("land"); }
            public void Effect(string name) { Events.Add(name); AfterEffect?.Invoke(name); }
        }
        struct SweepHost : ITimedBombSweepHost
        {
            public Bomb Bomb;
            public float DestinationX, DestinationY, CapturedDeltaX, CapturedDeltaY;
            Contact[] casts;
            Contact current;
            bool horizontal;
            float minX, maxX, minY, maxY;
            public bool Stopped { get { Bomb.Events.Add("stopped"); return Bomb.Stopped; } }
            public bool Landed { get { Bomb.Events.Add("landed"); return Bomb.Landed; } }
            public void SnapshotDelta()
            {
                Bomb.Events.Add("delta"); Bomb.Snapshots++;
                CapturedDeltaX = DestinationX - Bomb.X; CapturedDeltaY = DestinationY - Bomb.Y;
            }
            public float HorizontalLength { get { Bomb.Events.Add("length-x"); return Math.Abs(CapturedDeltaX); } }
            public float VerticalLength { get { Bomb.Events.Add("length-y"); return Math.Abs(CapturedDeltaY); } }
            public float DeltaX => CapturedDeltaX;
            public float DeltaY => CapturedDeltaY;
            public int QueryHorizontal(int direction, float length)
            {
                horizontal = true; casts = Bomb.Horizontal;
                Bomb.Queries.Add(new Query(true, direction, Bomb.X, Bomb.Y, length)); Bomb.Events.Add("query-x");
                Bomb.Queried?.Invoke(true); return casts.Length;
            }
            public int QueryVertical(int direction, float length)
            {
                horizontal = false; casts = Bomb.Vertical;
                Bomb.Queries.Add(new Query(false, direction, Bomb.X, Bomb.Y, length)); Bomb.Events.Add("query-y");
                Bomb.Queried?.Invoke(false); return casts.Length;
            }
            public bool FindSurface(int index)
            {
                current = casts[index]; current.Reads.Add("surface");
                Bomb.Selecting?.Invoke(horizontal, index); return current.Surface;
            }
            public bool SurfaceIsOneWay { get { current.Reads.Add("one-way"); return current.OneWay; } }
            public void SnapshotBounds()
            {
                current.Reads.Add("bounds");
                minX = current.MinX; maxX = current.MaxX; minY = current.MinY; maxY = current.MaxY;
            }
            public float PositionX { get { Bomb.Events.Add("position-x"); return Bomb.X; } }
            public float PositionY { get { Bomb.Events.Add("position-y"); return Bomb.Y; } }
            public float Radius { get { Bomb.Events.Add("radius"); return Bomb.Radius; } }
            public float BoundsMinX { get { current.Reads.Add("min-x"); return minX; } }
            public float BoundsMaxX { get { current.Reads.Add("max-x"); return maxX; } }
            public float BoundsMinY { get { current.Reads.Add("min-y"); return minY; } }
            public float BoundsMaxY { get { current.Reads.Add("max-y"); return maxY; } }
            public float CastDistance { get { current.Reads.Add("distance"); current.DistanceReads++; return current.Distance; } }
            public void ApplyHorizontal(int direction, float distance)
            { Bomb.X += direction * distance; Bomb.HorizontalWrites++; Bomb.Effect("move-x"); }
            public void ClearHorizontalVelocity() { Bomb.VelocityX = 0; Bomb.Effect("stop-x"); }
            public void ApplyVertical(int direction, float distance)
            { Bomb.Y += direction * distance; Bomb.VerticalWrites++; Bomb.Effect("move-y"); }
            public void ClearVerticalVelocity() { Bomb.VelocityY = 0; Bomb.Effect("stop-y"); }
            public void Land(float duration, float contactDistance, float sweepLength) => Bomb.Land(duration, contactDistance, sweepLength);
            public void ClearVelocity() { Bomb.VelocityX = Bomb.VelocityY = 0; Bomb.Effect("clear-velocity"); }
            public void RecordLanding() { Bomb.Records++; Bomb.Effect("record"); }
        }
        static SweepHost Create(Bomb bomb, float x, float y)
            => new SweepHost { Bomb = bomb, DestinationX = x, DestinationY = y };
        static Contact Wall(int direction = 1, float distance = 2)
            => new Contact { MinX = direction > 0 ? 2 : -2.5f, MaxX = direction > 0 ? 2.5f : -2,
                MinY = -2, MaxY = 2, Distance = distance };
        static Contact Floor(float distance = 1)
            => new Contact { MinY = -1.75f, MaxY = -1.25f, Distance = distance };
        static Contact Ceiling(float distance = 1.75f)
            => new Contact { MinY = 2, MaxY = 2.5f, Distance = distance };

        [Test] public void StoppedEntryDoesNotReadLandingOrAnyMotionFacts()
        {
            var bomb = new Bomb(); bomb.Stop(); var host = Create(bomb, 5, -4);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Events, Is.EqualTo(new[] { "stopped" })); Assert.That(bomb.Queries, Is.Empty);
            Assert.That(bomb.Snapshots, Is.Zero); Assert.That(bomb.X, Is.Zero); Assert.That(bomb.Y, Is.Zero);
        }
        [Test] public void LandedEntryDoesNotCaptureOrQueryTheRequestedDestination()
        {
            var bomb = new Bomb(); bomb.Land(0, 0, 1); bomb.Events.Clear(); var before = bomb.Read();
            var host = Create(bomb, 5, -4); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Events, Is.EqualTo(new[] { "stopped", "landed" })); Assert.That(bomb.Snapshots, Is.Zero);
            Assert.That(bomb.Read().Age, Is.EqualTo(before.Age)); Assert.That(bomb.Read().LandedAt, Is.EqualTo(before.LandedAt));
            Assert.That(bomb.HorizontalWrites + bomb.VerticalWrites, Is.Zero);
        }
        [TestCase(0f, 0f, 0, 0)]
        [TestCase(.000001f, -.000001f, 0, 0)]
        [TestCase(0f, 2f, 0, 1)]
        [TestCase(-2f, 0f, 1, 0)]
        [TestCase(.000002f, -.000002f, 1, 1)]
        public void EachAxisHonorsItsOwnInclusiveTinyMovementBoundary(float x, float y, int horizontal, int vertical)
        {
            var bomb = new Bomb(); var host = Create(bomb, x, y); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Snapshots, Is.EqualTo(1)); Assert.That(bomb.Queries.Count, Is.EqualTo(horizontal + vertical));
            Assert.That(bomb.HorizontalWrites, Is.EqualTo(horizontal)); Assert.That(bomb.VerticalWrites, Is.EqualTo(vertical));
            Assert.That(bomb.X, Is.EqualTo(horizontal == 0 ? 0 : x)); Assert.That(bomb.Y, Is.EqualTo(vertical == 0 ? 0 : y));
            Assert.That(bomb.VelocityX, Is.EqualTo(7)); Assert.That(bomb.VelocityY, Is.EqualTo(-3)); Assert.That(bomb.LandCalls, Is.Zero);
        }
        [TestCase(3f, 4f, 1, 1)] [TestCase(-3f, -4f, -1, -1)]
        public void EmptyAxesKeepTheEnteredDeltaAndQueryYOnlyAfterApplyingX(float x, float y, int horizontal, int vertical)
        {
            var bomb = new Bomb(); var host = Create(bomb, x, y); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Queries.Count, Is.EqualTo(2));
            Assert.That(bomb.Queries[0].Horizontal, Is.True); Assert.That(bomb.Queries[0].Direction, Is.EqualTo(horizontal));
            Assert.That(bomb.Queries[0].X, Is.Zero); Assert.That(bomb.Queries[0].Y, Is.Zero);
            Assert.That(bomb.Queries[0].Length, Is.EqualTo(Math.Abs(x)));
            Assert.That(bomb.Queries[1].Horizontal, Is.False); Assert.That(bomb.Queries[1].Direction, Is.EqualTo(vertical));
            Assert.That(bomb.Queries[1].X, Is.EqualTo(x)); Assert.That(bomb.Queries[1].Y, Is.Zero);
            Assert.That(bomb.Queries[1].Length, Is.EqualTo(Math.Abs(y)));
            Assert.That(bomb.X, Is.EqualTo(x)); Assert.That(bomb.Y, Is.EqualTo(y));
            Assert.That(bomb.Events.IndexOf("move-x"), Is.LessThan(bomb.Events.IndexOf("query-y")));
        }
        [TestCase(1, 2f, 1.749f)] [TestCase(-1, 2f, 1.749f)]
        [TestCase(1, 1f, .999f)] [TestCase(-1, 1f, .999f)]
        public void WallsUseTheLesserOfTheNativeContactAndExternalFaceBeforeTheMargin(int direction, float distance, float travel)
        {
            var bomb = new Bomb { Horizontal = new[] { Wall(direction, distance) } }; var host = Create(bomb, direction * 5, 0);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.X, Is.EqualTo(direction * travel).Within(.000001f)); Assert.That(bomb.Y, Is.Zero);
            Assert.That(bomb.VelocityX, Is.Zero); Assert.That(bomb.VelocityY, Is.EqualTo(-3));
            Assert.That(bomb.LandCalls, Is.Zero); Assert.That(bomb.Records, Is.Zero);
        }
        [Test] public void HorizontalComparisonUsesTheAlreadyMarginReducedAllowanceNotANewNearestDistance()
        {
            var first = new Contact { MinX = 3, MaxX = 3.5f, Distance = 2 };
            var later = new Contact { MinX = 3, MaxX = 3.5f, Distance = 1.9995f };
            var bomb = new Bomb { Horizontal = new[] { first, later } }; var host = Create(bomb, 5, 0);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.X, Is.EqualTo(1.999f).Within(.000001f)); Assert.That(bomb.VelocityX, Is.Zero);
            Assert.That(first.DistanceReads, Is.EqualTo(1)); Assert.That(later.DistanceReads, Is.EqualTo(1));
        }
        [TestCase(1)] [TestCase(-1)]
        public void WallFacesInsideTheRadiusClampToNoTravelRatherThanBackingIntoTheBomb(int direction)
        {
            var contact = Wall(direction); contact.MinX = direction > 0 ? .1f : -.5f; contact.MaxX = direction > 0 ? .5f : -.1f;
            var bomb = new Bomb { Horizontal = new[] { contact } }; var host = Create(bomb, direction * 5, 0);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.X, Is.Zero); Assert.That(bomb.HorizontalWrites, Is.EqualTo(1)); Assert.That(bomb.VelocityX, Is.Zero);
        }
        [TestCase(1)] [TestCase(-1)]
        public void HorizontalQualificationIgnoresEnemiesOneWayVerticalEdgesAndFacesBehindTheCurrentCentre(int direction)
        {
            var enemy = new Contact { Surface = false };
            var oneWay = Wall(direction); oneWay.OneWay = true;
            var belowEdge = Wall(direction); belowEdge.MinY = .25f;
            var aboveEdge = Wall(direction); aboveEdge.MaxY = -.25f;
            var behind = Wall(direction); behind.MinX = -.5f; behind.MaxX = .5f;
            var bomb = new Bomb { Horizontal = new[] { enemy, oneWay, belowEdge, aboveEdge, behind } };
            var host = Create(bomb, direction * 5, 0); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.X, Is.EqualTo(direction * 5)); Assert.That(bomb.VelocityX, Is.EqualTo(7));
            Assert.That(enemy.Reads, Is.EqualTo(new[] { "surface" })); Assert.That(oneWay.Reads, Is.EqualTo(new[] { "surface", "one-way" }));
            Assert.That(belowEdge.Reads, Does.Not.Contain("max-y"));
            foreach (var contact in bomb.Horizontal) Assert.That(contact.DistanceReads, Is.Zero);
        }
        [Test] public void CeilingStopsOnlyUpwardVelocityWithoutArmingTheLandingFuse()
        {
            var bomb = new Bomb { VelocityY = 10, Vertical = new[] { Ceiling() } }; var host = Create(bomb, 1, 4);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.X, Is.EqualTo(1)); Assert.That(bomb.Y, Is.EqualTo(1.749f).Within(.000001f));
            Assert.That(bomb.VelocityX, Is.EqualTo(7)); Assert.That(bomb.VelocityY, Is.Zero);
            Assert.That(bomb.Landed, Is.False); Assert.That(bomb.Remaining, Is.Zero); Assert.That(bomb.LandCalls, Is.Zero);
            Assert.That(bomb.Records, Is.Zero); Assert.That(bomb.Events, Does.Not.Contain("clear-velocity"));
        }
        [Test] public void DownwardContactArmsTheExistingOwnerAtTheFractionalFlightTimeBeforeZeroingAndRecording()
        {
            var bomb = new Bomb { Vertical = new[] { Floor() } }; bomb.Step(.01f); bomb.Flights.Clear();
            bomb.DuringFlight = duration => { var host = Create(bomb, 1, -4); TimedBombSweepExecution.Sweep(ref host, duration); };
            bomb.Step(.02f);
            float landedAt = .01f + .02f * (1f / 4f);
            Assert.That(bomb.Read().LandedAt, Is.EqualTo(landedAt)); Assert.That(bomb.Read().Age, Is.EqualTo(.01f + .02f));
            Assert.That(bomb.Remaining, Is.EqualTo(landedAt + 1.5f - bomb.Read().Age));
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f })); Assert.That(bomb.X, Is.EqualTo(1));
            Assert.That(bomb.Y, Is.EqualTo(-.999f).Within(.000001f)); Assert.That(bomb.VelocityX, Is.Zero); Assert.That(bomb.VelocityY, Is.Zero);
            Assert.That(bomb.LandCalls, Is.EqualTo(1)); Assert.That(bomb.Records, Is.EqualTo(1));
            Assert.That(bomb.Events.IndexOf("move-y"), Is.LessThan(bomb.Events.IndexOf("stop-y")));
            Assert.That(bomb.Events.IndexOf("stop-y"), Is.LessThan(bomb.Events.IndexOf("land")));
            Assert.That(bomb.Events.IndexOf("land"), Is.LessThan(bomb.Events.IndexOf("clear-velocity")));
            Assert.That(bomb.Events.IndexOf("clear-velocity"), Is.LessThan(bomb.Events.IndexOf("record")));
            bomb.Step(.1f); Assert.That(bomb.Flights.Count, Is.EqualTo(1)); Assert.That(bomb.Explosions, Is.Zero);
            bomb.Step(2); Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(bomb.Flights.Count, Is.EqualTo(1));
        }
        [Test] public void ExternalZeroDurationLandingUsesTheCurrentExistingAgeWithoutAdvancingASecondClock()
        {
            var bomb = new Bomb { Vertical = new[] { Floor() } }; bomb.Step(.1f); float age = bomb.Read().Age;
            var host = Create(bomb, 0, -4); TimedBombSweepExecution.Sweep(ref host, 0);
            Assert.That(bomb.Read().Age, Is.EqualTo(age)); Assert.That(bomb.Read().LandedAt, Is.EqualTo(age));
            Assert.That(bomb.Remaining, Is.EqualTo(age + 1.5f - age)); Assert.That(bomb.LandCalls, Is.EqualTo(1));
        }
        [TestCase(.25f, true)] [TestCase(.249f, false)] [TestCase(.5f, true)]
        public void DownwardOneWayContactRequiresTheCurrentBottomAtTheInclusiveTopMargin(float y, bool expected)
        {
            var platform = new Contact { OneWay = true, MinY = -.25f, MaxY = .03f, Distance = .02f };
            var bomb = new Bomb { Y = y, Vertical = new[] { platform } }; var host = Create(bomb, 0, y - 1);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Landed, Is.EqualTo(expected)); Assert.That(bomb.LandCalls, Is.EqualTo(expected ? 1 : 0));
            Assert.That(platform.DistanceReads, Is.EqualTo(expected ? 3 : 0));
            Assert.That(bomb.Y, Is.EqualTo(y - (expected ? .019f : 1)).Within(.000001f));
        }
        [Test] public void UpwardOneWayContactIsIgnoredBeforeReadingTheBottomMargin()
        {
            var platform = new Contact { OneWay = true, MinY = -.25f, MaxY = .03f, Distance = .02f };
            var bomb = new Bomb { Y = -.5f, Vertical = new[] { platform } }; var host = Create(bomb, 0, .5f);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Y, Is.EqualTo(.5f)); Assert.That(bomb.VelocityY, Is.EqualTo(-3)); Assert.That(bomb.LandCalls, Is.Zero);
            Assert.That(platform.DistanceReads, Is.Zero); Assert.That(platform.Reads, Does.Not.Contain("max-y"));
        }
        [TestCase(-1)] [TestCase(1)]
        public void VerticalQualificationRejectsEnemiesHorizontalEdgesAndFacesOnTheWrongSide(int direction)
        {
            var enemy = new Contact { Surface = false };
            var leftEdge = direction < 0 ? Floor() : Ceiling(); leftEdge.MaxX = -.25f;
            var rightEdge = direction < 0 ? Floor() : Ceiling(); rightEdge.MinX = .25f;
            var behind = new Contact { MinY = -.5f, MaxY = .5f };
            var beyond = direction < 0 ? Floor(5) : Ceiling(5);
            var bomb = new Bomb { Vertical = new[] { enemy, leftEdge, rightEdge, behind, beyond } };
            var host = Create(bomb, 0, direction * 4); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Y, Is.EqualTo(direction * 4)); Assert.That(bomb.VelocityY, Is.EqualTo(-3)); Assert.That(bomb.LandCalls, Is.Zero);
            Assert.That(enemy.Reads, Is.EqualTo(new[] { "surface" })); Assert.That(rightEdge.Reads, Does.Not.Contain("max-x"));
            Assert.That(leftEdge.DistanceReads + rightEdge.DistanceReads + behind.DistanceReads, Is.Zero);
            Assert.That(beyond.DistanceReads, Is.EqualTo(1)); Assert.That(behind.Reads, Does.Not.Contain("one-way"));
        }
        [Test] public void VerticalNearestSelectionKeepsNativeOrderAndLetsTheLastEqualContactWriteTravel()
        {
            var far = Floor(3); var first = Floor(1); var tie = Floor(1); var later = Floor(2);
            var bomb = new Bomb { Vertical = new[] { far, first, tie, later } }; var host = Create(bomb, 0, -4);
            TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Y, Is.EqualTo(-.999f).Within(.000001f)); Assert.That(bomb.Read().LandedAt, Is.EqualTo(.02f * (1f / 4f)));
            Assert.That(far.DistanceReads, Is.EqualTo(3)); Assert.That(first.DistanceReads, Is.EqualTo(3));
            Assert.That(tie.DistanceReads, Is.EqualTo(3)); Assert.That(later.DistanceReads, Is.EqualTo(1));
        }
        [Test] public void VerticalContactsAtTheEnteredLengthAreInclusiveAndNegativeDistancesClampBeforeLanding()
        {
            foreach (float distance in new[] { 4f, -.5f })
            {
                var bomb = new Bomb { Vertical = new[] { Floor(distance) } }; var host = Create(bomb, 0, -4);
                TimedBombSweepExecution.Sweep(ref host, .02f);
                Assert.That(bomb.Landed, Is.True); Assert.That(bomb.Y, Is.EqualTo(distance < 0 ? 0 : -3.999f).Within(.000001f));
                Assert.That(bomb.Read().LandedAt, Is.EqualTo(distance < 0 ? 0 : .02f));
            }
        }
        [Test] public void QueryCallbacksReevaluateCurrentPositionRadiusAndBoundsWithoutRecapturingTheDelta()
        {
            var wall = new Contact { MinX = 11.25f, MaxX = 11.75f, MinY = 19, MaxY = 21, Distance = 2 };
            var ceiling = Ceiling();
            var bomb = new Bomb { X = 1, Y = 2, Horizontal = new[] { wall }, Vertical = new[] { ceiling } };
            bomb.Queried = horizontal => {
                if (horizontal) { bomb.X = 10; bomb.Y = 20; }
                else { bomb.X = 50; bomb.Y = 60; bomb.Radius = .25f; ceiling.MinX = 49; ceiling.MaxX = 51; ceiling.MinY = 62; ceiling.MaxY = 62.5f; }
            };
            bomb.AfterEffect = name => { if (name == "stop-x") { bomb.X = 30; bomb.Y = 40; bomb.Radius = .5f; } };
            var host = Create(bomb, 4, 6); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(host.CapturedDeltaX, Is.EqualTo(3)); Assert.That(host.CapturedDeltaY, Is.EqualTo(4)); Assert.That(bomb.Snapshots, Is.EqualTo(1));
            Assert.That(bomb.Queries[0].X, Is.EqualTo(1)); Assert.That(bomb.Queries[0].Y, Is.EqualTo(2));
            Assert.That(bomb.Queries[1].X, Is.EqualTo(30)); Assert.That(bomb.Queries[1].Y, Is.EqualTo(40));
            Assert.That(bomb.X, Is.EqualTo(50)); Assert.That(bomb.Y, Is.EqualTo(61.749f).Within(.00001f));
            Assert.That(bomb.VelocityX, Is.Zero); Assert.That(bomb.VelocityY, Is.Zero); Assert.That(bomb.LandCalls, Is.Zero);
        }
        [Test] public void LaterCandidatesReadTheirLiveSurfaceAndBoundsInsteadOfAnUpFrontFactCopy()
        {
            var first = Floor(3); var later = Floor(1); later.Surface = false; later.MinX = 50; later.MaxX = 51;
            var bomb = new Bomb { Vertical = new[] { first, later } };
            bomb.Selecting = (horizontal, index) => { if (index == 0) { later.Surface = true; later.MinX = -10; later.MaxX = 10; } };
            var host = Create(bomb, 0, -4); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Y, Is.EqualTo(-.999f).Within(.000001f)); Assert.That(later.DistanceReads, Is.EqualTo(3));
            Assert.That(bomb.Read().LandedAt, Is.EqualTo(.02f * (1f / 4f)));
        }
        [Test] public void EnteredSweepContinuesYAfterANativeStopButTheExistingOwnerDeclinesLanding()
        {
            var floor = Floor(.5f); floor.MinY = 18.5f; floor.MaxY = 19;
            var bomb = new Bomb { Vertical = new[] { floor } };
            bomb.AfterEffect = name => { if (name == "move-x") { bomb.Stop(); bomb.Y = 20; } };
            var host = Create(bomb, 1, -4); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.Stopped, Is.True); Assert.That(bomb.Landed, Is.False); Assert.That(bomb.LandCalls, Is.EqualTo(1));
            Assert.That(bomb.Y, Is.EqualTo(19.501f).Within(.00001f)); Assert.That(bomb.Queries[1].Y, Is.EqualTo(20));
            Assert.That(bomb.VelocityX, Is.Zero); Assert.That(bomb.VelocityY, Is.Zero); Assert.That(bomb.Records, Is.EqualTo(1));
            TimedBombSweepExecution.Sweep(ref host, .02f); Assert.That(bomb.Snapshots, Is.EqualTo(1)); Assert.That(bomb.Records, Is.EqualTo(1));
        }
        [Test] public void NestedSweepsKeepIndependentDeltaAndNativeCandidateArrays()
        {
            var bomb = new Bomb { Horizontal = new[] { Wall(1, 1) } }; bool nested = false;
            bomb.Queried = horizontal => {
                if (nested) return; nested = true; bomb.Horizontal = Array.Empty<Contact>();
                var inner = Create(bomb, -2, 0); TimedBombSweepExecution.Sweep(ref inner, .01f);
            };
            var host = Create(bomb, 5, 0); TimedBombSweepExecution.Sweep(ref host, .02f);
            Assert.That(bomb.X, Is.EqualTo(-1.001f).Within(.000001f)); Assert.That(bomb.VelocityX, Is.Zero);
            Assert.That(host.CapturedDeltaX, Is.EqualTo(5)); Assert.That(bomb.HorizontalWrites, Is.EqualTo(2)); Assert.That(bomb.Snapshots, Is.EqualTo(2));
        }
        [TestCase(true)] [TestCase(false)]
        public void QualificationFailureDoesNotCommitTheCurrentAxisButKeepsAnyCompletedXResponse(bool horizontalFailure)
        {
            var bomb = new Bomb { Horizontal = horizontalFailure ? new[] { Wall(), Wall() } : new[] { Wall() },
                Vertical = new[] { Floor(), Floor(2) } };
            bomb.Selecting = (horizontal, index) => { if (horizontal == horizontalFailure && index == 1) throw new InvalidOperationException("qualification"); };
            var host = Create(bomb, 5, -4);
            Assert.Throws<InvalidOperationException>(() => TimedBombSweepExecution.Sweep(ref host, .02f));
            Assert.That(bomb.X, Is.EqualTo(horizontalFailure ? 0 : 1.749f).Within(.000001f)); Assert.That(bomb.Y, Is.Zero);
            Assert.That(bomb.VelocityX, Is.EqualTo(horizontalFailure ? 7 : 0)); Assert.That(bomb.VelocityY, Is.EqualTo(-3));
            Assert.That(bomb.HorizontalWrites, Is.EqualTo(horizontalFailure ? 0 : 1)); Assert.That(bomb.VerticalWrites, Is.Zero);
            Assert.That(bomb.LandCalls, Is.Zero); Assert.That(bomb.Records, Is.Zero); Assert.That(bomb.Stopped, Is.False);
        }
        [TestCase("move-x", false, false, -3f)]
        [TestCase("stop-x", true, false, -3f)]
        [TestCase("move-y", true, true, -3f)]
        [TestCase("stop-y", true, true, 0f)]
        public void NativeWriteFailurePreservesOnlyTheEffectsReachedBeforeIt(string failure, bool xStopped, bool yMoved, float velocityY)
        {
            var bomb = new Bomb { Horizontal = new[] { Wall() }, Vertical = new[] { Floor() } };
            bomb.AfterEffect = name => { if (name == failure) throw new InvalidOperationException(failure); };
            var host = Create(bomb, 5, -4);
            Assert.Throws<InvalidOperationException>(() => TimedBombSweepExecution.Sweep(ref host, .02f));
            Assert.That(bomb.X, Is.EqualTo(1.749f).Within(.000001f)); Assert.That(bomb.Y, Is.EqualTo(yMoved ? -.999f : 0).Within(.000001f));
            Assert.That(bomb.VelocityX, Is.EqualTo(xStopped ? 0 : 7)); Assert.That(bomb.VelocityY, Is.EqualTo(velocityY));
            Assert.That(bomb.HorizontalWrites, Is.EqualTo(1)); Assert.That(bomb.VerticalWrites, Is.EqualTo(yMoved ? 1 : 0));
            Assert.That(bomb.Landed, Is.False); Assert.That(bomb.LandCalls, Is.Zero); Assert.That(bomb.Records, Is.Zero);
        }
        [TestCase("land", 7f, 0)] [TestCase("clear-velocity", 0f, 0)] [TestCase("record", 0f, 1)]
        public void LandingFailureKeepsTheExistingFractionalFuseAndAlreadyAppliedNativeWrites(string failure, float velocityX, int records)
        {
            var bomb = new Bomb { Vertical = new[] { Floor() } };
            bomb.AfterEffect = name => { if (name == failure) throw new InvalidOperationException(failure); };
            bomb.DuringFlight = duration => { var host = Create(bomb, 1, -4); TimedBombSweepExecution.Sweep(ref host, duration); };
            Assert.Throws<InvalidOperationException>(() => bomb.Step(.02f));
            Assert.That(bomb.X, Is.EqualTo(1)); Assert.That(bomb.Y, Is.EqualTo(-.999f).Within(.000001f));
            Assert.That(bomb.VelocityX, Is.EqualTo(velocityX)); Assert.That(bomb.VelocityY, Is.Zero);
            Assert.That(bomb.Read().Age, Is.Zero); Assert.That(bomb.Read().LandedAt, Is.EqualTo(.02f * (1f / 4f)));
            Assert.That(bomb.Landed, Is.True); Assert.That(bomb.LandCalls, Is.EqualTo(1)); Assert.That(bomb.Records, Is.EqualTo(records));
            Assert.That(bomb.Stopped, Is.False); Assert.That(bomb.Explosions, Is.Zero);
        }
    }
}
