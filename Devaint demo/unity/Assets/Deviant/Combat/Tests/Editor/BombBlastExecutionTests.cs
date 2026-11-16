using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class BombBlastExecutionTests
    {
        sealed class Actor
        {
            public readonly int Id;
            public bool Exists = true, Player, Decoy, Dead, Accept = true;
            public float Health = 100, CenterX;
            public int Attempts;
            public Actor(int id) { Id = id; }
        }
        sealed class Region
        {
            public Actor Actor;
            public bool Exists = true, Occluded;
            public float Distance;
            public Region(Actor actor, float distance = 0, bool occluded = false)
            { Actor = actor; Distance = distance; Occluded = occluded; }
        }
        sealed class Fragment
        {
            public int Index, OwnerId, Sweeps;
            public float Angle, Origin, Position, SweptPosition;
        }
        readonly struct Hit
        {
            public readonly int ActorId, OwnerId, Direction;
            public readonly float Damage;
            public Hit(int actorId, int ownerId, int direction, float damage)
            { ActorId = actorId; OwnerId = ownerId; Direction = direction; Damage = damage; }
        }
        sealed class Blast
        {
            public Actor Owner = new Actor(0);
            public Region[] Candidates = Array.Empty<Region>();
            public readonly List<float> Angles = new List<float>();
            public readonly List<string> Calls = new List<string>();
            public readonly List<int> Claims = new List<int>(), Closest = new List<int>(), Occlusion = new List<int>();
            public readonly List<HashSet<int>> Ledgers = new List<HashSet<int>>();
            public readonly List<Hit> Hits = new List<Hit>();
            public readonly List<Fragment> Fragments = new List<Fragment>();
            public float Position, Radius = 1.5f, Damage = 18, QueryPosition, QueryRadius, BurstPosition, BurstRadius;
            public int Explosions, Removals;
            public Action<Actor> FindingClosest, Receiving;
            public Action Exploding;
            public Action<int> ReadingAngle;
            public Action<Fragment> Spawning, Sweeping;
            public void Execute() { var host = new Host(this); BombBlastExecution.ExplodeStopped(ref host); }
        }
        struct Host : IBombBlastHost
        {
            readonly Blast blast;
            HashSet<int> seen;
            Region[] candidates;
            Region region;
            float contactDistance;
            Fragment fragment;
            public Host(Blast blast)
            { this.blast = blast; seen = null; candidates = null; region = null; contactDistance = 0; fragment = null; }
            public void BeginBlast()
            { blast.Calls.Add("begin"); seen = new HashSet<int>(); blast.Ledgers.Add(seen); }
            public int QueryCandidates()
            {
                blast.Calls.Add("query"); candidates = blast.Candidates;
                blast.QueryPosition = blast.Position; blast.QueryRadius = blast.Radius; return candidates.Length;
            }
            public void SelectCandidate(int index) { blast.Calls.Add("select:" + index); region = candidates[index]; }
            public bool RegionExists => region != null && region.Exists;
            public bool TargetExists => region.Actor != null && region.Actor.Exists;
            public bool TargetIsOwner => region.Actor == blast.Owner;
            public bool TargetIsPlayer => region.Actor.Player;
            public bool TargetIsDecoy => region.Actor.Decoy;
            public bool TargetDead => region.Actor.Dead;
            public bool TryClaimTarget()
            {
                int id = region.Actor.Id; blast.Calls.Add("claim:" + id); blast.Claims.Add(id); return seen.Add(id);
            }
            public void FindClosestPoint()
            {
                blast.Calls.Add("closest:" + region.Actor.Id); blast.Closest.Add(region.Actor.Id);
                blast.FindingClosest?.Invoke(region.Actor); contactDistance = region.Distance;
            }
            public float ContactDistance => contactDistance;
            public float BlastRadius => blast.Radius;
            public bool Occluded()
            {
                blast.Calls.Add("occlusion:" + region.Actor.Id); blast.Occlusion.Add(region.Actor.Id); return region.Occluded;
            }
            public void ReceiveBasic()
            {
                var target = region.Actor; blast.Calls.Add("receive:" + target.Id); target.Attempts++;
                blast.Hits.Add(new Hit(target.Id, blast.Owner.Id, target.CenterX < blast.Position ? -1 : 1, blast.Damage));
                if (target.Accept) target.Health -= blast.Damage;
                blast.Receiving?.Invoke(target);
            }
            public void BombExploded()
            {
                blast.Calls.Add("exploded"); blast.Explosions++;
                blast.BurstPosition = blast.Position; blast.BurstRadius = blast.Radius; blast.Exploding?.Invoke();
            }
            public int FragmentCount { get { blast.Calls.Add("count"); return blast.Angles.Count; } }
            public float FragmentAngleAt(int index)
            { blast.Calls.Add("angle:" + index); blast.ReadingAngle?.Invoke(index); return blast.Angles[index]; }
            public void SpawnFragment(float angle)
            {
                fragment = new Fragment { Index = blast.Fragments.Count, OwnerId = blast.Owner.Id,
                    Angle = angle, Origin = blast.Position, Position = blast.Position };
                blast.Fragments.Add(fragment); blast.Calls.Add("spawn:" + fragment.Index); blast.Spawning?.Invoke(fragment);
            }
            public void SweepFragmentAtCurrentPosition()
            {
                blast.Calls.Add("sweep:" + fragment.Index); fragment.Sweeps++; fragment.SweptPosition = fragment.Position;
                blast.Sweeping?.Invoke(fragment);
            }
            public void Remove() { blast.Calls.Add("remove"); blast.Removals++; }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static int[] HitIds(Blast blast) => blast.Hits.ConvertAll(hit => hit.ActorId).ToArray();

        [TestCase("no-region")] [TestCase("destroyed-region")] [TestCase("no-actor")] [TestCase("destroyed-actor")]
        [TestCase("owner")] [TestCase("player")] [TestCase("decoy")] [TestCase("dead")]
        public void IneligibleCandidatesDoNotClaimAnActorIdOrReadItsGeometry(string scenario)
        {
            var blast = new Blast(); var excluded = new Actor(9); var accepted = new Actor(9);
            var region = new Region(excluded);
            switch (scenario)
            {
                case "no-region": region = null; break;
                case "destroyed-region": region.Exists = false; break;
                case "no-actor": region.Actor = null; break;
                case "destroyed-actor": excluded.Exists = false; break;
                case "owner": blast.Owner = excluded; break;
                case "player": excluded.Player = true; break;
                case "decoy": excluded.Decoy = true; break;
                case "dead": excluded.Dead = true; break;
            }
            blast.Candidates = new[] { region, new Region(accepted) }; blast.Execute();
            Assert.That(excluded.Attempts, Is.Zero); Assert.That(accepted.Attempts, Is.EqualTo(1));
            Assert.That(accepted.Health, Is.EqualTo(82)); Assert.That(blast.Claims, Is.EqualTo(new[] { 9 }));
            Assert.That(blast.Closest, Is.EqualTo(new[] { 9 })); Assert.That(blast.Occlusion, Is.EqualTo(new[] { 9 }));
            Assert.That(blast.Explosions, Is.EqualTo(1)); Assert.That(blast.Removals, Is.EqualTo(1));
        }
        [Test] public void NativeCandidateOrderAndActorIdDedupStayIndependentForEachEnteredBlast()
        {
            var first = new Actor(9); var second = new Actor(3); var third = new Actor(6);
            var candidates = new[] { new Region(first), new Region(second), new Region(first), new Region(third) };
            var outer = new Blast { Candidates = candidates }; var nested = new Blast { Candidates = candidates };
            bool entered = false;
            outer.Receiving = actor =>
            {
                if (entered) return;
                entered = true; nested.Execute();
                Assert.That(outer.Ledgers[0], Is.EquivalentTo(new[] { 9 }));
            };
            outer.Execute();
            Assert.That(HitIds(outer), Is.EqualTo(new[] { 9, 3, 6 }));
            Assert.That(HitIds(nested), Is.EqualTo(new[] { 9, 3, 6 }));
            Assert.That(outer.Claims, Is.EqualTo(new[] { 9, 3, 9, 6 }));
            Assert.That(outer.Ledgers[0], Is.Not.SameAs(nested.Ledgers[0]));
            foreach (var actor in new[] { first, second, third })
            { Assert.That(actor.Attempts, Is.EqualTo(2)); Assert.That(actor.Health, Is.EqualTo(64)); }
        }
        [Test] public void DistanceOcclusionAndRejectedReceiveSpendTheClaimBeforeAnyLaterCollider()
        {
            var outside = new Actor(1); var blocked = new Actor(2); var rejecting = new Actor(3) { Accept = false };
            var blast = new Blast { Candidates = new[] { new Region(outside, 2), new Region(outside),
                new Region(blocked, 0, true), new Region(blocked), new Region(rejecting), new Region(rejecting) } };
            blast.Execute();
            Assert.That(HitIds(blast), Is.EqualTo(new[] { 3 })); Assert.That(rejecting.Attempts, Is.EqualTo(1));
            Assert.That(outside.Health, Is.EqualTo(100)); Assert.That(blocked.Health, Is.EqualTo(100));
            Assert.That(rejecting.Health, Is.EqualTo(100)); Assert.That(blast.Ledgers[0], Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(blast.Closest, Is.EqualTo(new[] { 1, 2, 3 })); Assert.That(blast.Occlusion, Is.EqualTo(new[] { 2, 3 }));
        }
        [Test] public void InsideTargetsReceiveDamageUnlessOccludedAndOutsideTargetsSkipOcclusion()
        {
            var inside = new Actor(1); var outside = new Actor(2); var blocked = new Actor(3);
            var blast = new Blast { Radius = 1.5f, Candidates = new[] { new Region(inside, 1.49f),
                new Region(outside, 1.51f), new Region(blocked, 1.49f, true) } };
            blast.Execute();
            Assert.That(HitIds(blast), Is.EqualTo(new[] { 1 }));
            Assert.That(inside.Health, Is.EqualTo(82)); Assert.That(outside.Health, Is.EqualTo(100));
            Assert.That(blocked.Health, Is.EqualTo(100));
            Assert.That(blast.Occlusion, Is.EqualTo(new[] { 1, 3 })); Assert.That(blast.Closest, Is.EqualTo(new[] { 1, 2, 3 }));
        }
        [Test] public void ReceiveCallbacksChangeLaterCandidateEligibilityAndLiveRadiusOwnerDamageAndPosition()
        {
            var first = new Actor(1) { CenterX = -1 }; var nextOwner = new Actor(2);
            var outside = new Actor(3); var original = new Actor(4); var replacement = new Actor(5) { CenterX = 11 };
            var lastRegion = new Region(original, .25f);
            var blast = new Blast { Candidates = new[] { new Region(first), new Region(nextOwner), new Region(outside, 1), lastRegion } };
            blast.Receiving = actor =>
            {
                if (actor != first) return;
                blast.Owner = nextOwner; blast.Position = 10; blast.Radius = .5f; blast.Damage = 7;
                blast.Candidates = Array.Empty<Region>(); lastRegion.Actor = replacement;
            };
            blast.Execute();
            Assert.That(HitIds(blast), Is.EqualTo(new[] { 1, 5 })); Assert.That(original.Attempts, Is.Zero);
            Assert.That(blast.Hits[0].OwnerId, Is.Zero); Assert.That(blast.Hits[0].Direction, Is.EqualTo(-1));
            Assert.That(Bits(blast.Hits[0].Damage), Is.EqualTo(Bits(18)));
            Assert.That(blast.Hits[1].OwnerId, Is.EqualTo(2)); Assert.That(blast.Hits[1].Direction, Is.EqualTo(1));
            Assert.That(Bits(blast.Hits[1].Damage), Is.EqualTo(Bits(7))); Assert.That(replacement.Health, Is.EqualTo(93));
            Assert.That(blast.Closest, Is.EqualTo(new[] { 1, 3, 5 })); Assert.That(blast.Occlusion, Is.EqualTo(new[] { 1, 5 }));
            Assert.That(blast.QueryPosition, Is.Zero); Assert.That(blast.QueryRadius, Is.EqualTo(1.5f));
            Assert.That(blast.BurstPosition, Is.EqualTo(10)); Assert.That(blast.BurstRadius, Is.EqualTo(.5f));
        }
        [Test] public void ClaimPrecedesClosestPointAndGeometryErrorsDoNotPublishAnExplosionOrRemove()
        {
            var target = new Actor(7); var blast = new Blast { Candidates = new[] { new Region(target) } };
            var failure = new InvalidOperationException("closest point");
            blast.FindingClosest = actor =>
            {
                Assert.That(blast.Ledgers[0].Contains(actor.Id), Is.True); throw failure;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => blast.Execute()), Is.SameAs(failure));
            Assert.That(blast.Ledgers[0], Is.EquivalentTo(new[] { 7 })); Assert.That(target.Attempts, Is.Zero);
            Assert.That(blast.Occlusion, Is.Empty); Assert.That(blast.Explosions, Is.Zero); Assert.That(blast.Removals, Is.Zero);
        }
        [Test] public void ReceiveExceptionKeepsAppliedDamageAndClaimButSkipsTheRestOfTheBlast()
        {
            var first = new Actor(1); var second = new Actor(2);
            var blast = new Blast { Candidates = new[] { new Region(first), new Region(second) } }; blast.Angles.Add(30);
            var failure = new InvalidOperationException("receive"); blast.Receiving = actor => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => blast.Execute()), Is.SameAs(failure));
            Assert.That(first.Health, Is.EqualTo(82)); Assert.That(second.Attempts, Is.Zero);
            Assert.That(blast.Ledgers[0], Is.EquivalentTo(new[] { 1 })); Assert.That(blast.Explosions, Is.Zero);
            Assert.That(blast.Fragments, Is.Empty); Assert.That(blast.Removals, Is.Zero);
        }
        [Test] public void DamageThenExplosionThenAuthoredAngleSpawnAndImmediateCurrentPositionSweepPrecedeRemoval()
        {
            var blast = new Blast { Candidates = new[] { new Region(new Actor(7)) } };
            blast.Angles.AddRange(new[] { 150f, 30f, 120f, 60f });
            blast.Exploding = () => { blast.Position = 2; blast.Owner = new Actor(8); };
            blast.Spawning = fragment => fragment.Position += .25f;
            blast.Sweeping = fragment => blast.Position += 1;
            blast.Execute();
            Assert.That(blast.Calls, Is.EqualTo(new[] { "begin", "query", "select:0", "claim:7", "closest:7", "occlusion:7", "receive:7", "exploded",
                "count", "angle:0", "spawn:0", "sweep:0", "count", "angle:1", "spawn:1", "sweep:1",
                "count", "angle:2", "spawn:2", "sweep:2", "count", "angle:3", "spawn:3", "sweep:3", "count", "remove" }));
            Assert.That(blast.Fragments.ConvertAll(fragment => fragment.Angle), Is.EqualTo(new[] { 150f, 30f, 120f, 60f }));
            Assert.That(blast.Fragments.ConvertAll(fragment => fragment.Origin), Is.EqualTo(new[] { 2f, 3f, 4f, 5f }));
            Assert.That(blast.Fragments.ConvertAll(fragment => fragment.SweptPosition), Is.EqualTo(new[] { 2.25f, 3.25f, 4.25f, 5.25f }));
            foreach (var fragment in blast.Fragments)
            { Assert.That(fragment.OwnerId, Is.EqualTo(8)); Assert.That(fragment.Sweeps, Is.EqualTo(1)); }
            Assert.That(blast.Removals, Is.EqualTo(1));
        }
        [Test] public void FragmentLoopReadsTheCurrentCountAndNextAuthoredAngleAfterEachImmediateSweep()
        {
            var blast = new Blast(); blast.Angles.AddRange(new[] { 30f, 60f, 120f, 150f });
            blast.Sweeping = fragment =>
            {
                if (fragment.Index != 0) return;
                blast.Angles[1] = -45.125f; blast.Angles.RemoveRange(2, 2);
            };
            blast.Execute();
            Assert.That(blast.Fragments.Count, Is.EqualTo(2)); Assert.That(Bits(blast.Fragments[1].Angle), Is.EqualTo(Bits(-45.125f)));
            Assert.That(blast.Calls, Is.EqualTo(new[] { "begin", "query", "exploded", "count", "angle:0", "spawn:0", "sweep:0",
                "count", "angle:1", "spawn:1", "sweep:1", "count", "remove" }));
        }
        [TestCase("angle")] [TestCase("spawn")] [TestCase("sweep")]
        public void FragmentExceptionsPreserveEarlierFragmentsAndTheExactPartialAttemptWithoutForcedRemoval(string phase)
        {
            var target = new Actor(1); var blast = new Blast { Candidates = new[] { new Region(target) } };
            blast.Angles.AddRange(new[] { 30f, 60f, 120f }); var failure = new InvalidOperationException(phase);
            blast.ReadingAngle = index => { if (phase == "angle" && index == 1) throw failure; };
            blast.Spawning = fragment => { if (phase == "spawn" && fragment.Index == 1) throw failure; };
            blast.Sweeping = fragment => { if (phase == "sweep" && fragment.Index == 1) throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => blast.Execute()), Is.SameAs(failure));
            Assert.That(target.Health, Is.EqualTo(82)); Assert.That(target.Attempts, Is.EqualTo(1));
            Assert.That(blast.Explosions, Is.EqualTo(1)); Assert.That(blast.Removals, Is.Zero);
            Assert.That(blast.Fragments.Count, Is.EqualTo(phase == "angle" ? 1 : 2));
            Assert.That(blast.Fragments[0].Sweeps, Is.EqualTo(1));
            if (phase != "angle") Assert.That(blast.Fragments[1].Sweeps, Is.EqualTo(phase == "sweep" ? 1 : 0));
            Assert.That(blast.Calls, Does.Not.Contain("angle:2")); Assert.That(blast.Calls, Does.Not.Contain("remove"));
            Assert.That(blast.Calls[blast.Calls.Count - 1], Is.EqualTo(phase == "angle" ? "angle:1" : phase == "spawn" ? "spawn:1" : "sweep:1"));
        }
        [Test] public void ExplosionCallbackExceptionKeepsDamageButDoesNotBeginAnyFragmentOrRemove()
        {
            var target = new Actor(1); var blast = new Blast { Candidates = new[] { new Region(target) } }; blast.Angles.Add(30);
            var failure = new InvalidOperationException("explosion"); blast.Exploding = () => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => blast.Execute()), Is.SameAs(failure));
            Assert.That(target.Health, Is.EqualTo(82)); Assert.That(blast.Explosions, Is.EqualTo(1));
            Assert.That(blast.Fragments, Is.Empty); Assert.That(blast.Removals, Is.Zero); Assert.That(blast.Calls, Does.Not.Contain("count"));
        }
    }
}
