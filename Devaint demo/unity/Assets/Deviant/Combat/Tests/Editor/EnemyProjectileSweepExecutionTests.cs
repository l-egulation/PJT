using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EnemyProjectileSweepExecutionTests
    {
        sealed class Target
        {
            public readonly int Id;
            public float X, Y, Health = 100;
            public bool Player, Decoy = true, Dead, Blocked, Parry, RejectDamage;
            public int Hits;
            public Target(int id, float x = 0, float y = 0) { Id = id; X = x; Y = y; }
        }
        readonly struct Point
        {
            public readonly float X, Y;
            public Point(float x, float y) { X = x; Y = y; }
        }
        readonly struct Sample
        {
            public readonly float X, Y, Width, Height;
            public Sample(float x, float y, float width, float height)
            { X = x; Y = y; Width = width; Height = height; }
        }
        readonly struct Travel
        {
            public readonly Sample Box;
            public readonly float DirectionX, DirectionY, Distance;
            public Travel(Sample box, float directionX, float directionY, float distance)
            { Box = box; DirectionX = directionX; DirectionY = directionY; Distance = distance; }
        }
        sealed class Projectile : IEnemyProjectileExecutionHost
        {
            public readonly EnemyProjectileExecution Owner = new EnemyProjectileExecution(8, 100);
            public float Now { get; set; }
            public Target Source = new Target(100) { Decoy = false };
            public bool SourceExists => Source != null;
            public bool SourceDead => Source.Dead;
            public bool SourceIsPlayer => Source.Player;
            public float X, Y, VelocityX = 1, VelocityY, Bonus = .4f;
            public float? Allowed;
            public int Begins, PositionWrites, ReleaseRequests, Removals;
            public readonly List<Target> Targets = new List<Target>();
            public readonly List<Sample> Samples = new List<Sample>();
            public readonly List<Travel> Travels = new List<Travel>();
            public readonly List<Point> OcclusionOrigins = new List<Point>();
            public readonly List<Point> ContactOrigins = new List<Point>();
            public readonly List<int> Contacts = new List<int>();
            public readonly List<int[]> Sorted = new List<int[]>();
            public readonly List<string> Events = new List<string>();
            public Func<Sample, List<Target>> Query;
            public Action<string> Callback;
            public void Mark(string name) { Events.Add(name); Callback?.Invoke(name); }
            public void Step(float duration) => Owner.Step(duration, this);
            public void Sweep(float duration)
            { var host = new Host(this); EnemyProjectileSweepExecution.Sweep(duration, ref host); }
            public void Remove() { Removals++; Mark("remove"); }
            public bool Contact(Target target)
            { var host = new ContactHost(this, target, X, Y); return Owner.TryContact(target.Id, target.Player, ref host); }
            public void PrimeClaim(Target target)
            {
                Assert.That(target.Player, Is.True);
                var error = new InvalidOperationException("entered native parry");
                Callback = name => { if (name == "parry/" + target.Id) throw error; };
                Assert.That(Assert.Throws<InvalidOperationException>(() => Contact(target)), Is.SameAs(error));
                Callback = null; Events.Clear(); Contacts.Clear(); ContactOrigins.Clear();
            }
        }
        struct Host : IEnemyProjectileSweepHost
        {
            readonly Projectile shot;
            List<Target> query, candidates;
            public Host(Projectile shot) { this.shot = shot; query = candidates = null; }
            public void ReadPosition(out float x, out float y)
            { shot.Mark("position"); x = shot.X; y = shot.Y; }
            public float VelocityMagnitude
            { get { shot.Mark("speed"); return (float)Math.Sqrt(shot.VelocityX * shot.VelocityX + shot.VelocityY * shot.VelocityY); } }
            public void ReadDirection(out float x, out float y)
            {
                shot.Mark("direction");
                float length = (float)Math.Sqrt(shot.VelocityX * shot.VelocityX + shot.VelocityY * shot.VelocityY);
                x = length > .00001f ? shot.VelocityX / length : 0;
                y = length > .00001f ? shot.VelocityY / length : 0;
            }
            public float SolidTravel(float x, float y, float width, float height, float directionX, float directionY, float distance)
            {
                shot.Travels.Add(new Travel(new Sample(x, y, width, height), directionX, directionY, distance));
                shot.Mark("solid"); return shot.Allowed ?? distance;
            }
            public void BeginCandidates()
            { shot.Mark("begin"); shot.Begins++; candidates = new List<Target>(); }
            public int QueryBox(float x, float y, float width, float height)
            {
                var box = new Sample(x, y, width, height); shot.Samples.Add(box);
                if (shot.Query != null) query = shot.Query(box);
                else
                {
                    query = new List<Target>();
                    foreach (var target in shot.Targets)
                        if (Math.Abs(target.X - x) <= width / 2 && Math.Abs(target.Y - y) <= height / 2) query.Add(target);
                }
                shot.Mark("query"); return query.Count;
            }
            public bool ContainsCandidate(int i)
            { shot.Mark("contains/" + query[i].Id); return candidates.Contains(query[i]); }
            public void AddCandidate(int i)
            { shot.Mark("add/" + query[i].Id); candidates.Add(query[i]); }
            public void SortCandidates(float originX, float originY)
            {
                shot.Mark("sort");
                candidates.Sort((a, b) => Distance(a, originX, originY).CompareTo(Distance(b, originX, originY)));
                shot.Sorted.Add(candidates.ConvertAll(target => target.Id).ToArray());
            }
            static float Distance(Target target, float x, float y)
            { float dx = x - target.X, dy = y - target.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
            public int CandidateCount => candidates.Count;
            public bool AcceptsTarget(int i)
            {
                var target = candidates[i]; shot.Mark("accept/" + target.Id);
                return shot.Owner.AcceptsTarget(target.Dead, target == shot.Source, target.Player, target.Decoy);
            }
            public bool Occluded(int i, float originX, float originY)
            {
                shot.OcclusionOrigins.Add(new Point(originX, originY));
                shot.Mark("occluded/" + candidates[i].Id); return candidates[i].Blocked;
            }
            public bool TryContact(int i, float originX, float originY)
            {
                var target = candidates[i]; shot.Mark("contact/" + target.Id);
                shot.Contacts.Add(target.Id); shot.ContactOrigins.Add(new Point(originX, originY));
                var contact = new ContactHost(shot, target, originX, originY);
                return shot.Owner.TryContact(target.Id, target.Player, ref contact);
            }
            public void SetPosition(float x, float y)
            { shot.Mark("set"); shot.X = x; shot.Y = y; shot.PositionWrites++; }
            public void Release()
            { shot.Mark("release"); shot.ReleaseRequests++; shot.Owner.Release(shot); }
        }
        readonly struct ContactHost : IEnemyProjectileContactHost
        {
            readonly Projectile shot;
            readonly Target target;
            readonly float originX, originY;
            public ContactHost(Projectile shot, Target target, float originX, float originY)
            { this.shot = shot; this.target = target; this.originX = originX; this.originY = originY; }
            public bool TryParry() { shot.Mark("parry/" + target.Id); return target.Parry; }
            public void ReflectSourceAndVelocity()
            { shot.Source = target; shot.VelocityX = -shot.VelocityX; shot.VelocityY = -shot.VelocityY; shot.Mark("reflect"); }
            public float SpecialBonus { get { shot.Mark("bonus"); return shot.Bonus; } }
            public void RestoreOrigin() { shot.X = originX; shot.Y = originY; shot.Mark("restore"); }
            public void Receive(float damage)
            { if (!target.RejectDamage) { target.Health -= damage; target.Hits++; } shot.Mark("receive/" + target.Id); }
            public void Remove() => shot.Remove();
        }
        static Projectile AllCandidates()
        {
            var shot = new Projectile(); shot.Query = _ => new List<Target>(shot.Targets); return shot;
        }
        static List<string> EventsStarting(Projectile shot, string prefix)
            => shot.Events.FindAll(name => name.StartsWith(prefix, StringComparison.Ordinal));

        [TestCase(0f, 2)]
        [TestCase(.04f, 2)]
        [TestCase(.1f, 2)]
        [TestCase(.25f, 4)]
        [TestCase(2.35f, 25)]
        public void SamplesIncludeBothEndpointsWithCeilingCountAndAtMostPointOneSpacing(float distance, int count)
        {
            var shot = new Projectile { X = 2, Y = -3 }; shot.Step(distance);
            Assert.That(shot.Samples.Count, Is.EqualTo(count)); Assert.That(shot.Begins, Is.EqualTo(1));
            Assert.That(shot.Samples[0].X, Is.EqualTo(2)); Assert.That(shot.Samples[count - 1].X, Is.EqualTo(2 + distance));
            for (int i = 0; i < count; i++)
            {
                var box = shot.Samples[i]; Assert.That(box.Y, Is.EqualTo(-3));
                Assert.That(box.Width, Is.EqualTo(.25f)); Assert.That(box.Height, Is.EqualTo(.25f));
                Assert.That(box.X, Is.EqualTo(2 + distance * i / (count - 1)).Within(.000001f));
                if (i != 0) Assert.That(box.X - shot.Samples[i - 1].X, Is.InRange(0f, .100001f));
            }
            Assert.That(shot.X, Is.EqualTo(2 + distance)); Assert.That(shot.Y, Is.EqualTo(-3));
            Assert.That(shot.Removals, Is.Zero); Assert.That(shot.PositionWrites, Is.EqualTo(1));
        }
        [Test] public void ZeroVelocityStillQueriesTheOverlapTwiceEvenForALargeDuration()
        {
            var shot = new Projectile { X = 7, Y = -2, VelocityX = 0 }; shot.Step(4);
            Assert.That(shot.Samples.Count, Is.EqualTo(2));
            foreach (var box in shot.Samples) { Assert.That(box.X, Is.EqualTo(7)); Assert.That(box.Y, Is.EqualTo(-2)); }
            Assert.That(shot.Travels[0].Distance, Is.Zero); Assert.That(shot.Removals, Is.Zero);
        }
        [Test] public void DiagonalClippedFlightUsesTheAllowedLengthForCountAndEndpointGeometry()
        {
            var shot = new Projectile { X = 2, Y = -3, VelocityX = 3, VelocityY = 4, Allowed = .5f }; shot.Step(2);
            Assert.That(shot.Travels[0].Distance, Is.EqualTo(10)); Assert.That(shot.Travels[0].DirectionX, Is.EqualTo(.6f));
            Assert.That(shot.Travels[0].DirectionY, Is.EqualTo(.8f)); Assert.That(shot.Samples.Count, Is.EqualTo(6));
            Assert.That(shot.Samples[2].X, Is.EqualTo(2.12f).Within(.000001f));
            Assert.That(shot.Samples[2].Y, Is.EqualTo(-2.84f).Within(.000001f));
            Assert.That(shot.X, Is.EqualTo(2.3f)); Assert.That(shot.Y, Is.EqualTo(-2.6f));
            Assert.That(shot.Events.GetRange(shot.Events.Count - 3, 3), Is.EqualTo(new[] { "set", "release", "remove" }));
        }
        [Test] public void SolidTravelKeepsItsOriginalDirectionButNextUsesTheCurrentDirectionAfterTheCallback()
        {
            var shot = new Projectile { X = 2, Y = 3, VelocityX = 2, Allowed = .2f };
            shot.Callback = name => { if (name == "solid") { shot.VelocityX = 0; shot.VelocityY = -7; shot.X = 99; shot.Y = 99; } };
            shot.Step(.25f);
            Assert.That(shot.Travels[0].Distance, Is.EqualTo(.5f)); Assert.That(shot.Travels[0].DirectionX, Is.EqualTo(1));
            Assert.That(shot.Travels[0].DirectionY, Is.Zero); Assert.That(shot.Travels[0].Box.X, Is.EqualTo(2));
            Assert.That(shot.Samples.Count, Is.EqualTo(3)); Assert.That(shot.Samples[1].X, Is.EqualTo(2));
            Assert.That(shot.Samples[1].Y, Is.EqualTo(2.9f)); Assert.That(shot.X, Is.EqualTo(2)); Assert.That(shot.Y, Is.EqualTo(2.8f));
            Assert.That(shot.Events.GetRange(0, 6), Is.EqualTo(new[] { "position", "speed", "direction", "solid", "direction", "begin" }));
        }
        [Test] public void GeometryFindsATargetBetweenEndpointsAndNotOutsideTheQuarterUnitBox()
        {
            var shot = new Projectile(); var hit = new Target(1, .5f); var outside = new Target(2, .5f, .126f);
            shot.Targets.AddRange(new[] { outside, hit, hit }); shot.Step(1);
            Assert.That(shot.Contacts, Is.EqualTo(new[] { 1 })); Assert.That(hit.Hits, Is.EqualTo(1)); Assert.That(hit.Health, Is.EqualTo(92));
            Assert.That(outside.Hits, Is.Zero); Assert.That(shot.Sorted[0], Is.EqualTo(new[] { 1 }));
            Assert.That(shot.X, Is.Zero); Assert.That(shot.PositionWrites, Is.Zero); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void AllSamplesCollectAndDeduplicateBeforeLiveDistanceSortingOrAnyEligibilityRead()
        {
            var shot = AllCandidates(); var far = new Target(9, 1); var near = new Target(2, 3); var middle = new Target(5, 2);
            shot.Targets.AddRange(new[] { far, near, far, middle, near });
            shot.Callback = name => { if (name == "sort") { far.X = 3; near.X = .25f; } };
            shot.Step(.25f);
            Assert.That(shot.Samples.Count, Is.EqualTo(4)); Assert.That(shot.Sorted[0], Is.EqualTo(new[] { 2, 5, 9 }));
            Assert.That(EventsStarting(shot, "add/"), Is.EqualTo(new[] { "add/9", "add/2", "add/5" }));
            Assert.That(EventsStarting(shot, "accept/"), Is.EqualTo(new[] { "accept/2" }));
            Assert.That(shot.Events.LastIndexOf("query"), Is.LessThan(shot.Events.IndexOf("sort")));
            Assert.That(near.Hits, Is.EqualTo(1)); Assert.That(far.Hits + middle.Hits, Is.Zero);
        }
        [Test] public void RejectedEligibilitySkipsOcclusionAndBlockedTargetsSkipTheExistingContactLedger()
        {
            var shot = AllCandidates(); var dead = new Target(1, .1f) { Dead = true }; var source = shot.Source; source.X = .2f;
            var enemy = new Target(3, .3f) { Decoy = false }; var blocked = new Target(4, .4f) { Player = true, Blocked = true };
            var hit = new Target(5, .5f); shot.Targets.AddRange(new[] { hit, blocked, enemy, source, dead }); shot.Step(1);
            Assert.That(EventsStarting(shot, "accept/"), Is.EqualTo(new[] { "accept/1", "accept/100", "accept/3", "accept/4", "accept/5" }));
            Assert.That(EventsStarting(shot, "occluded/"), Is.EqualTo(new[] { "occluded/4", "occluded/5" }));
            Assert.That(shot.Contacts, Is.EqualTo(new[] { 5 })); Assert.That(hit.Hits, Is.EqualTo(1)); Assert.That(blocked.Hits, Is.Zero);
            var stillLive = AllCandidates(); stillLive.Targets.Add(blocked); stillLive.Step(0);
            blocked.Blocked = false; stillLive.Step(0); Assert.That(blocked.Hits, Is.EqualTo(1));
        }
        [Test] public void EarlierClaimedContactChangesLaterEligibilityAndOcclusionWithoutCapturingActorFacts()
        {
            var shot = AllCandidates(); var claimed = new Target(1, .1f) { Player = true }; shot.PrimeClaim(claimed);
            var becomesSource = new Target(2, .2f); var becomesBlocked = new Target(3, .3f); var becomesEligible = new Target(4, .4f) { Dead = true, Decoy = false };
            shot.Targets.AddRange(new[] { claimed, becomesSource, becomesBlocked, becomesEligible });
            shot.Callback = name => {
                if (name != "contact/1") return;
                shot.Source = becomesSource; becomesBlocked.Blocked = true;
                becomesEligible.Dead = false; becomesEligible.Decoy = true;
                shot.X = 90; shot.Y = 91;
            };
            shot.Step(1);
            Assert.That(shot.Contacts, Is.EqualTo(new[] { 1, 4 })); Assert.That(becomesEligible.Hits, Is.EqualTo(1));
            Assert.That(becomesSource.Hits + becomesBlocked.Hits, Is.Zero); Assert.That(claimed.Hits, Is.Zero);
            Assert.That(EventsStarting(shot, "occluded/"), Is.EqualTo(new[] { "occluded/1", "occluded/3", "occluded/4" }));
            foreach (var origin in shot.OcclusionOrigins) { Assert.That(origin.X, Is.Zero); Assert.That(origin.Y, Is.Zero); }
            foreach (var origin in shot.ContactOrigins) { Assert.That(origin.X, Is.Zero); Assert.That(origin.Y, Is.Zero); }
            Assert.That(shot.X, Is.EqualTo(90)); Assert.That(shot.Y, Is.EqualTo(91)); Assert.That(shot.PositionWrites, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void FirstSuccessfulExistingContactReturnsBeforePositionAndWallReleaseEvenWhenDamageIsRejected(bool reject)
        {
            var shot = AllCandidates(); shot.Allowed = .2f;
            var first = new Target(1, .1f) { RejectDamage = reject }; var last = new Target(2, .2f);
            shot.Targets.AddRange(new[] { last, first }); shot.Step(1);
            Assert.That(shot.Contacts, Is.EqualTo(new[] { 1 })); Assert.That(last.Hits, Is.Zero);
            Assert.That(first.Health, Is.EqualTo(reject ? 100 : 92)); Assert.That(shot.Owner.Read().Removed, Is.True);
            Assert.That(shot.PositionWrites, Is.Zero); Assert.That(shot.ReleaseRequests, Is.Zero); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void AlreadyClaimedCandidatesContinueToPositionAndWallRelease()
        {
            var shot = AllCandidates(); shot.Allowed = .2f;
            var target = new Target(1) { Player = true }; shot.PrimeClaim(target); shot.Targets.Add(target);
            shot.Step(1);
            Assert.That(shot.Contacts, Is.EqualTo(new[] { 1 })); Assert.That(target.Hits, Is.Zero);
            Assert.That(shot.X, Is.EqualTo(.2f)); Assert.That(shot.PositionWrites, Is.EqualTo(1));
            Assert.That(shot.ReleaseRequests, Is.EqualTo(1)); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void ParryRestoresTheOriginalPositionAndRetainsTheExistingReflectionOwner()
        {
            var shot = AllCandidates(); shot.X = 2; shot.Y = 3; shot.VelocityX = -2; shot.Allowed = .2f;
            var parrier = new Target(1, 2, 3) { Player = true, Parry = true }; shot.Targets.Add(parrier); shot.Step(1);
            Assert.That(shot.Source, Is.SameAs(parrier)); Assert.That(shot.VelocityX, Is.EqualTo(2));
            Assert.That(shot.Owner.Read().Reflected, Is.True); Assert.That(shot.Owner.Read().Damage, Is.EqualTo(11.2f));
            Assert.That(shot.X, Is.EqualTo(2)); Assert.That(shot.Y, Is.EqualTo(3)); Assert.That(shot.PositionWrites, Is.Zero);
            Assert.That(shot.ReleaseRequests, Is.Zero); Assert.That(shot.Removals, Is.Zero); Assert.That(parrier.Hits, Is.Zero);
        }
        [TestCase(.5f, .5f, false)]
        [TestCase(.5f, .49995f, false)]
        [TestCase(.5f, .4999f, false)]
        [TestCase(.5f, .49989f, true)]
        [TestCase(0f, 0f, false)]
        public void WallReleaseUsesStrictOriginalDistanceMinusPointZeroZeroZeroOneAfterPosition(float distance, float allowed, bool released)
        {
            var shot = new Projectile { Allowed = allowed }; shot.Step(distance);
            Assert.That(shot.X, Is.EqualTo(allowed)); Assert.That(shot.PositionWrites, Is.EqualTo(1));
            Assert.That(shot.ReleaseRequests, Is.EqualTo(released ? 1 : 0)); Assert.That(shot.Removals, Is.EqualTo(released ? 1 : 0));
            Assert.That(shot.Owner.Read().Removed, Is.EqualTo(released));
            Assert.That(shot.Events[shot.Events.Count - (released ? 3 : 1)], Is.EqualTo("set"));
        }
        [Test] public void ReentrantSweepGetsIndependentCandidatesAndTheOuterTraversalResumesItsOwnList()
        {
            var shot = AllCandidates(); var first = new Target(1, 1) { Player = true }; var second = new Target(2, 2) { Player = true };
            var nested = new Target(3, 3) { Player = true };
            shot.PrimeClaim(first); shot.PrimeClaim(second); shot.PrimeClaim(nested); shot.Targets.AddRange(new[] { first, second });
            bool entered = false;
            shot.Callback = name => {
                if (name != "contact/1" || entered) return; entered = true;
                shot.Targets.Clear(); shot.Targets.Add(nested); shot.Sweep(0);
            };
            shot.Step(.2f);
            Assert.That(EventsStarting(shot, "contact/"), Is.EqualTo(new[] { "contact/1", "contact/3", "contact/2" }));
            Assert.That(shot.Sorted, Has.Count.EqualTo(2)); Assert.That(shot.Sorted[0], Is.EqualTo(new[] { 1, 2 }));
            Assert.That(shot.Sorted[1], Is.EqualTo(new[] { 3 })); Assert.That(shot.Begins, Is.EqualTo(2));
            Assert.That(shot.X, Is.EqualTo(.2f)); Assert.That(shot.PositionWrites, Is.EqualTo(2)); Assert.That(shot.Removals, Is.Zero);
        }
        [Test] public void EnteredSweepDoesNotInventARemovedGuardAfterAHostCallback()
        {
            var shot = AllCandidates(); shot.Allowed = .2f;
            shot.Targets.AddRange(new[] { new Target(1, .1f), new Target(2, .2f) });
            shot.Callback = name => { if (name == "occluded/1") shot.Owner.Release(shot); };
            shot.Step(1);
            Assert.That(shot.Contacts, Is.EqualTo(new[] { 1, 2 })); Assert.That(shot.X, Is.EqualTo(.2f));
            Assert.That(shot.PositionWrites, Is.EqualTo(1)); Assert.That(shot.ReleaseRequests, Is.EqualTo(1));
            Assert.That(shot.Removals, Is.EqualTo(1)); Assert.That(shot.Targets[0].Hits + shot.Targets[1].Hits, Is.Zero);
            int queries = shot.Samples.Count; shot.Step(1); Assert.That(shot.Samples.Count, Is.EqualTo(queries));
        }
        [TestCase("position", 1)] [TestCase("speed", 1)] [TestCase("direction", 1)]
        [TestCase("solid", 1)] [TestCase("direction", 2)] [TestCase("begin", 1)]
        [TestCase("query", 1)] [TestCase("query", 2)] [TestCase("sort", 1)]
        [TestCase("accept/1", 1)] [TestCase("occluded/1", 1)] [TestCase("contact/1", 1)]
        [TestCase("parry/1", 1)] [TestCase("receive/1", 1)] [TestCase("remove", 1)]
        public void NativeFailuresPropagateAtTheirExactPrefixWithoutLaterContactMovementOrRelease(string stage, int occurrence)
        {
            var shot = AllCandidates(); var target = new Target(1) { Player = true }; shot.Targets.Add(target);
            var error = new InvalidOperationException(stage); int seen = 0;
            shot.Callback = name => { if (name == stage && ++seen == occurrence) throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => shot.Step(.1f)), Is.SameAs(error));
            var complete = new[] { "position", "speed", "direction", "solid", "direction", "begin", "query", "contains/1", "add/1",
                "query", "contains/1", "sort", "accept/1", "occluded/1", "contact/1", "parry/1", "receive/1", "remove" };
            int end = -1;
            for (int i = 0, matches = 0; i < complete.Length; i++)
                if (complete[i] == stage && ++matches == occurrence) { end = i; break; }
            Assert.That(shot.Events, Is.EqualTo(new ArraySegment<string>(complete, 0, end + 1)));
            Assert.That(shot.X, Is.Zero); Assert.That(shot.PositionWrites, Is.Zero); Assert.That(shot.ReleaseRequests, Is.Zero);
            Assert.That(target.Hits, Is.EqualTo(stage == "receive/1" || stage == "remove" ? 1 : 0));
            Assert.That(shot.Owner.Read().Removed, Is.EqualTo(stage == "remove"));
            if (stage == "parry/1" || stage == "receive/1")
            { shot.Callback = null; Assert.That(shot.Contact(target), Is.False, "The existing contact claim survives its callback exception."); }
        }
        [TestCase("set")] [TestCase("release")] [TestCase("remove")]
        public void MovementAndWallRemovalFailuresKeepOnlyTheAlreadyCompletedPrefix(string stage)
        {
            var shot = new Projectile { X = 2, Y = 3, Allowed = .2f };
            var error = new InvalidOperationException(stage); shot.Callback = name => { if (name == stage) throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => shot.Step(1)), Is.SameAs(error));
            Assert.That(shot.X, Is.EqualTo(stage == "set" ? 2 : 2.2f)); Assert.That(shot.Y, Is.EqualTo(3));
            Assert.That(shot.PositionWrites, Is.EqualTo(stage == "set" ? 0 : 1));
            Assert.That(shot.ReleaseRequests, Is.EqualTo(stage == "remove" ? 1 : 0));
            Assert.That(shot.Owner.Read().Removed, Is.EqualTo(stage == "remove"));
            Assert.That(shot.Removals, Is.EqualTo(stage == "remove" ? 1 : 0)); Assert.That(shot.Events[shot.Events.Count - 1], Is.EqualTo(stage));
        }
    }
}
