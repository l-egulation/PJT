using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorDamageExecutionTests
    {
        readonly struct Observation
        {
            public readonly ActorVitalsSnapshot Vitals;
            public readonly ActorStatusSnapshot Statuses;
            public readonly float Damage;
            public readonly bool Player;
            public Observation(State state, float damage = 0)
            { Vitals = state.Vitals.Read(); Statuses = state.Statuses.Read(); Damage = damage; Player = state.Player; }
        }
        sealed class State
        {
            public readonly IActorVitals Vitals = new ActorVitalsState(100);
            // Existing callback-order fixtures intentionally have no player
            // source; ordinary player-basic push has dedicated cases below.
            public readonly IActorStatuses Statuses = new ActorStatusState();
            public float Now;
            public bool Player, Boss, Decoy, SourcePlayer, Invulnerable, Forced, Parried;
            public float BasicStaggerSeconds = .3f, BasicPushDistance = .25f, BasicPushSeconds = .1f, ActionPushDistance, ActionPushSeconds;
            public float PushLimit = float.PositiveInfinity;
            public Action Parrying, Cancelled, Recorded, Died, Visualized;
            public Func<bool> PreventDeath;
            public readonly List<string> Events = new List<string>();
            public readonly List<Observation> Callbacks = new List<Observation>(), Records = new List<Observation>();
            public readonly List<ActorDamageResult> Accepted = new List<ActorDamageResult>();
        }
        struct Host : IActorDamageHost, IActorDeathInterceptor
        {
            public readonly State State;
            public int ClockReads, InvulnerabilityReads, ParryCalls, Cancellations, RecordCalls, PlayerDeaths, DeathVisuals;
            public Host(State state)
            {
                State = state;
                ClockReads = InvulnerabilityReads = ParryCalls = Cancellations = RecordCalls = PlayerDeaths = DeathVisuals = 0;
            }
            public IActorVitals Vitals => State.Vitals;
            public bool TryPreventDeath() => State.PreventDeath?.Invoke() ?? false;
            public IActorStatuses Statuses => State.Statuses;
            public float Now { get { ClockReads++; return State.Now; } }
            public bool IsPlayer => State.Player;
            public bool IsBoss => State.Boss;
            public bool IsDecoy => State.Decoy;
            public bool SourceIsPlayer => State.SourcePlayer;
            public float BasicStaggerSeconds => State.BasicStaggerSeconds;
            public float BasicPushDistance => State.BasicPushDistance;
            public float BasicPushSeconds => State.BasicPushSeconds;
            public float ActionPushDistance => State.ActionPushDistance;
            public float ActionPushSeconds => State.ActionPushSeconds;
            public float LimitAutomaticPush(float distance) => Math.Min(distance, State.PushLimit);
            public bool PlayerInvulnerable { get { InvulnerabilityReads++; return State.Invulnerable; } }
            public bool IsForcedMoving => State.Forced;
            public bool TryParryMelee() { ParryCalls++; State.Parrying?.Invoke(); return State.Parried; }
            public void AcceptedHit(float requested, ActorVitalsSnapshot before, ActorDamageResult applied, float now)
            { State.Accepted.Add(applied); }
            public void CancelAttack()
            {
                Cancellations++; State.Events.Add("cancel"); State.Callbacks.Add(new Observation(State)); State.Cancelled?.Invoke();
            }
            public void RecordDamage(float damage)
            {
                RecordCalls++; State.Events.Add("record"); State.Records.Add(new Observation(State, damage)); State.Recorded?.Invoke();
            }
            public void DiePlayer() { PlayerDeaths++; State.Events.Add("die"); State.Died?.Invoke(); }
            public void ApplyDeathVisual() { DeathVisuals++; State.Events.Add("visual"); State.Visualized?.Invoke(); }
        }
        static bool Receive(ref Host host, float damage = 0, bool basic = false, HitData mutable = null,
            SkillHitDefinition immutable = null, bool bypass = false, bool projectile = false)
            => ActorDamageExecution.Receive(damage, -1, basic, mutable, immutable, bypass, projectile, ref host);

        [Test] public void RevivalRestoresHealthBeforeDeathCallbacksAndOnlyPreventsOneDeath()
        {
            var state = new State { Player = true }; bool available = true;
            state.PreventDeath = () => { if (!available) return false; available = false; state.Vitals.WriteHealth(50); return true; };
            var host = new Host(state);
            Assert.That(Receive(ref host, 999), Is.True);
            Assert.That(state.Vitals.Read().Health, Is.EqualTo(50));
            Assert.That(host.PlayerDeaths, Is.Zero); Assert.That(host.DeathVisuals, Is.Zero);
            Assert.That(Receive(ref host, 999), Is.True);
            Assert.That(host.PlayerDeaths, Is.EqualTo(1)); Assert.That(host.DeathVisuals, Is.EqualTo(1));
        }

        [Test] public void ZeroHitStaggerPreservesDamageAndPushWithoutCancellingButStunAndDeathStillCancel()
        {
            var state=new State {Now=2,SourcePlayer=true,BasicStaggerSeconds=0};
            var host=new Host(state);
            Assert.That(Receive(ref host,damage:10,basic:true),Is.True);
            Assert.That(state.Vitals.Read().Health,Is.EqualTo(90));
            Assert.That(state.Statuses.Read().StaggerUntil,Is.LessThanOrEqualTo(2));
            Assert.That(state.Statuses.Read().KnockUntil,Is.GreaterThan(2));
            Assert.That(host.Cancellations,Is.Zero);
            Assert.That(Receive(ref host,damage:1,mutable:new HitData {stun=.5f}),Is.True);
            Assert.That(host.Cancellations,Is.EqualTo(1));
            Assert.That(Receive(ref host,damage:999,basic:true),Is.True);
            Assert.That(host.Cancellations,Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void RangeLimitClipsAutomaticPushAndZeroReplacesAlreadyRunningMotionWithoutLosingStagger(bool tuned)
        {
            var state = new State { SourcePlayer = true, PushLimit = .05f,
                ActionPushDistance = tuned ? 1.75f : 0, ActionPushSeconds = .16f };
            var host = new Host(state);
            Assert.That(Receive(ref host, 10, basic: !tuned), Is.True);
            float seconds = tuned ? .16f : .1f;
            Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(-.05f / seconds).Within(.000001f));
            state.Now = .02f; state.PushLimit = 0;
            Assert.That(Receive(ref host, 10, basic: !tuned), Is.True);
            Assert.That(state.Statuses.Read().KnockSpeed, Is.Zero);
            Assert.That(state.Statuses.KnockSpeedAt(state.Now, .01f), Is.Zero);
            Assert.That(state.Statuses.Read().StaggerUntil, Is.EqualTo(.32f).Within(.000001f));
            Assert.That(state.Vitals.Read().Health, Is.EqualTo(80));
            Assert.That(host.Cancellations, Is.EqualTo(2));
        }
        [Test] public void ExplicitSkillPushBypassesTheAutomaticRangeLimit()
        {
            var state = new State { SourcePlayer = true, PushLimit = 0, ActionPushDistance = 1.75f, ActionPushSeconds = .16f };
            var host = new Host(state);
            Assert.That(Receive(ref host, 10, basic: true, mutable: new HitData { push = 2, pushSeconds = .2f }), Is.True);
            Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(-10));
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(.2f));
        }

        [Test] public void EnemyReactionDefinitionOwnsValidatedValuesWithoutRetainingItsMutableSource()
        {
            var source = new EnemyHitReactionData { basicStaggerSeconds = .3f, basicPushDistance = .25f, basicPushSeconds = .1f, basicPushOverlap = .25f };
            Assert.That(EnemyHitReactionDefinition.TryCreate(source, out var reaction, out var error), Is.True, error);
            source.basicStaggerSeconds = source.basicPushDistance = source.basicPushSeconds = source.basicPushOverlap = 9;
            Assert.That(reaction.BasicPushOverlap, Is.EqualTo(.25f));
            Assert.That(reaction.BasicStaggerSeconds, Is.EqualTo(.3f));
            Assert.That(reaction.BasicPushDistance, Is.EqualTo(.25f));
            Assert.That(reaction.BasicPushSeconds, Is.EqualTo(.1f));
            foreach (var property in typeof(EnemyHitReactionDefinition).GetProperties()) Assert.That(property.GetSetMethod(true), Is.Null);
        }
        [TestCase("missing")] [TestCase("stagger-zero")] [TestCase("stagger-nan")] [TestCase("stagger-infinite")]
        [TestCase("distance-negative")] [TestCase("distance-nan")] [TestCase("distance-infinite")]
        [TestCase("seconds-zero")] [TestCase("seconds-negative")] [TestCase("seconds-nan")] [TestCase("seconds-infinite")] [TestCase("speed-overflow")]
        [TestCase("overlap-negative")] [TestCase("overlap-nan")] [TestCase("overlap-infinite")]
        public void InvalidEnemyReactionSourceDoesNotPublishADefinition(string scenario)
        {
            var source = new EnemyHitReactionData { basicStaggerSeconds = .3f, basicPushDistance = .25f, basicPushSeconds = .1f };
            switch (scenario)
            {
                case "missing": source = null; break;
                case "stagger-zero": source.basicStaggerSeconds = 0; break;
                case "stagger-nan": source.basicStaggerSeconds = float.NaN; break;
                case "stagger-infinite": source.basicStaggerSeconds = float.PositiveInfinity; break;
                case "distance-negative": source.basicPushDistance = -1; break;
                case "distance-nan": source.basicPushDistance = float.NaN; break;
                case "distance-infinite": source.basicPushDistance = float.PositiveInfinity; break;
                case "seconds-zero": source.basicPushSeconds = 0; break;
                case "seconds-negative": source.basicPushSeconds = -1; break;
                case "seconds-nan": source.basicPushSeconds = float.NaN; break;
                case "seconds-infinite": source.basicPushSeconds = float.PositiveInfinity; break;
                case "speed-overflow": source.basicPushDistance = float.MaxValue; source.basicPushSeconds = float.Epsilon; break;
                case "overlap-negative": source.basicPushOverlap = -.01f; break;
                case "overlap-nan": source.basicPushOverlap = float.NaN; break;
                case "overlap-infinite": source.basicPushOverlap = float.PositiveInfinity; break;
            }
            Assert.That(EnemyHitReactionDefinition.TryCreate(source, out var reaction, out var error), Is.False);
            Assert.That(reaction, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test] public void DeadRejectsBeforeReadingTheClockOrDefensesEvenWithBypass()
        {
            var state = new State { Player = true, Invulnerable = true, Parried = true }; state.Vitals.WriteHealth(0);
            var host = new Host(state);
            Assert.That(Receive(ref host, 1, basic: true, bypass: true), Is.False);
            Assert.That(host.ClockReads + host.InvulnerabilityReads + host.ParryCalls, Is.Zero);
            Assert.That(state.Vitals.Read().ReceivedHits, Is.Zero); Assert.That(state.Events, Is.Empty);
            Assert.That(state.Accepted, Is.Empty);
            Assert.That(state.Statuses.Read().InputLockUntil, Is.Zero);
        }
        [TestCase(true, false, false, true, true, false, 0, 1)]
        [TestCase(true, false, false, false, true, false, 1, 1)]
        [TestCase(true, false, true, false, true, true, 0, 1)]
        [TestCase(true, false, true, true, true, false, 0, 1)]
        [TestCase(true, true, false, true, true, true, 0, 0)]
        [TestCase(false, false, false, true, true, true, 0, 0)]
        public void EligibilityKeepsInvulnerabilityBeforeMeleeParryAndTheirExistingBypassAndProjectileGates(
            bool player, bool bypass, bool projectile, bool invulnerable, bool parried, bool accepted, int parryCalls, int invulnerabilityReads)
        {
            var state = new State { Player = player, Invulnerable = invulnerable, Parried = parried };
            var host = new Host(state);
            Assert.That(Receive(ref host, 1, bypass: bypass, projectile: projectile), Is.EqualTo(accepted));
            Assert.That(host.ClockReads, Is.EqualTo(1)); Assert.That(host.ParryCalls, Is.EqualTo(parryCalls));
            Assert.That(host.InvulnerabilityReads, Is.EqualTo(invulnerabilityReads));
            Assert.That(state.Vitals.Read().ReceivedHits, Is.EqualTo(accepted ? 1 : 0));
            Assert.That(state.Accepted.Count, Is.EqualTo(accepted ? 1 : 0));
            Assert.That(host.RecordCalls, Is.EqualTo(accepted ? 1 : 0));
            Assert.That(state.Statuses.Read().InputLockUntil, Is.EqualTo(accepted && player ? .12f : 0));
        }
        [Test] public void ParryCallbackCanMakeTheVitalsOwnerRejectWithoutCreatingHitOrDeathEffects()
        {
            var state = new State { Player = true }; state.Parrying = () => state.Vitals.WriteHealth(0);
            var host = new Host(state);
            Assert.That(Receive(ref host, 1, basic: true, mutable: new HitData { root = 1 }), Is.False);
            Assert.That(host.ParryCalls, Is.EqualTo(1)); Assert.That(state.Vitals.Read().ReceivedHits, Is.Zero);
            Assert.That(state.Events, Is.Empty); Assert.That(state.Statuses.Read().InputLockUntil, Is.Zero);
        }
        [Test] public void PlayerHitReadsCurrentForcedMovementAfterTheParryCallbackAndIgnoresEnemyControls()
        {
            var state = new State { Player = true }; state.Parrying = () => state.Forced = true;
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true, mutable: new HitData { root = 1, stun = 1, push = 1, pushSeconds = 1 }), Is.True);
            Assert.That(state.Statuses.Read().InputLockUntil, Is.EqualTo(.12f)); Assert.That(state.Statuses.Read().KnockUntil, Is.Zero);
            Assert.That(state.Statuses.Read().RootUntil + state.Statuses.Read().StunUntil + state.Statuses.Read().StaggerUntil, Is.Zero);
            Assert.That(host.Cancellations, Is.Zero); Assert.That(state.Vitals.Read().ReceivedHits, Is.EqualTo(1));
        }
        [Test] public void BasicAndStunCallbacksSeeCommittedPhasesAndChangeOnlyFollowingMutableControlReads()
        {
            var state = new State(); var control = new HitData { root = .1f };
            state.Cancelled = () =>
            {
                if (state.Callbacks.Count == 1) { control.root = .75f; control.stun = .5f; control.push = 2; control.pushSeconds = .25f; }
                else { control.root = 8; control.stun = 9; control.push = 1.5f; control.pushSeconds = .5f; state.Vitals.WriteHealth(23); }
            };
            var host = new Host(state);
            Assert.That(Receive(ref host, 20, basic: true, mutable: control), Is.True);
            Assert.That(host.Cancellations, Is.EqualTo(2)); Assert.That(host.RecordCalls, Is.EqualTo(1));
            Assert.That(state.Callbacks[0].Vitals.Health, Is.EqualTo(80)); Assert.That(state.Callbacks[0].Vitals.ReceivedHits, Is.EqualTo(1));
            Assert.That(state.Callbacks[0].Statuses.StaggerUntil, Is.EqualTo(.3f)); Assert.That(state.Callbacks[0].Statuses.RootUntil, Is.Zero);
            Assert.That(state.Callbacks[1].Statuses.RootUntil, Is.EqualTo(.75f)); Assert.That(state.Callbacks[1].Statuses.StunUntil, Is.EqualTo(.5f));
            Assert.That(state.Callbacks[1].Statuses.KnockUntil, Is.Zero);
            var recorded = state.Records[0]; Assert.That(recorded.Vitals.Health, Is.EqualTo(23)); Assert.That(recorded.Damage, Is.EqualTo(20));
            Assert.That(recorded.Statuses.RootUntil, Is.EqualTo(.75f)); Assert.That(recorded.Statuses.StunUntil, Is.EqualTo(.5f));
            Assert.That(recorded.Statuses.KnockUntil, Is.EqualTo(.5f)); Assert.That(recorded.Statuses.KnockSpeed, Is.EqualTo(-3));
            Assert.That(state.Events, Is.EqualTo(new[] { "cancel", "cancel", "record" }));
        }
        [Test] public void MutableControlCanBeDisabledByBasicCancellationWithoutApplyingStunOrPush()
        {
            var state = new State(); var control = new HitData { root = 1, stun = 1, push = 1, pushSeconds = 1 };
            state.Cancelled = () => { control.root = control.stun = control.push = 0; };
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true, mutable: control), Is.True);
            Assert.That(host.Cancellations, Is.EqualTo(1)); Assert.That(state.Statuses.Read().RootUntil, Is.Zero);
            Assert.That(state.Statuses.Read().StunUntil + state.Statuses.Read().KnockUntil, Is.Zero);
        }
        [Test] public void ImmutableControlKeepsItsDefinitionWhenCallbacksEditTheOriginalDto()
        {
            var state = new State(); var source = new HitData { root = .625f, stun = .375f, push = 1, pushSeconds = .25f };
            var definition = new SkillHitDefinition(source);
            state.Cancelled = () => { source.root = source.stun = source.push = source.pushSeconds = 0; };
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true, immutable: definition), Is.True);
            Assert.That(host.Cancellations, Is.EqualTo(2)); Assert.That(state.Statuses.Read().RootUntil, Is.EqualTo(.625f));
            Assert.That(state.Statuses.Read().StunUntil, Is.EqualTo(.375f)); Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(-4));
        }
        [TestCase(true, true, false, false, .1f, -2.5f)]
        [TestCase(true, false, false, false, 0, 0)]
        [TestCase(false, true, false, false, 0, 0)]
        [TestCase(true, true, true, false, 0, 0)]
        [TestCase(true, true, false, true, 0, 0)]
        public void OrdinaryBasicPushRequiresAPlayerSourceAndExcludesBossesDecoysAndSpecials(
            bool basic, bool sourcePlayer, bool boss, bool decoy, float until, float speed)
        {
            var state = new State { SourcePlayer = sourcePlayer, Boss = boss, Decoy = decoy };
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: basic), Is.True);
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(until));
            Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(speed));
            Assert.That(state.Statuses.Read().StaggerUntil, Is.EqualTo(basic && !boss && !decoy ? .3f : 0));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExplicitPushReplacesOrdinaryPushForMutableAndImmutableControls(bool immutable)
        {
            var state = new State { SourcePlayer = true }; var source = new HitData { push = 1.5f, pushSeconds = .5f };
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true, mutable: immutable ? null : source,
                immutable: immutable ? new SkillHitDefinition(source) : null), Is.True);
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(.5f));
            Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(-3));
            Assert.That(state.Callbacks[0].Statuses.KnockUntil, Is.Zero, "Basic cancellation precedes either push phase.");
        }
        [Test] public void BasicCancellationReadsLiveSourceAndControlBeforeStartingTheConfiguredOrdinaryPush()
        {
            var state = new State { Now = 2, BasicStaggerSeconds = .6f, BasicPushDistance = .4f, BasicPushSeconds = .2f };
            state.Statuses.WriteStaggerUntil(9);
            var control = new HitData { push = 3, pushSeconds = 1 };
            state.Cancelled = () => { state.SourcePlayer = true; control.push = 0; };
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true, mutable: control), Is.True);
            Assert.That(state.Statuses.Read().StaggerUntil, Is.EqualTo(9), "A basic hit never shortens an existing longer stagger.");
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(2.2f));
            Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(-2));
            Assert.That(state.Callbacks[0].Statuses.KnockUntil, Is.Zero);
        }
        [Test] public void LaterStunCancellationDoesNotRetryTheEarlierOrdinaryPushDecision()
        {
            var state = new State { SourcePlayer = true }; var control = new HitData { stun = .2f, push = 1, pushSeconds = .5f };
            state.Cancelled = () => { if (state.Callbacks.Count == 2) control.push = 0; };
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true, mutable: control), Is.True);
            Assert.That(host.Cancellations, Is.EqualTo(2)); Assert.That(state.Statuses.Read().KnockUntil, Is.Zero);
        }

        [Test] public void StunFailureKeepsTheEarlierAcceptedVitalsStaggerAndOrdinaryPushWithoutEnteringRecord()
        {
            var state = new State { SourcePlayer = true }; var failure = new InvalidOperationException("stun cancellation");
            state.Cancelled = () => { if (state.Callbacks.Count == 2) throw failure; };
            var host = new Host(state);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Receive(ref host, 2, basic: true,
                mutable: new HitData { root = .5f, stun = .25f })), Is.SameAs(failure));
            Assert.That(state.Accepted.Count, Is.EqualTo(1)); Assert.That(state.Vitals.Read().Health, Is.EqualTo(98));
            Assert.That(state.Statuses.Read().StaggerUntil, Is.EqualTo(.3f)); Assert.That(state.Statuses.Read().RootUntil, Is.EqualTo(.5f));
            Assert.That(state.Statuses.Read().StunUntil, Is.EqualTo(.25f)); Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(.1f));
            Assert.That(state.Statuses.Read().KnockSpeed, Is.EqualTo(-2.5f)); Assert.That(host.RecordCalls, Is.Zero);
        }
        [Test] public void BasicCancellationKeepsItsEnteredOrdinaryTargetBranchWhileLaterExplicitPushUsesCurrentImmunity()
        {
            var state = new State { SourcePlayer = true };
            state.Cancelled = () => state.Boss = true;
            var host = new Host(state);
            Assert.That(Receive(ref host, basic: true), Is.True);
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(.1f), "The already-entered ordinary reaction is not reclassified after cancellation.");
            Assert.That(state.Statuses.Read().StaggerUntil, Is.EqualTo(.3f));
        }

        [TestCase(true, false, false, false)] [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)] [TestCase(false, false, false, true)]
        public void StunCancellationCanRemoveOrAddBossAndDecoyPushImmunity(bool initialBoss, bool initialDecoy, bool boss, bool decoy)
        {
            var state = new State { Boss = initialBoss, Decoy = initialDecoy };
            state.Cancelled = () => { state.Boss = boss; state.Decoy = decoy; };
            var host = new Host(state);
            Assert.That(Receive(ref host, mutable: new HitData { root = .5f, stun = .25f, push = 1, pushSeconds = .5f }), Is.True);
            Assert.That(host.Cancellations, Is.EqualTo(1)); Assert.That(state.Statuses.Read().RootUntil, Is.EqualTo(.5f));
            Assert.That(state.Statuses.Read().StunUntil, Is.EqualTo(.25f)); Assert.That(state.Statuses.Read().StaggerUntil, Is.Zero);
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(boss || decoy ? 0 : .5f));
        }
        [Test] public void EnteredEnemyBranchAndCapturedClockSurvivePlayerAndClockEditsDuringCancellation()
        {
            var state = new State { Now = 2 };
            state.Cancelled = () => { state.Player = true; state.Now = 9; };
            var host = new Host(state);
            Assert.That(Receive(ref host, 1, basic: true, mutable: new HitData { root = .5f, stun = .25f, push = 1, pushSeconds = .5f }), Is.True);
            Assert.That(host.ClockReads, Is.EqualTo(1)); Assert.That(host.Cancellations, Is.EqualTo(2));
            Assert.That(state.Statuses.Read().RootUntil, Is.EqualTo(2.5f)); Assert.That(state.Statuses.Read().StunUntil, Is.EqualTo(2.25f));
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(2.5f)); Assert.That(state.Statuses.Read().InputLockUntil, Is.Zero);
            Assert.That(state.Records[0].Player, Is.True);
        }
        [TestCase(0)] [TestCase(10)]
        public void ZeroOrFullyAbsorbedDamageIsAcceptedAndStillRunsHitControlPhases(float damage)
        {
            var state = new State(); state.Vitals.WriteShield(20);
            var host = new Host(state);
            Assert.That(Receive(ref host, damage, basic: true, mutable: new HitData { root = .5f, stun = .25f }), Is.True);
            Assert.That(state.Vitals.Read().Health, Is.EqualTo(100)); Assert.That(state.Vitals.Read().ReceivedHits, Is.EqualTo(1));
            Assert.That(host.Cancellations, Is.EqualTo(2)); Assert.That(state.Records[0].Damage, Is.Zero);
            Assert.That(state.Records[0].Statuses.RootUntil, Is.EqualTo(.5f));
        }
        [Test] public void NestedHitInBasicCancellationCompletesBeforeOuterControlsAndBothRecordsSeeCurrentHealth()
        {
            var state = new State(); bool nestedAccepted = false;
            state.Cancelled = () => { var nestedHost = new Host(state); nestedAccepted = Receive(ref nestedHost, 7); };
            var host = new Host(state);
            Assert.That(Receive(ref host, 10, basic: true, mutable: new HitData { root = .5f }), Is.True);
            Assert.That(nestedAccepted, Is.True); Assert.That(state.Vitals.Read().ReceivedHits, Is.EqualTo(2));
            Assert.That(state.Records.Count, Is.EqualTo(2)); Assert.That(state.Records[0].Vitals.Health, Is.EqualTo(83));
            Assert.That(state.Records[1].Vitals.Health, Is.EqualTo(83)); Assert.That(state.Records[0].Damage, Is.EqualTo(7));
            Assert.That(state.Records[1].Damage, Is.EqualTo(10)); Assert.That(state.Records[0].Statuses.RootUntil, Is.Zero);
            Assert.That(state.Records[1].Statuses.RootUntil, Is.EqualTo(.5f));
        }
        [TestCase(false, true)] [TestCase(true, false)]
        public void EnteredDeathStillAppliesVisualAfterRevivalAndReadsPlayerKindAfterDeathCancellation(bool initiallyPlayer, bool finallyPlayer)
        {
            var state = new State { Player = initiallyPlayer }; state.Vitals.WriteHealth(1);
            state.Cancelled = () => { state.Vitals.WriteHealth(25); state.Player = finallyPlayer; };
            var host = new Host(state);
            Assert.That(Receive(ref host, 2), Is.True);
            Assert.That(state.Records[0].Vitals.Health, Is.Zero); Assert.That(state.Vitals.Read().Health, Is.EqualTo(25));
            Assert.That(host.Cancellations, Is.EqualTo(1)); Assert.That(host.PlayerDeaths, Is.EqualTo(finallyPlayer ? 1 : 0));
            Assert.That(host.DeathVisuals, Is.EqualTo(1));
            Assert.That(state.Events, Is.EqualTo(finallyPlayer ? new[] { "record", "cancel", "die", "visual" } : new[] { "record", "cancel", "visual" }));
        }
        [Test] public void RecordCallbackCanCauseDeathAndPlayerDieCallbackCanChangeFactsBeforeTheVisualWithoutRechecks()
        {
            var state = new State { Player = true };
            state.Recorded = () => state.Vitals.WriteHealth(0);
            state.Died = () => { state.Vitals.WriteHealth(37); state.Player = false; };
            var host = new Host(state);
            Assert.That(Receive(ref host), Is.True);
            Assert.That(state.Records[0].Vitals.Health, Is.EqualTo(100)); Assert.That(state.Callbacks[0].Vitals.Health, Is.Zero);
            Assert.That(state.Vitals.Read().Health, Is.EqualTo(37)); Assert.That(host.DeathVisuals, Is.EqualTo(1));
            Assert.That(state.Events, Is.EqualTo(new[] { "record", "cancel", "die", "visual" }));
        }
        [Test] public void RevivalInTheRecordCallbackSkipsDeathEvenWhenApplyDamageWasLethal()
        {
            var state = new State { Player = true };
            state.Recorded = () => state.Vitals.WriteHealth(37);
            var host = new Host(state);
            Assert.That(Receive(ref host, 200), Is.True);
            Assert.That(state.Records[0].Vitals.Health, Is.Zero); Assert.That(state.Records[0].Damage, Is.EqualTo(200));
            Assert.That(host.Cancellations + host.PlayerDeaths + host.DeathVisuals, Is.Zero);
            Assert.That(state.Statuses.Read().InputLockUntil, Is.EqualTo(.12f));
        }
        [TestCase("basic")] [TestCase("stun")] [TestCase("record")] [TestCase("death")] [TestCase("die")] [TestCase("visual")]
        public void CallbackFailuresPropagateTheSameExceptionAndLeaveOnlyAlreadyEnteredPhases(string phase)
        {
            var state = new State { Player = phase == "die" || phase == "visual" };
            var failure = new InvalidOperationException(phase); Action fail = () => throw failure;
            bool death = phase == "death" || phase == "die" || phase == "visual";
            if (death) state.Vitals.WriteHealth(1);
            if (phase == "basic" || phase == "stun" || phase == "death") state.Cancelled = fail;
            if (phase == "record") state.Recorded = fail;
            if (phase == "die") state.Died = fail;
            if (phase == "visual") state.Visualized = fail;
            var control = death ? null : new HitData { root = .5f, stun = .25f, push = 1, pushSeconds = .5f };
            var host = new Host(state);
            var thrown = Assert.Throws<InvalidOperationException>(() => Receive(ref host, 2, basic: phase == "basic", mutable: control));
            Assert.That(thrown, Is.SameAs(failure)); Assert.That(state.Vitals.Read().ReceivedHits, Is.EqualTo(1));
            Assert.That(state.Accepted.Count, Is.EqualTo(1), "The committed fact precedes the throwing existing callback.");
            Assert.That(state.Vitals.Read().Health, Is.EqualTo(death ? 0 : 98));
            Assert.That(state.Statuses.Read().StaggerUntil, Is.EqualTo(phase == "basic" ? .3f : 0));
            Assert.That(state.Statuses.Read().RootUntil, Is.EqualTo(phase == "stun" || phase == "record" ? .5f : 0));
            Assert.That(state.Statuses.Read().StunUntil, Is.EqualTo(phase == "stun" || phase == "record" ? .25f : 0));
            Assert.That(state.Statuses.Read().KnockUntil, Is.EqualTo(phase == "record" ? .5f : phase == "die" || phase == "visual" ? .1f : 0));
            Assert.That(host.RecordCalls, Is.EqualTo(phase == "basic" || phase == "stun" ? 0 : 1));
            Assert.That(host.PlayerDeaths, Is.EqualTo(phase == "die" || phase == "visual" ? 1 : 0));
            Assert.That(host.DeathVisuals, Is.EqualTo(phase == "visual" ? 1 : 0));
        }
    }
}
