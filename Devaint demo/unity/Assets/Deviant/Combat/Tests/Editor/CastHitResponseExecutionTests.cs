using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastHitResponseExecutionTests
    {
        sealed class Actor
        {
            public readonly int Id;
            public bool Exists = true, Boss, Accept = true;
            public float Health = 100, KnockUntil, KnockSpeed;
            public int Attempts, Hits;
            public Actor(int id) { Id = id; }
        }
        sealed class Movement
        {
            public bool Primary = true;
            public float EndDamage = 17, Speed = 4, Recovery = .25f;
        }
        sealed class Hit
        {
            public float Damage = 5;
            public bool Nearest, Push;
        }
        sealed class Cast
        {
            public CastHitResponseExecution Response;
            public readonly HashSet<(int, int)> Claims = new HashSet<(int, int)>();
            public readonly List<Actor> Enemies = new List<Actor>();
            public readonly List<string> Effects = new List<string>();
            public Actor Marked;
            public Movement Movement = new Movement();
            public bool Moving = true, Cancelled, Charged, TargetHit, Recast;
            public float Now = .375f, Started = .125f, Step = .03125f, Multiplier = 1.5f, RecastWindow = .5f;
            public float? ReleasedDamage;
            public float MotionEnd = 3, RecastDeadline;
            public int Direction = -1, BossStops, ApproachMarks, MovingReads, CancelledReads, EnumerationStarts, EnumerationEnds;
            public Action<Actor, bool> Receiving;
            public Action<Actor> Knocking;
            public void Apply(int index, Actor actor, Hit hit = null)
            {
                var host = new HitHost(this, actor, hit ?? new Hit(), index); Response.ApplyTarget(ref host);
            }
            public void End() { var host = new HitHost(this); Response.EndPrimary(ref host); }
            public void Push(float step) { var host = new PushHost(this); Response.PushTargets(step, ref host); }
            public bool Receive(Actor actor, float damage, bool end)
            {
                actor.Attempts++;
                if (!actor.Accept || actor.Health <= 0) return false;
                actor.Hits++; actor.Health -= damage; Effects.Add((end ? "end:" : "hit:") + actor.Id);
                Receiving?.Invoke(actor, end); return true;
            }
        }
        readonly struct HitHost : ICastHitResponseHost
        {
            readonly Cast cast;
            readonly Actor target;
            readonly Hit hit;
            readonly int index;
            public HitHost(Cast cast, Actor target = null, Hit hit = null, int index = 0)
            { this.cast = cast; this.target = target; this.hit = hit; this.index = index; }
            public bool TryClaimTarget() => cast.Claims.Add((index, target.Id));
            public float HitDamage => cast.ReleasedDamage ?? hit.Damage;
            public float DamageMultiplier => cast.Multiplier;
            public bool ReceiveControl(float damage) => cast.Receive(target, damage, false);
            public bool TrackPrimaryTarget => hit.Nearest;
            public void MarkTarget() { cast.Marked = target; cast.TargetHit = true; cast.Effects.Add("mark:" + target.Id); }
            public void MarkApproachAccepted()
            {
                cast.ApproachMarks++; if (cast.Recast) cast.RecastDeadline = cast.Now + cast.RecastWindow;
            }
            public bool HasResponseMovement => !cast.Charged && cast.Movement != null;
            public bool PrimaryPush => cast.Movement.Primary;
            public bool MarkedTargetExists => cast.Marked != null && cast.Marked.Exists;
            public bool TargetIsBoss => target.Boss;
            public bool TargetIsMarked => ReferenceEquals(target, cast.Marked);
            public int TargetActorId => target.Id;
            public bool PushWithCaster => hit.Push;
            public float EndForcedDashAtBoss()
            {
                cast.BossStops++; cast.Moving = false; cast.Effects.Add("stop");
                return cast.Now - cast.Started + cast.Step + cast.Movement.Recovery;
            }
            public void SetMotionEnd(float end) { cast.MotionEnd = end; cast.Effects.Add("motion-end"); }
            public bool Cancelled => cast.Cancelled;
            public bool MovementExists => cast.Movement != null;
            public bool MarkedTargetAlive => cast.Marked != null && cast.Marked.Exists && cast.Marked.Health > 0;
            public float EndDamage => cast.Movement.EndDamage;
            public void ReceiveMarked(float damage) => cast.Receive(cast.Marked, damage, true);
        }
        struct PushHost : ICastPushHost
        {
            readonly Cast cast;
            List<Actor>.Enumerator enemies;
            public PushHost(Cast cast) { this.cast = cast; enemies = default; }
            public bool Moving { get { cast.MovingReads++; return cast.Moving; } }
            public bool Cancelled { get { cast.CancelledReads++; return cast.Cancelled; } }
            public void BeginEnemies() { cast.EnumerationStarts++; enemies = cast.Enemies.GetEnumerator(); }
            public bool NextEnemy() => enemies.MoveNext();
            public void EndEnemies() { cast.EnumerationEnds++; enemies.Dispose(); }
            public bool EnemyExists => enemies.Current != null && enemies.Current.Exists;
            public bool EnemyDead => enemies.Current.Health <= 0;
            public int EnemyActorId => enemies.Current.Id;
            public float Now => cast.Now;
            public float MovementSpeed => cast.Movement.Speed;
            public int Direction => cast.Direction;
            public void SetEnemyKnockMotion(float until, float speed)
            {
                var actor = enemies.Current; actor.KnockUntil = until; actor.KnockSpeed = speed;
                cast.Effects.Add("push:" + actor.Id); cast.Knocking?.Invoke(actor);
            }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        [Test] public void TargetClaimPrecedesDamageAndSurvivesReceiveReentry()
        {
            var cast = new Cast(); var actor = new Actor(1); int callbacks = 0;
            cast.Receiving = (target, end) =>
            {
                Assert.That(cast.Claims.Contains((0, actor.Id)), Is.True); Assert.That(cast.TargetHit, Is.False);
                callbacks++; cast.Apply(0, actor);
            };
            cast.Apply(0, actor); cast.Apply(0, actor);
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(actor.Hits, Is.EqualTo(1)); Assert.That(actor.Attempts, Is.EqualTo(1));
            Assert.That(actor.Health, Is.EqualTo(92.5f)); Assert.That(cast.Marked, Is.SameAs(actor));
        }
        [TestCase(false)] [TestCase(true)]
        public void RejectedAndDeadTargetsSpendTheExistingClaimWithoutMarkOrPush(bool dead)
        {
            var cast = new Cast(); var actor = new Actor(1) { Accept = dead, Health = dead ? 0 : 100 };
            cast.Enemies.Add(actor); cast.Apply(0, actor, new Hit { Nearest = true, Push = true });
            actor.Accept = true; actor.Health = 100; cast.Apply(0, actor); cast.Push(.125f);
            Assert.That(actor.Attempts, Is.EqualTo(1)); Assert.That(actor.Hits, Is.Zero);
            Assert.That(cast.Marked, Is.Null); Assert.That(cast.TargetHit, Is.False); Assert.That(cast.ApproachMarks, Is.Zero);
            Assert.That(actor.KnockUntil, Is.Zero);
        }
        [Test] public void ClaimKeysRemainPerHitAndTargetWithoutASecondResponseLedger()
        {
            var cast = new Cast(); var first = new Actor(1); var second = new Actor(2);
            cast.Apply(0, first); cast.Apply(0, second); cast.Apply(1, first); cast.Apply(1, second);
            cast.Apply(0, first); cast.Apply(1, second);
            Assert.That(first.Hits, Is.EqualTo(2)); Assert.That(second.Hits, Is.EqualTo(2));
            Assert.That(cast.Claims.Count, Is.EqualTo(4)); Assert.That(cast.Marked, Is.SameAs(first));
        }
        [Test] public void ReleaseDamageReplacesOnlyTheHitBaseAndUsesTheCurrentMultiplier()
        {
            var cast = new Cast { ReleasedDamage = 12.5f, Multiplier = 1.25f };
            var actor = new Actor(1); cast.Apply(0, actor, new Hit { Damage = 999 });
            Assert.That(Bits(actor.Health), Is.EqualTo(Bits(100 - 12.5f * 1.25f)));
            cast.Multiplier = 2; cast.End();
            Assert.That(Bits(actor.Health), Is.EqualTo(Bits(100 - 12.5f * 1.25f - 17 * 2)));
        }
        [Test] public void AcceptedNearestReplacesAnExistingMarkAndMarksApproachAtPostReceiveTime()
        {
            var cast = new Cast { Recast = true, Marked = new Actor(2) }; var actor = new Actor(1);
            cast.Receiving = (target, end) => cast.Now = 2.375f;
            cast.Apply(0, actor, new Hit { Nearest = true });
            Assert.That(cast.Marked, Is.SameAs(actor)); Assert.That(cast.TargetHit, Is.True);
            Assert.That(cast.ApproachMarks, Is.EqualTo(1)); Assert.That(cast.RecastDeadline, Is.EqualTo(2.875f));
            Assert.That(cast.Effects, Is.EqualTo(new[] { "hit:1", "mark:1" }));
        }
        [Test] public void APreexistingNearestBossMarkDoesNotEnterTheUnmarkedPrimaryStopBranch()
        {
            var cast = new Cast(); var actor = new Actor(1) { Boss = true };
            cast.Apply(0, actor, new Hit { Nearest = true });
            Assert.That(cast.BossStops, Is.Zero); Assert.That(actor.Hits, Is.EqualTo(1)); Assert.That(cast.Moving, Is.True);
            cast.End(); Assert.That(actor.Hits, Is.EqualTo(2));
        }
        [TestCase("charged")] [TestCase("no-movement")] [TestCase("not-primary")]
        public void OnlyAnUnmarkedPrimaryMovementClaimsTheFirstAcceptedTarget(string scenario)
        {
            var cast = new Cast(); var actor = new Actor(1); cast.Enemies.Add(actor);
            if (scenario == "charged") cast.Charged = true;
            if (scenario == "no-movement") cast.Movement = null;
            if (scenario == "not-primary") cast.Movement.Primary = false;
            cast.Apply(0, actor); cast.Moving = false; cast.Push(.125f);
            Assert.That(cast.Marked, Is.Null); Assert.That(cast.TargetHit, Is.False); Assert.That(cast.BossStops, Is.Zero);
            Assert.That(actor.Hits, Is.EqualTo(1)); Assert.That(actor.KnockUntil, Is.Zero);
        }
        [Test] public void AReceiveCallbackCanInstallMovementAndChangeThePrimaryBossClassification()
        {
            var cast = new Cast { Movement = null }; var actor = new Actor(1);
            cast.Receiving = (target, end) => { if (!end) { cast.Movement = new Movement(); actor.Boss = true; } };
            cast.Apply(0, actor);
            Assert.That(cast.Marked, Is.SameAs(actor)); Assert.That(cast.BossStops, Is.EqualTo(1));
            Assert.That(actor.Hits, Is.EqualTo(2)); Assert.That(actor.Health, Is.EqualTo(67));
            Assert.That(Bits(cast.MotionEnd), Is.EqualTo(Bits(.375f - .125f + .03125f + .25f)));
            Assert.That(cast.Effects, Is.EqualTo(new[] { "hit:1", "mark:1", "stop", "motion-end", "end:1" }));
        }
        [Test] public void ReceiveCanRemoveMovementOrEstablishAnotherMarkBeforePrimarySelection()
        {
            var cast = new Cast(); var actor = new Actor(1); var other = new Actor(2);
            cast.Receiving = (target, end) => { cast.Marked = other; cast.Movement = null; };
            cast.Apply(0, actor, new Hit { Push = true });
            Assert.That(cast.Marked, Is.SameAs(other)); Assert.That(cast.TargetHit, Is.False);
            cast.Movement = new Movement(); cast.Enemies.Add(actor); cast.Push(.125f);
            Assert.That(actor.KnockSpeed, Is.EqualTo(-4), "Explicit PushWithCaster survives a callback changing movement and mark.");
        }
        [Test] public void BossEndCallbacksSeeTheStopAndMotionEndThenCanDemoteBeforeFinalShove()
        {
            var cast = new Cast(); var actor = new Actor(1) { Boss = true }; cast.Enemies.Add(actor);
            cast.Receiving = (target, end) =>
            {
                if (!end) return;
                Assert.That(cast.Moving, Is.False); Assert.That(cast.BossStops, Is.EqualTo(1));
                Assert.That(cast.MotionEnd, Is.EqualTo(.53125f));
                actor.Boss = false; cast.Moving = true; cast.End();
            };
            cast.Apply(0, actor); cast.Push(.125f); cast.End();
            Assert.That(actor.Hits, Is.EqualTo(2)); Assert.That(actor.KnockUntil, Is.EqualTo(.5f));
            Assert.That(actor.KnockSpeed, Is.EqualTo(-4));
        }
        [Test] public void ReplacingTheMarkDoesNotRemoveAnAdmittedNormalVictimFromTheShove()
        {
            var cast = new Cast(); var actor = new Actor(1) { Boss = true }; var replacement = new Actor(2);
            cast.Enemies.Add(actor); cast.Enemies.Add(replacement);
            cast.Receiving = (target, end) => { if (end) { actor.Boss = false; cast.Marked = replacement; cast.Moving = true; } };
            cast.Apply(0, actor); cast.Push(.125f); cast.End();
            Assert.That(cast.Marked, Is.SameAs(replacement)); Assert.That(actor.Hits, Is.EqualTo(2));
            Assert.That(actor.KnockUntil, Is.EqualTo(.5f)); Assert.That(actor.KnockSpeed, Is.EqualTo(-4));
            Assert.That(replacement.Hits, Is.Zero); Assert.That(replacement.KnockUntil, Is.Zero);
        }
        [Test] public void CancellationDuringReceiveDoesNotAbortTheEnteredMarkAndMembershipResponse()
        {
            var cast = new Cast(); var actor = new Actor(1); cast.Enemies.Add(actor);
            cast.Receiving = (target, end) => cast.Cancelled = true;
            cast.Apply(0, actor); Assert.That(cast.Marked, Is.SameAs(actor)); cast.End(); cast.Push(.125f);
            Assert.That(actor.Hits, Is.EqualTo(1)); Assert.That(actor.KnockUntil, Is.Zero);
            cast.Cancelled = false; cast.Push(.125f); Assert.That(actor.KnockSpeed, Is.EqualTo(-4));
            cast.End(); Assert.That(actor.Hits, Is.EqualTo(2), "A cancelled end request must not spend the end-hit claim.");
        }
        [TestCase("cancelled")] [TestCase("missing-movement")] [TestCase("not-primary")]
        public void EndPrimaryIneligibilityDoesNotSpendTheLaterEligibleClaim(string scenario)
        {
            var cast = new Cast { Marked = new Actor(1) };
            if (scenario == "cancelled") cast.Cancelled = true;
            if (scenario == "missing-movement") cast.Movement = null;
            if (scenario == "not-primary") cast.Movement.Primary = false;
            cast.End(); Assert.That(cast.Marked.Hits, Is.Zero);
            cast.Cancelled = false; cast.Movement = new Movement(); cast.End(); cast.End();
            Assert.That(cast.Marked.Hits, Is.EqualTo(1)); Assert.That(cast.Marked.Health, Is.EqualTo(74.5f));
        }
        [TestCase("no-mark")] [TestCase("destroyed")] [TestCase("dead")] [TestCase("rejected")]
        public void EndClaimPrecedesTargetExistenceLifeAndReceiveAcceptance(string scenario)
        {
            var cast = new Cast(); var actor = new Actor(1);
            if (scenario != "no-mark") cast.Marked = actor;
            if (scenario == "destroyed") actor.Exists = false;
            if (scenario == "dead") actor.Health = 0;
            if (scenario == "rejected") actor.Accept = false;
            cast.End(); cast.Marked = actor; actor.Exists = actor.Accept = true; actor.Health = 100; cast.End();
            Assert.That(actor.Hits, Is.Zero); Assert.That(actor.Attempts, Is.EqualTo(scenario == "rejected" ? 1 : 0));
        }
        [Test] public void EndClaimSurvivesReceiveReentryAndAnExceptionAfterNativeDamage()
        {
            var cast = new Cast { Marked = new Actor(1) }; var failure = new InvalidOperationException("end");
            cast.Receiving = (target, end) => { cast.End(); throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => cast.End()), Is.SameAs(failure));
            cast.Receiving = null; cast.End(); Assert.That(cast.Marked.Hits, Is.EqualTo(1)); Assert.That(cast.Marked.Health, Is.EqualTo(74.5f));
        }
        [Test] public void FailedReceiveSpendsOnlyItsOriginalTargetClaimAndLeavesNoAcceptedResponse()
        {
            var cast = new Cast(); var actor = new Actor(1); var failure = new InvalidOperationException("hit");
            cast.Enemies.Add(actor); cast.Receiving = (target, end) => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => cast.Apply(0, actor, new Hit { Nearest = true, Push = true })), Is.SameAs(failure));
            cast.Receiving = null; cast.Apply(0, actor); cast.Push(.125f);
            Assert.That(cast.Marked, Is.Null); Assert.That(cast.ApproachMarks, Is.Zero); Assert.That(actor.KnockUntil, Is.Zero);
            cast.Apply(1, actor); Assert.That(actor.Hits, Is.EqualTo(2)); Assert.That(cast.Marked, Is.SameAs(actor));
        }
        [Test] public void BossEndFailureRetainsTheEndClaimButDoesNotAddTheUnfinishedShove()
        {
            var cast = new Cast(); var actor = new Actor(1) { Boss = true }; cast.Enemies.Add(actor);
            var failure = new InvalidOperationException("boss-end");
            cast.Receiving = (target, end) => { if (end) { actor.Boss = false; cast.Moving = true; throw failure; } };
            Assert.That(Assert.Throws<InvalidOperationException>(() => cast.Apply(0, actor)), Is.SameAs(failure));
            cast.Receiving = null; cast.Apply(0, actor); cast.End(); cast.Push(.125f);
            Assert.That(actor.Hits, Is.EqualTo(2)); Assert.That(cast.BossStops, Is.EqualTo(1)); Assert.That(actor.KnockUntil, Is.Zero);
        }
        [Test] public void EveryPrimaryVictimAndExplicitPushJoinTheShoveExceptBosses()
        {
            var cast = new Cast(); var primary = new Actor(1); var ordinary = new Actor(2); var pushed = new Actor(3); var boss = new Actor(4) { Boss = true };
            cast.Enemies.AddRange(new[] { primary, ordinary, pushed, boss });
            cast.Apply(0, primary); cast.Apply(0, ordinary); cast.Apply(0, pushed, new Hit { Push = true }); cast.Apply(0, boss, new Hit { Push = true });
            cast.Push(.125f);
            Assert.That(primary.KnockSpeed, Is.EqualTo(-4)); Assert.That(pushed.KnockSpeed, Is.EqualTo(-4));
            Assert.That(ordinary.KnockSpeed, Is.EqualTo(-4)); Assert.That(boss.KnockUntil, Is.Zero);
            primary.Boss = true; cast.Push(.25f);
            Assert.That(primary.KnockUntil, Is.EqualTo(.625f), "Push uses current life/membership, not a new boss-classification gate.");
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void PushAdmissionChecksMovingThenCancellationOnceBeforeEnumerating(bool moving, bool cancelled)
        {
            var cast = new Cast { Moving = moving, Cancelled = cancelled }; cast.Push(.125f);
            Assert.That(cast.MovingReads, Is.EqualTo(1)); Assert.That(cast.CancelledReads, Is.EqualTo(moving ? 1 : 0));
            Assert.That(cast.EnumerationStarts, Is.Zero); Assert.That(cast.EnumerationEnds, Is.Zero);
        }
        [Test] public void PushReadsTheLiveListLifeMembershipClockSpeedAndDirectionAtEachMember()
        {
            var cast = new Cast(); var first = new Actor(1); var second = new Actor(2); var outsider = new Actor(3); var dead = new Actor(4); var gone = new Actor(5);
            foreach (var actor in new[] { first, second, dead, gone }) cast.Apply(0, actor, new Hit { Push = true });
            dead.Health = 0; gone.Exists = false; cast.Enemies.AddRange(new[] { first, null, outsider, dead, gone, second });
            cast.Knocking = actor => { if (actor == first) { cast.Now = 2.25f; cast.Movement.Speed = 6; cast.Direction = 1; cast.Cancelled = true; cast.Moving = false; } };
            cast.Push(.125f);
            Assert.That(first.KnockUntil, Is.EqualTo(.5f)); Assert.That(first.KnockSpeed, Is.EqualTo(-4));
            Assert.That(second.KnockUntil, Is.EqualTo(2.375f)); Assert.That(second.KnockSpeed, Is.EqualTo(6));
            Assert.That(dead.KnockUntil, Is.Zero); Assert.That(gone.KnockUntil, Is.Zero); Assert.That(outsider.KnockUntil, Is.Zero);
            Assert.That(cast.MovingReads, Is.EqualTo(1)); Assert.That(cast.CancelledReads, Is.EqualTo(1));
            Assert.That(cast.EnumerationStarts, Is.EqualTo(1)); Assert.That(cast.EnumerationEnds, Is.EqualTo(1));
            cast.Knocking = null; cast.Cancelled = false; cast.Moving = true; cast.Enemies.Remove(first); first.KnockUntil = 0;
            dead.Health = 100; gone.Exists = true; cast.Push(.25f);
            Assert.That(first.KnockUntil, Is.Zero); Assert.That(dead.KnockUntil, Is.EqualTo(2.5f)); Assert.That(gone.KnockUntil, Is.EqualTo(2.5f));
        }
        [Test] public void PushErrorsDisposeTheLiveEnumeratorAndDoNotRollBackPriorKnockOrEraseMembership()
        {
            var cast = new Cast(); var actor = new Actor(1); cast.Enemies.Add(actor); cast.Apply(0, actor);
            var failure = new InvalidOperationException("knock"); cast.Knocking = target => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => cast.Push(.125f)), Is.SameAs(failure));
            Assert.That(actor.KnockUntil, Is.EqualTo(.5f)); Assert.That(cast.EnumerationEnds, Is.EqualTo(1));
            cast.Knocking = null; cast.Now = 1; cast.Push(.25f); Assert.That(actor.KnockUntil, Is.EqualTo(1.25f));
        }
        [Test] public void StructuralEnemyListChangesKeepNativeForeachInvalidationInsteadOfIteratingASnapshot()
        {
            var cast = new Cast(); var first = new Actor(1); var second = new Actor(2);
            cast.Enemies.Add(first); cast.Enemies.Add(second); cast.Apply(0, first); cast.Apply(0, second, new Hit { Push = true });
            cast.Knocking = actor => cast.Enemies.Remove(second);
            Assert.Throws<InvalidOperationException>(() => cast.Push(.125f));
            Assert.That(first.KnockUntil, Is.EqualTo(.5f)); Assert.That(second.KnockUntil, Is.Zero); Assert.That(cast.EnumerationEnds, Is.EqualTo(1));
        }
        [Test] public void EachCastOwnsIndependentShoveMembershipAndEndClaims()
        {
            var first = new Cast(); var second = new Cast(); var actor = new Actor(1);
            first.Enemies.Add(actor); second.Enemies.Add(actor); first.Apply(0, actor); second.Push(.125f);
            Assert.That(actor.KnockUntil, Is.Zero); first.End(); second.Marked = actor; second.End();
            Assert.That(actor.Hits, Is.EqualTo(3)); Assert.That(actor.Health, Is.EqualTo(41.5f));
        }
    }
}
