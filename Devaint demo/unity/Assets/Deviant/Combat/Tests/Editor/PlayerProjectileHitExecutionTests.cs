using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerProjectileHitExecutionTests
    {
        sealed class Actor
        {
            public readonly int Id;
            public bool Exists = true, Player, Decoy, Accept = true, Occluded;
            public float Health = 100, CenterX = 1, ClosestX = 1;
            public int Attempts, Hits, Contacts, OcclusionQueries;
            public Actor(int id) { Id = id; }
        }
        sealed class Region
        {
            public Actor Actor;
            public bool Exists = true;
            public Region(Actor actor) { Actor = actor; }
        }
        sealed class Contact
        {
            public bool Valid = true;
            public Region Region;
            public Contact(Actor actor = null) { if (actor != null) Region = new Region(actor); }
        }
        sealed class Receipt
        {
            public Actor Target, Owner;
            public float Damage;
            public int Direction;
            public bool Basic;
        }
        sealed class Shot : IPlayerProjectileExecutionHost
        {
            public readonly PlayerProjectileExecution Execution;
            public readonly List<string> Effects = new List<string>();
            public readonly List<Receipt> Receipts = new List<Receipt>();
            public Actor Owner = new Actor(100) { Player = true };
            public Region[] Candidates = Array.Empty<Region>();
            public float X, VelocityX = -3, ActualSpeed = 3, Damage = 5;
            public object SourceWeapon = new object();
            public int DirectClaims, Removals, Queries, Visits, Visuals, Records;
            public float QueryX, QueryRadius, VisualX, VisualRadius, RecordedDamage, RecordedRadius;
            public Actor VisitPlayer, VisitActor;
            public object VisitWeapon;
            public float VisitDelay, VisitGap;
            public Action DuringRemove, DuringQuery, DuringVisit, DuringVisual, DuringRecord;
            public Action<Actor> DuringReceive;
            public Shot(float blast = 0, bool pierce = false, float delay = 0, float interval = 0, bool noStagger = false)
            {
                Execution = new PlayerProjectileExecution(new RangedProjectileDefinition(new RangedData
                {
                    distance = 6, blastRadius = blast, pierce = pierce, returnDelay = delay,
                    behindGap = .25f, hitInterval = interval, noBasicStagger = noStagger
                }));
            }
            public bool Resolve(Contact contact, float age = -1)
            { var host = new HitHost(this, contact); return PlayerProjectileHitExecution.Resolve(age, ref host); }
            public void Detonate()
            { var host = new BlastHost(this); PlayerProjectileHitExecution.Detonate(ref host); }
            public void Release() => Execution.Release(this);
            public bool Receive(Actor actor, int direction)
            {
                actor.Attempts++;
                Receipts.Add(new Receipt { Target = actor, Owner = Owner, Damage = Damage, Direction = direction, Basic = !Execution.Definition.NoBasicStagger });
                Effects.Add("receive:" + actor.Id);
                if (!actor.Accept || actor.Health <= 0) return false;
                actor.Hits++; actor.Health = Math.Max(0, actor.Health - Damage);
                DuringReceive?.Invoke(actor); return true;
            }
            public void Visit(Actor player, Actor actor)
            {
                Visits++; VisitPlayer = player; VisitActor = actor; VisitWeapon = SourceWeapon;
                VisitDelay = Execution.Definition.ReturnDelay; VisitGap = Execution.Definition.BehindGap;
                Effects.Add("visit:" + actor.Id); DuringVisit?.Invoke();
            }
            public void Remove() { Removals++; Effects.Add("remove"); DuringRemove?.Invoke(); }
            public void SweepByTime(float duration) => throw new InvalidOperationException("Not a flight test.");
            public void SweepByDistance(float distance) => throw new InvalidOperationException("Not a flight test.");
            public void SweepCurrent() => throw new InvalidOperationException("Not a flight test.");
            public void Rotate(float duration) => throw new InvalidOperationException("Not a flight test.");
        }
        struct HitHost : IPlayerProjectileHitHost
        {
            readonly Shot shot;
            readonly Contact contact;
            Region region;
            Actor player;
            public HitHost(Shot shot, Contact contact) { this.shot = shot; this.contact = contact; region = null; player = null; }
            public bool ValidContact => contact.Valid;
            public float BlastRadius => shot.Execution.Definition.BlastRadius;
            public void Detonate() => shot.Detonate();
            public bool CaptureHitRegion() { region = contact.Region; return region != null && region.Exists; }
            public bool TryClaimActor(float age)
            {
                shot.DirectClaims++;
                return shot.Execution.TryClaimActor(region.Actor.Id, shot.Execution.Definition.HitInterval > 0 ? shot.ActualSpeed : 0, age);
            }
            public bool Receive() => shot.Receive(region.Actor, shot.VelocityX < 0 ? -1 : 1);
            public float ReturnDelay => shot.Execution.Definition.ReturnDelay;
            public bool TargetDead => region.Actor.Health <= 0;
            public bool CaptureReturnPlayer()
            {
                if (shot.Owner == null || !shot.Owner.Player) return false;
                player = shot.Owner; return true;
            }
            public void VisitTarget() => shot.Visit(player, region.Actor);
            public bool Pierce => shot.Execution.Definition.Pierce;
            public void Release() => shot.Release();
        }
        struct BlastHost : IPlayerProjectileBlastHost
        {
            readonly Shot shot;
            HashSet<int> seen;
            Region[] candidates;
            int index;
            Region region;
            float contactX;
            public BlastHost(Shot shot)
            { this.shot = shot; seen = null; candidates = null; index = -1; region = null; contactX = 0; }
            public bool Removed => shot.Execution.Removed;
            public float BlastRadius => shot.Execution.Definition.BlastRadius;
            public void Release() => shot.Release();
            public void BeginCandidates()
            {
                seen = new HashSet<int>(); shot.Queries++; shot.QueryX = shot.X;
                shot.QueryRadius = shot.Execution.Definition.BlastRadius; shot.Effects.Add("query");
                shot.DuringQuery?.Invoke(); candidates = shot.Candidates;
            }
            public bool NextCandidate()
            {
                if (++index >= candidates.Length) return false;
                region = candidates[index]; return true;
            }
            public bool RegionExists => region != null && region.Exists;
            public bool ActorExists => region.Actor != null && region.Actor.Exists;
            public bool ActorIsOwner => ReferenceEquals(region.Actor, shot.Owner);
            public bool ActorIsPlayer => region.Actor.Player;
            public bool ActorIsDecoy => region.Actor.Decoy;
            public bool ActorDead => region.Actor.Health <= 0;
            public bool TryClaimActor() => seen.Add(region.Actor.Id);
            public void CaptureContact() { region.Actor.Contacts++; contactX = region.Actor.ClosestX; }
            public float ContactDistance => Math.Abs(shot.X - contactX);
            public bool ContactOccluded { get { region.Actor.OcclusionQueries++; return region.Actor.Occluded; } }
            public void Receive() => shot.Receive(region.Actor, region.Actor.CenterX < shot.X ? -1 : 1);
            public void ShowExplosion()
            {
                shot.Visuals++; shot.VisualX = shot.X; shot.VisualRadius = shot.Execution.Definition.BlastRadius;
                shot.Effects.Add("visual"); shot.DuringVisual?.Invoke();
            }
            public void RecordExplosion()
            {
                shot.Records++; shot.RecordedDamage = shot.Damage; shot.RecordedRadius = shot.Execution.Definition.BlastRadius;
                shot.Effects.Add("record"); shot.DuringRecord?.Invoke();
            }
        }

        [Test] public void InvalidContactDoesNotEnterBlastClaimOrRemoval()
        {
            var shot = new Shot(blast: 3); var actor = new Actor(1);
            shot.Candidates = new[] { new Region(actor) };
            Assert.That(shot.Resolve(new Contact(actor) { Valid = false }), Is.False);
            Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.Effects, Is.Empty);
            Assert.That(shot.DirectClaims, Is.Zero); Assert.That(actor.Attempts, Is.Zero);
        }
        [Test] public void BlastContactIsTerminalWithoutDirectClaimReturnVisitOrPierce()
        {
            var shot = new Shot(blast: 3, pierce: true, delay: 5); var actor = new Actor(1);
            shot.Candidates = new[] { new Region(actor), new Region(actor) };
            Assert.That(shot.Resolve(new Contact(actor)), Is.True);
            Assert.That(shot.DirectClaims, Is.Zero); Assert.That(shot.Visits, Is.Zero);
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(actor.Attempts, Is.EqualTo(1));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "remove", "query", "receive:1", "visual", "record" }));
        }
        [Test] public void SolidContactReleasesEvenAPiercingShotWithoutAnActorClaim()
        {
            var shot = new Shot(pierce: true, delay: 5);
            Assert.That(shot.Resolve(new Contact()), Is.True);
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.DirectClaims, Is.Zero);
            Assert.That(shot.Effects, Is.EqualTo(new[] { "remove" }));
        }
        [Test] public void RejectedLifetimeClaimDoesNotReceiveVisitOrRemove()
        {
            var shot = new Shot(delay: 5, interval: .25f); var actor = new Actor(1);
            Assert.That(shot.Resolve(new Contact(actor), 2), Is.False);
            Assert.That(actor.Attempts, Is.Zero); Assert.That(shot.Visits, Is.Zero);
            Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.Effects, Is.Empty);
            Assert.That(shot.Resolve(new Contact(actor), 1.75f), Is.True);
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Health, Is.EqualTo(95));
        }
        [TestCase(false)] [TestCase(true)]
        public void RejectedReceiveSpendsTheClaimAndStillAppliesPierceOrRelease(bool pierce)
        {
            var shot = new Shot(pierce: pierce, delay: 5); var actor = new Actor(1) { Accept = false };
            var contact = new Contact(actor);
            Assert.That(shot.Resolve(contact), Is.EqualTo(!pierce));
            actor.Accept = true;
            Assert.That(shot.Resolve(contact), Is.False);
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Health, Is.EqualTo(100));
            Assert.That(shot.Visits, Is.Zero); Assert.That(shot.Execution.Removed, Is.EqualTo(!pierce));
            Assert.That(shot.Removals, Is.EqualTo(pierce ? 0 : 1));
        }
        [TestCase("killed")] [TestCase("non-player")] [TestCase("missing-owner")]
        [TestCase("zero-delay")] [TestCase("negative-delay")]
        public void AcceptedReceiveVisitsOnlyWithPositiveDelayLiveTargetAndPlayerOwner(string scenario)
        {
            float delay = scenario == "zero-delay" ? 0 : scenario == "negative-delay" ? -1 : 5;
            var shot = new Shot(delay: delay); var actor = new Actor(1);
            if (scenario == "killed") shot.Damage = 100;
            if (scenario == "non-player") shot.Owner.Player = false;
            if (scenario == "missing-owner") shot.Owner = null;
            Assert.That(shot.Resolve(new Contact(actor)), Is.True);
            Assert.That(actor.Hits, Is.EqualTo(1)); Assert.That(shot.Visits, Is.Zero);
            Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void ReceiveReentrySeesTheCommittedClaimBeforeAnyReturnOrRelease()
        {
            var shot = new Shot(delay: 5); var actor = new Actor(1); var contact = new Contact(actor);
            shot.DuringReceive = _ =>
            {
                Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.Visits, Is.Zero);
                Assert.That(shot.Resolve(contact), Is.False);
            };
            Assert.That(shot.Resolve(contact), Is.True);
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Health, Is.EqualTo(95));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "visit:1", "remove" }));
        }
        [Test] public void ReturnUsesThePostReceiveRegionActorOwnerAndSourceWeapon()
        {
            var shot = new Shot(delay: 5, noStagger: true); var actor = new Actor(1);
            var replacement = new Actor(2); var player = new Actor(101) { Player = true };
            var contact = new Contact(actor); var initialOwner = shot.Owner; var weapon = new object();
            shot.DuringReceive = _ =>
            {
                contact.Region.Actor = replacement; shot.Owner = player; shot.SourceWeapon = weapon;
                shot.VelocityX = 9;
            };
            Assert.That(shot.Resolve(contact), Is.True);
            Assert.That(shot.Receipts[0].Target, Is.SameAs(actor)); Assert.That(shot.Receipts[0].Owner, Is.SameAs(initialOwner));
            Assert.That(shot.Receipts[0].Direction, Is.EqualTo(-1)); Assert.That(shot.Receipts[0].Basic, Is.False);
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(replacement.Attempts, Is.Zero);
            Assert.That(shot.VisitPlayer, Is.SameAs(player)); Assert.That(shot.VisitActor, Is.SameAs(replacement));
            Assert.That(shot.VisitWeapon, Is.SameAs(weapon)); Assert.That(shot.VisitDelay, Is.EqualTo(5));
            Assert.That(shot.VisitGap, Is.EqualTo(.25f));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "visit:2", "remove" }));
        }
        [TestCase(false)] [TestCase(true)]
        public void RemovalDuringReceiveDoesNotAbortEnteredReturnOrChangePierceResult(bool pierce)
        {
            var shot = new Shot(pierce: pierce, delay: 5); var actor = new Actor(1);
            shot.DuringReceive = _ => shot.Release();
            Assert.That(shot.Resolve(new Contact(actor)), Is.EqualTo(!pierce));
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "remove", "visit:1" }));
        }
        [TestCase(false)] [TestCase(true)]
        public void VisitRunsBeforePierceOrFinalReleaseAndMayReenterRemoval(bool pierce)
        {
            var shot = new Shot(pierce: pierce, delay: 5); var actor = new Actor(1);
            shot.DuringVisit = () =>
            {
                Assert.That(shot.Execution.Removed, Is.False); shot.Release();
            };
            Assert.That(shot.Resolve(new Contact(actor)), Is.EqualTo(!pierce));
            Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "visit:1", "remove" }));
        }
        [TestCase(false)] [TestCase(true)]
        public void ReceiveOrVisitExceptionRetainsTheClaimAndCompletedDamageWithoutFinalRelease(bool visit)
        {
            var shot = new Shot(delay: 5); var actor = new Actor(1); var contact = new Contact(actor);
            if (visit) shot.DuringVisit = () => throw new InvalidOperationException("visit");
            else shot.DuringReceive = _ => throw new InvalidOperationException("receive");
            Assert.Throws<InvalidOperationException>(() => shot.Resolve(contact));
            Assert.That(shot.Resolve(contact), Is.False);
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Health, Is.EqualTo(95));
            Assert.That(shot.Visits, Is.EqualTo(visit ? 1 : 0)); Assert.That(shot.Execution.Removed, Is.False);
            Assert.That(shot.Removals, Is.Zero);
        }
        [Test] public void RecurringContactReadsCurrentActualSpeedBeforeSpendingTheClaim()
        {
            var shot = new Shot(pierce: true, interval: .25f); var actor = new Actor(1);
            shot.ActualSpeed = 6;
            Assert.That(shot.Resolve(new Contact(actor), 1), Is.False);
            Assert.That(actor.Attempts, Is.Zero);
            shot.ActualSpeed = 3; shot.VelocityX = 3;
            Assert.That(shot.Resolve(new Contact(actor), 1), Is.False);
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(shot.Receipts[0].Direction, Is.EqualTo(1));
            Assert.That(shot.Execution.Removed, Is.False);
        }

        [TestCase(0f)] [TestCase(-1f)]
        public void NonpositiveBlastRadiusDoesNotRemoveOrQuery(float radius)
        {
            var shot = new Shot(blast: radius); shot.Detonate();
            Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.Effects, Is.Empty);
        }
        [Test] public void RemovalIsCommittedBeforeQueryAndBothCallbacksCannotDetonateAgain()
        {
            var shot = new Shot(blast: 3); var actor = new Actor(1); shot.Candidates = new[] { new Region(actor) };
            shot.DuringRemove = () => { Assert.That(shot.Execution.Removed, Is.True); shot.Detonate(); };
            shot.DuringQuery = () => { Assert.That(shot.Execution.Removed, Is.True); shot.Detonate(); };
            shot.Detonate(); shot.Detonate();
            Assert.That(shot.Removals, Is.EqualTo(1)); Assert.That(shot.Queries, Is.EqualTo(1));
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Health, Is.EqualTo(95));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "remove", "query", "receive:1", "visual", "record" }));
        }
        [Test] public void BlastFiltersNativeCandidatesBeforeClaimAndGeometry()
        {
            var shot = new Shot(blast: 3); var player = new Actor(2) { Player = true };
            var decoy = new Actor(3) { Decoy = true }; var dead = new Actor(4) { Health = 0 };
            var missing = new Actor(5) { Exists = false }; var actor = new Actor(1);
            shot.Candidates = new[]
            {
                null, new Region(null), new Region(actor) { Exists = false }, new Region(missing),
                new Region(shot.Owner), new Region(player), new Region(decoy), new Region(dead), new Region(actor), new Region(actor)
            };
            shot.Detonate();
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(actor.Attempts, Is.EqualTo(1));
            Assert.That(actor.Contacts, Is.EqualTo(1));
            foreach (var filtered in new[] { shot.Owner, player, decoy, dead, missing })
            { Assert.That(filtered.Attempts, Is.Zero); Assert.That(filtered.Contacts, Is.Zero); }
        }
        [Test] public void GeometryRejectionAlreadyClaimsTheActorBeforeLaterCallbacksExposeIt()
        {
            var shot = new Shot(blast: 3); var far = new Actor(1) { ClosestX = 4 };
            var blocked = new Actor(2) { Occluded = true }; var mover = new Actor(3);
            shot.Candidates = new[] { new Region(far), new Region(blocked), new Region(mover), new Region(far), new Region(blocked) };
            shot.DuringReceive = actor => { far.ClosestX = 1; blocked.Occluded = false; };
            shot.Detonate();
            Assert.That(mover.Health, Is.EqualTo(95)); Assert.That(far.Attempts, Is.Zero); Assert.That(blocked.Attempts, Is.Zero);
            Assert.That(far.Contacts, Is.EqualTo(1)); Assert.That(far.OcclusionQueries, Is.Zero);
            Assert.That(blocked.Contacts, Is.EqualTo(1)); Assert.That(blocked.OcclusionQueries, Is.EqualTo(1));
        }
        [TestCase(3f, true)] [TestCase(3.00005f, true)] [TestCase(3.0002f, false)]
        public void BlastDistanceToleranceIsCheckedBeforeOcclusion(float distance, bool hit)
        {
            var shot = new Shot(blast: 3); var actor = new Actor(1) { ClosestX = distance };
            shot.Candidates = new[] { new Region(actor) }; shot.Detonate();
            Assert.That(actor.Attempts, Is.EqualTo(hit ? 1 : 0)); Assert.That(actor.Health, Is.EqualTo(hit ? 95 : 100));
            Assert.That(actor.OcclusionQueries, Is.EqualTo(hit ? 1 : 0));
        }
        [Test] public void RejectedBlastReceiveStillVisitsLaterActorsAndCompletesPresentation()
        {
            var shot = new Shot(blast: 3); var rejected = new Actor(1) { Accept = false }; var accepted = new Actor(2);
            shot.Candidates = new[] { new Region(rejected), new Region(rejected), new Region(accepted) }; shot.Detonate();
            Assert.That(rejected.Attempts, Is.EqualTo(1)); Assert.That(rejected.Health, Is.EqualTo(100));
            Assert.That(accepted.Health, Is.EqualTo(95)); Assert.That(shot.Visuals, Is.EqualTo(1)); Assert.That(shot.Records, Is.EqualTo(1));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "remove", "query", "receive:1", "receive:2", "visual", "record" }));
        }
        [Test] public void FilteredActorCanBecomeEligibleAtALaterColliderWithoutAPrematureClaim()
        {
            var shot = new Shot(blast: 3); var actor = new Actor(1) { Decoy = true }; var mover = new Actor(2);
            shot.Candidates = new[] { new Region(actor), new Region(mover), new Region(actor) };
            shot.DuringReceive = target => { if (ReferenceEquals(target, mover)) actor.Decoy = false; };
            shot.Detonate();
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Health, Is.EqualTo(95));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "remove", "query", "receive:2", "receive:1", "visual", "record" }));
        }
        [Test] public void BlastCallbacksLeaveLaterGeometryDamageOwnerDirectionAndVisualPositionLive()
        {
            var shot = new Shot(blast: 3, noStagger: true); var first = new Actor(1); var second = new Actor(2);
            var initialOwner = shot.Owner; var nextOwner = new Actor(101) { Player = true };
            shot.Candidates = new[] { new Region(first), new Region(second) };
            shot.DuringReceive = actor =>
            {
                if (!ReferenceEquals(actor, first)) return;
                shot.X = 10; shot.Damage = 7; shot.Owner = nextOwner;
                second.ClosestX = 12; second.CenterX = 9;
            };
            shot.Detonate();
            Assert.That(first.Health, Is.EqualTo(95)); Assert.That(second.Health, Is.EqualTo(93));
            Assert.That(shot.Receipts[0].Owner, Is.SameAs(initialOwner)); Assert.That(shot.Receipts[0].Direction, Is.EqualTo(1));
            Assert.That(shot.Receipts[1].Owner, Is.SameAs(nextOwner)); Assert.That(shot.Receipts[1].Direction, Is.EqualTo(-1));
            Assert.That(shot.Receipts[1].Damage, Is.EqualTo(7)); Assert.That(shot.Receipts[1].Basic, Is.False);
            Assert.That(shot.QueryX, Is.Zero); Assert.That(shot.QueryRadius, Is.EqualTo(3));
            Assert.That(shot.VisualX, Is.EqualTo(10)); Assert.That(shot.VisualRadius, Is.EqualTo(3));
            Assert.That(shot.RecordedDamage, Is.EqualTo(7)); Assert.That(shot.RecordedRadius, Is.EqualTo(3));
        }
        [Test] public void EachProjectileGetsAFreshBlastActorSet()
        {
            var actor = new Actor(1); var candidates = new[] { new Region(actor), new Region(actor) };
            var first = new Shot(blast: 3) { Candidates = candidates }; var second = new Shot(blast: 3) { Candidates = candidates };
            first.Detonate(); second.Detonate();
            Assert.That(actor.Attempts, Is.EqualTo(2)); Assert.That(actor.Health, Is.EqualTo(90));
            Assert.That(first.Removals, Is.EqualTo(1)); Assert.That(second.Removals, Is.EqualTo(1));
        }
        [TestCase("remove")] [TestCase("query")] [TestCase("receive")] [TestCase("visual")] [TestCase("record")]
        public void BlastExceptionKeepsOnlyTheAlreadyCompletedCallbackPrefixAndTerminalClaim(string phase)
        {
            var shot = new Shot(blast: 3); var first = new Actor(1); var second = new Actor(2);
            shot.Candidates = new[] { new Region(first), new Region(second) };
            Action fail = () => throw new InvalidOperationException(phase);
            if (phase == "remove") shot.DuringRemove = fail;
            if (phase == "query") shot.DuringQuery = fail;
            if (phase == "receive") shot.DuringReceive = _ => fail();
            if (phase == "visual") shot.DuringVisual = fail;
            if (phase == "record") shot.DuringRecord = fail;
            Assert.Throws<InvalidOperationException>(() => shot.Detonate());
            shot.Detonate(); Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
            var expected = new List<string> { "remove" };
            if (phase != "remove") expected.Add("query");
            if (phase != "remove" && phase != "query") expected.Add("receive:1");
            if (phase == "visual" || phase == "record") { expected.Add("receive:2"); expected.Add("visual"); }
            if (phase == "record") expected.Add("record");
            Assert.That(shot.Effects, Is.EqualTo(expected));
            Assert.That(first.Health, Is.EqualTo(phase == "remove" || phase == "query" ? 100 : 95));
            Assert.That(second.Health, Is.EqualTo(phase == "visual" || phase == "record" ? 95 : 100));
            Assert.That(shot.Visuals, Is.EqualTo(phase == "visual" || phase == "record" ? 1 : 0));
            Assert.That(shot.Records, Is.EqualTo(phase == "record" ? 1 : 0));
        }
    }
}
