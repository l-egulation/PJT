using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ContactBombSweepExecutionTests
    {
        sealed class Candidate
        {
            public bool Surface, OneWay, Region = true, Actor = true, Owner, Player, Decoy, Dead;
            public float Top;
            public readonly List<string> Reads = new List<string>();
        }
        sealed class Contact
        {
            public Candidate Candidate = Solid();
            public float Distance, NormalY = 1;
            public int DistanceReads;
        }
        sealed class State
        {
            public float X, Y, Radius = .25f, Fuse;
            public bool Stopped;
            public int Explosions, TimedSweeps;
            public Candidate[] Overlaps = Array.Empty<Candidate>();
            public Contact[] Contacts = Array.Empty<Contact>();
            public readonly List<string> Events = new List<string>();
            public Action OverlapsQueried, CastQueried, TravelApplied, Exploding, TimedSwept;
            public Action<int> CastSelected;
        }
        struct ObstacleHost : IContactBombObstacleHost
        {
            public State State;
            public Candidate Candidate;
            public float DeltaY, HitNormalY;
            public bool FindSurface() { Candidate.Reads.Add("surface"); return Candidate.Surface; }
            public bool SurfaceIsOneWay { get { Candidate.Reads.Add("one-way"); return Candidate.OneWay; } }
            public float MovementY { get { Candidate.Reads.Add("movement"); return DeltaY; } }
            public float NormalY { get { Candidate.Reads.Add("normal"); return HitNormalY; } }
            public float BombBottom { get { Candidate.Reads.Add("bottom"); return State.Y - State.Radius; } }
            public float SurfaceTop { get { Candidate.Reads.Add("top"); return Candidate.Top; } }
            public bool FindRegion() { Candidate.Reads.Add("region"); return Candidate.Region; }
            public bool TargetExists { get { Candidate.Reads.Add("actor"); return Candidate.Actor; } }
            public bool TargetIsOwner { get { Candidate.Reads.Add("owner"); return Candidate.Owner; } }
            public bool TargetIsPlayer { get { Candidate.Reads.Add("player"); return Candidate.Player; } }
            public bool TargetIsDecoy { get { Candidate.Reads.Add("decoy"); return Candidate.Decoy; } }
            public bool TargetDead { get { Candidate.Reads.Add("dead"); return Candidate.Dead; } }
        }
        struct SweepHost : IContactBombSweepHost
        {
            public State State;
            public float DestinationX, DestinationY, DeltaX, DeltaY;
            public int Snapshots, OverlapQueries, CastQueries, TravelWrites;
            Contact current;
            public bool Stopped { get { State.Events.Add("stopped"); return State.Stopped; } }
            public float FuseSeconds { get { State.Events.Add("fuse"); return State.Fuse; } }
            public void SweepTimed() { State.Events.Add("timed"); State.TimedSweeps++; State.TimedSwept?.Invoke(); }
            public void SnapshotDelta()
            {
                State.Events.Add("delta"); Snapshots++;
                DeltaX = DestinationX - State.X; DeltaY = DestinationY - State.Y;
            }
            public int QueryOverlaps()
            {
                State.Events.Add("overlaps"); OverlapQueries++; State.OverlapsQueried?.Invoke(); return State.Overlaps.Length;
            }
            public bool OverlapIsObstacle(int index)
            {
                State.Events.Add("overlap:" + index); return Qualifies(State.Overlaps[index], 1);
            }
            public float DeltaLength
            {
                get { State.Events.Add("length"); return (float)Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY); }
            }
            public int QueryCast(float length)
            {
                State.Events.Add("cast"); CastQueries++; State.CastQueried?.Invoke(); return State.Contacts.Length;
            }
            public bool CastIsObstacle(int index)
            {
                State.Events.Add("contact:" + index); current = State.Contacts[index]; State.CastSelected?.Invoke(index);
                return Qualifies(current.Candidate, current.NormalY);
            }
            bool Qualifies(Candidate candidate, float normalY)
            {
                var obstacle = new ObstacleHost { State = State, Candidate = candidate, DeltaY = DeltaY, HitNormalY = normalY };
                return ContactBombSweepExecution.IsObstacle(ref obstacle);
            }
            public float CastDistance
            {
                get { State.Events.Add("distance"); current.DistanceReads++; return current.Distance; }
            }
            public void ApplyTravel(float length, float distance)
            {
                State.Events.Add("travel"); TravelWrites++;
                State.X += DeltaX / length * distance; State.Y += DeltaY / length * distance;
                State.TravelApplied?.Invoke();
            }
            public void Explode()
            {
                if (State.Stopped) return;
                State.Stopped = true; State.Events.Add("explode"); State.Explosions++; State.Exploding?.Invoke();
            }
        }
        static Candidate Solid() => new Candidate { Surface = true };
        static SweepHost Create(float destinationX = 5, float destinationY = 0)
            => new SweepHost { State = new State(), DestinationX = destinationX, DestinationY = destinationY };
        static bool Qualifies(Candidate candidate, float movementY = -1, float normalY = 1, float bottom = 0)
        {
            var host = new ObstacleHost { State = new State { Y = bottom + .25f }, Candidate = candidate,
                DeltaY = movementY, HitNormalY = normalY };
            return ContactBombSweepExecution.IsObstacle(ref host);
        }

        [Test] public void SolidSurfacePreemptsActorFactsAndDoesNotReadOneWayGeometry()
        {
            var candidate = Solid(); candidate.Owner = candidate.Player = candidate.Dead = true;
            Assert.That(Qualifies(candidate, 1, 0), Is.True);
            Assert.That(candidate.Reads, Is.EqualTo(new[] { "surface", "one-way" }));
        }
        [TestCase(-1f, 1f, 0f, true)]
        [TestCase(-1f, .5f, 0f, false)]
        [TestCase(-1f, .5001f, 0f, true)]
        [TestCase(0f, 1f, 0f, false)]
        [TestCase(1f, 1f, 0f, false)]
        [TestCase(-1f, 1f, -.125f, false)]
        [TestCase(-1f, 1f, .125f, true)]
        public void OneWayRequiresDescentUpwardNormalAndBottomAtTheInclusiveTopMargin(float movementY, float normalY, float bottom, bool expected)
        {
            var candidate = new Candidate { Surface = true, OneWay = true, Top = .03f };
            Assert.That(Qualifies(candidate, movementY, normalY, bottom), Is.EqualTo(expected));
            Assert.That(candidate.Reads, Does.Not.Contain("region"));
            if (movementY >= 0) Assert.That(candidate.Reads, Does.Not.Contain("normal"));
            if (movementY >= 0 || normalY <= .5f) Assert.That(candidate.Reads, Does.Not.Contain("bottom"));
        }
        [TestCase(true, false, false, false, false)]
        [TestCase(false, true, false, false, false)]
        [TestCase(false, false, true, false, false)]
        [TestCase(false, false, false, true, false)]
        [TestCase(false, false, false, false, true)]
        public void OnlyLiveNonOwnerNonPlayerNonDecoyActorsQualify(bool owner, bool player, bool decoy, bool dead, bool expected)
        {
            var candidate = new Candidate { Owner = owner, Player = player, Decoy = decoy, Dead = dead };
            Assert.That(Qualifies(candidate), Is.EqualTo(expected));
            Assert.That(candidate.Reads, Does.Not.Contain("bottom"));
            if (owner) Assert.That(candidate.Reads, Does.Not.Contain("player"));
            if (owner || player) Assert.That(candidate.Reads, Does.Not.Contain("decoy"));
            if (owner || player || decoy) Assert.That(candidate.Reads, Does.Not.Contain("dead"));
        }
        [TestCase(false)] [TestCase(true)]
        public void MissingRegionOrActorStopsBeforeActorFlags(bool region)
        {
            var candidate = new Candidate { Region = region, Actor = false };
            Assert.That(Qualifies(candidate), Is.False);
            Assert.That(candidate.Reads, Is.EqualTo(region ? new[] { "surface", "region", "actor" } : new[] { "surface", "region" }));
        }
        [Test] public void FirstQualifyingStartingOverlapExplodesWithoutFlightOrLaterQualification()
        {
            var host = Create(); var rejected = new Candidate { Player = true }; var first = Solid(); var later = Solid();
            host.State.Overlaps = new[] { rejected, first, later };
            host.State.Contacts = new[] { new Contact { Distance = 1 } };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.Explosions, Is.EqualTo(1)); Assert.That(host.State.Stopped, Is.True);
            Assert.That(host.OverlapQueries, Is.EqualTo(1)); Assert.That(host.CastQueries, Is.Zero); Assert.That(host.TravelWrites, Is.Zero);
            Assert.That(later.Reads, Is.Empty); Assert.That(host.State.Events, Does.Not.Contain("length"));
            Assert.That(host.State.X, Is.Zero); Assert.That(host.State.Y, Is.Zero);
        }
        [Test] public void StartingOverlapStillExplodesForZeroLengthDestination()
        {
            var host = Create(0); host.State.Overlaps = new[] { Solid() };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.Explosions, Is.EqualTo(1)); Assert.That(host.CastQueries, Is.Zero);
            Assert.That(host.State.Events, Does.Not.Contain("length"));
        }
        [TestCase(0f, 0)] [TestCase(.0000005f, 0)] [TestCase(.000001f, 1)] [TestCase(3f, 1)]
        public void EmptySweepsAlwaysQueryOverlapsButOnlySubThresholdLengthSkipsTravel(float distance, int expectedFlight)
        {
            var host = Create(distance); ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.OverlapQueries, Is.EqualTo(1)); Assert.That(host.CastQueries, Is.EqualTo(expectedFlight));
            Assert.That(host.TravelWrites, Is.EqualTo(expectedFlight)); Assert.That(host.State.Explosions, Is.Zero);
            Assert.That(host.State.X, Is.EqualTo(expectedFlight == 0 ? 0 : distance));
        }
        [Test] public void CastKeepsTheFirstTieAndNearestEligibleContactInNativeOrder()
        {
            var host = Create();
            var rejected = new Contact { Candidate = new Candidate { Decoy = true }, Distance = .1f };
            var far = new Contact { Distance = 4 }; var first = new Contact { Distance = 2 };
            var tie = new Contact { Distance = 2 }; var behind = new Contact { Distance = 3 };
            host.State.Contacts = new[] { rejected, far, first, tie, behind };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.X, Is.EqualTo(2)); Assert.That(host.State.Explosions, Is.EqualTo(1));
            Assert.That(rejected.DistanceReads, Is.Zero); Assert.That(far.DistanceReads, Is.EqualTo(1));
            Assert.That(first.DistanceReads, Is.EqualTo(2)); Assert.That(tie.DistanceReads, Is.EqualTo(1));
            Assert.That(behind.DistanceReads, Is.EqualTo(1));
            Assert.That(host.State.Events.IndexOf("contact:0"), Is.LessThan(host.State.Events.IndexOf("contact:4")));
            Assert.That(host.State.Events.IndexOf("travel"), Is.LessThan(host.State.Events.IndexOf("explode")));
        }
        [TestCase(-2f, 0f)] [TestCase(7f, 5f)]
        public void FirstContactIsClampedToTheEnteredSegmentEvenOutsideItsDistance(float distance, float expected)
        {
            var host = Create(); host.State.Contacts = new[] { new Contact { Distance = distance } };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.X, Is.EqualTo(expected)); Assert.That(host.TravelWrites, Is.EqualTo(1));
            Assert.That(host.State.Explosions, Is.EqualTo(1));
        }
        [Test] public void NonObstaclesNeverShortenTravelOrExplode()
        {
            var host = Create();
            host.State.Overlaps = new[] { new Candidate { Owner = true }, new Candidate { Actor = false } };
            host.State.Contacts = new[] { new Contact { Candidate = new Candidate { Dead = true }, Distance = 1 },
                new Contact { Candidate = new Candidate { Surface = true, OneWay = true }, Distance = 2 } };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.X, Is.EqualTo(5)); Assert.That(host.State.Explosions, Is.Zero);
            foreach (var contact in host.State.Contacts) Assert.That(contact.DistanceReads, Is.Zero);
        }
        [Test] public void OverlapQualificationReadsTheCurrentBottomAfterTheQuery()
        {
            var host = Create(0, 0); var state = host.State; state.Y = .5f;
            state.Overlaps = new[] { new Candidate { Surface = true, OneWay = true, Top = 1 } };
            state.OverlapsQueried = () => state.Y = 1.25f;
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.DeltaY, Is.EqualTo(-.5f)); Assert.That(state.Explosions, Is.EqualTo(1));
            Assert.That(state.Y, Is.EqualTo(1.25f)); Assert.That(host.TravelWrites, Is.Zero);
        }
        [Test] public void DeltaIsSnapshottedBeforeQueriesButTravelAddsToPositionAfterBothQueries()
        {
            var host = Create(4, 6); var state = host.State; state.X = 1; state.Y = 2;
            state.OverlapsQueried = () => { state.X = 10; state.Y = 20; };
            state.CastQueried = () => { state.X = 40; state.Y = 50; };
            state.Contacts = new[] { new Contact { Distance = 2 } };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.DeltaX, Is.EqualTo(3)); Assert.That(host.DeltaY, Is.EqualTo(4));
            Assert.That(state.X, Is.EqualTo(41.2f).Within(.00001f)); Assert.That(state.Y, Is.EqualTo(51.6f).Within(.00001f));
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(host.Snapshots, Is.EqualTo(1));
        }
        [Test] public void QualificationErrorAfterANearerContactDoesNotCommitTravelOrExplosion()
        {
            var host = Create(); var state = host.State;
            state.Contacts = new[] { new Contact { Distance = 1 }, new Contact { Distance = 2 } };
            state.CastSelected = index => { if (index == 1) { state.X = 12; throw new InvalidOperationException("qualification"); } };
            Assert.Throws<InvalidOperationException>(() => ContactBombSweepExecution.Sweep(ref host));
            Assert.That(state.X, Is.EqualTo(12)); Assert.That(host.TravelWrites, Is.Zero);
            Assert.That(state.Explosions, Is.Zero); Assert.That(state.Stopped, Is.False);
        }
        [Test] public void TravelErrorKeepsTheAppliedPositionWithoutClaimingAnExplosion()
        {
            var host = Create(); var state = host.State; state.Contacts = new[] { new Contact { Distance = 2 } };
            state.TravelApplied = () => throw new InvalidOperationException("travel");
            Assert.Throws<InvalidOperationException>(() => ContactBombSweepExecution.Sweep(ref host));
            Assert.That(state.X, Is.EqualTo(2)); Assert.That(host.TravelWrites, Is.EqualTo(1));
            Assert.That(state.Explosions, Is.Zero); Assert.That(state.Stopped, Is.False);
        }
        [Test] public void ExplosionErrorKeepsTravelAndTheExistingStopClaimWithoutASecondAttempt()
        {
            var host = Create(); var state = host.State; state.Contacts = new[] { new Contact { Distance = 2 } };
            state.Exploding = () => throw new InvalidOperationException("explosion");
            Assert.Throws<InvalidOperationException>(() => ContactBombSweepExecution.Sweep(ref host));
            Assert.That(state.X, Is.EqualTo(2)); Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(state.Stopped, Is.True);
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(host.Snapshots, Is.EqualTo(1)); Assert.That(host.TravelWrites, Is.EqualTo(1));
        }
        [Test] public void PositiveFuseDelegatesExactlyOnceWithoutEnteringContactFlowEvenIfTheCallbackChangesIt()
        {
            var host = Create(); var state = host.State; state.Fuse = 1.5f; state.Overlaps = new[] { Solid() };
            state.TimedSwept = () => state.Fuse = 0;
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(state.TimedSweeps, Is.EqualTo(1)); Assert.That(host.Snapshots, Is.Zero);
            Assert.That(host.OverlapQueries, Is.Zero); Assert.That(host.CastQueries, Is.Zero); Assert.That(host.TravelWrites, Is.Zero);
            Assert.That(state.Explosions, Is.Zero); Assert.That(state.Events, Is.EqualTo(new[] { "stopped", "fuse", "timed" }));
        }
        [TestCase(0f)] [TestCase(-1f)]
        public void NonpositiveFuseUsesContactFlow(float fuse)
        {
            var host = Create(); host.State.Fuse = fuse; host.State.Overlaps = new[] { Solid() };
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.TimedSweeps, Is.Zero); Assert.That(host.State.Explosions, Is.EqualTo(1));
        }
        [Test] public void StoppedOwnerPreemptsFuseAndBothContactAndTimedWork()
        {
            var host = Create(); host.State.Stopped = true; host.State.Fuse = 1.5f;
            ContactBombSweepExecution.Sweep(ref host);
            Assert.That(host.State.Events, Is.EqualTo(new[] { "stopped" })); Assert.That(host.Snapshots, Is.Zero);
            Assert.That(host.State.TimedSweeps, Is.Zero); Assert.That(host.State.Explosions, Is.Zero);
        }
    }
}
