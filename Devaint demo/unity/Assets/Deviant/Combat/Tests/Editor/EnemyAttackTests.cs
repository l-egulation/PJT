using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EnemyAttackTests
    {
        sealed class Host : IEnemyAttackHost
        {
            public int Profile { get; set; }
            public float Now { get; set; }
            public readonly List<int> Hits = new List<int>();
            public int Projectiles;
            public Action<int> OnHit;
            public Action OnFire;
            public void MeleeHit(int index) { Hits.Add(index); OnHit?.Invoke(index); }
            public void FireProjectile() { Projectiles++; OnFire?.Invoke(); }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        [Test] public void SnapshotsUseTheCallersTimeAndRetainTheLastStartAcrossCancelAndReset()
        {
            var state = new EnemyAttackExecution(); var initial = state.Read();
            Assert.That(initial.Attacking, Is.False); Assert.That(initial.StartedAt, Is.EqualTo(-100));
            Assert.That(initial.NextAttackAt, Is.Zero); Assert.That(initial.Sequence, Is.Zero); Assert.That(initial.ProjectileHitIndex, Is.Zero);
            Assert.That(initial.Elapsed(0), Is.EqualTo(100)); Assert.That(initial.Telegraph(0, 0), Is.EqualTo(-1));
            state.Begin(4); var started = state.Read();
            Assert.That(started.Elapsed(3), Is.Zero); Assert.That(started.Elapsed(4.25f), Is.EqualTo(.25f));
            Assert.That(started.Telegraph(0, 3), Is.Zero); Assert.That(started.Telegraph(0, 10), Is.EqualTo(1));
            Assert.That(started.Sequence, Is.EqualTo(1)); Assert.That(started.NextAttackAt, Is.Zero);
            state.Cancel(5); var cancelled = state.Read();
            Assert.That(cancelled.Attacking, Is.False); Assert.That(cancelled.StartedAt, Is.EqualTo(4));
            Assert.That(Bits(cancelled.NextAttackAt), Is.EqualTo(Bits(5.3f))); Assert.That(cancelled.Elapsed(6), Is.EqualTo(2));
            state.Reset(8); var reset = state.Read();
            Assert.That(reset.Attacking, Is.False); Assert.That(reset.StartedAt, Is.EqualTo(4));
            Assert.That(reset.NextAttackAt, Is.EqualTo(8.5f)); Assert.That(reset.Sequence, Is.EqualTo(1));
            Assert.That(reset.Telegraph(2, 8), Is.EqualTo(-1));
            Assert.That(started.Attacking, Is.True); Assert.That(started.StartedAt, Is.EqualTo(4));
            Assert.That(initial.Elapsed(0), Is.EqualTo(100), "Later commands cannot edit old value snapshots.");
            state.Begin(0); Assert.That(state.Read().Sequence, Is.EqualTo(2)); Assert.That(state.Read().Elapsed(.25f), Is.EqualTo(.25f));
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void TelegraphReadsTheCurrentCompatibilityProfileWithoutAccumulatingATimer(int profile)
        {
            var state = new EnemyAttackExecution(); state.Begin(0); var view = state.Read();
            float first = profile == 2 ? .45f : .35f;
            Assert.That(Bits(view.Telegraph(profile, .175f)), Is.EqualTo(Bits(profile == 2 ? .3888889f : .5f)));
            Assert.That(view.Telegraph(profile, first), Is.EqualTo(1));
            Assert.That(view.Telegraph(profile, first + 1), Is.EqualTo(1));
            Assert.That(view.Elapsed(.175f), Is.EqualTo(.175f));
            state.Cancel(1); Assert.That(state.Read().Telegraph(profile, 1), Is.EqualTo(-1));
        }
        [Test] public void WindowStartIncludesTheLookaheadEndAndWindowEndExcludesTheCurrentTime()
        {
            var state = new EnemyAttackExecution(); var host = new Host(); state.Begin(0);
            state.Step(Before(.35f), false, host); Assert.That(host.Hits, Is.Empty);
            state.Step(.35f, false, host); Assert.That(host.Hits, Is.EqualTo(new[] { 0 }));
            host.Now = .35f; state.Step(.01f, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0, 0 }), "An active melee window is a native query on every overlapping tick.");
            host.Now = Before(.43f); state.Step(.01f, false, host); Assert.That(host.Hits.Count, Is.EqualTo(3));
            host.Now = .43f; state.Step(1, false, host); Assert.That(host.Hits.Count, Is.EqualTo(3));
        }
        [Test] public void BossWindowsKeepFloatAdditionOrderRatherThanDecimalThresholdSubstitution()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 2, Now = .45f }; state.Begin(0);
            state.Step(.01f, false, host); Assert.That(host.Hits, Is.EqualTo(new[] { 0 }));
            host.Now = .53f; state.Step(.01f, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0, 0 }), "The .53 literal is still inside the evaluated first active window.");
            host.Now = .54f; state.Step(.01f, false, host);
            Assert.That(host.Hits.Count, Is.EqualTo(2), "A later sample is outside the first active window.");
            host.Now = .73f; state.Step(.01f, false, host);
            Assert.That(host.Hits.Count, Is.EqualTo(2), "The second window is no longer active at this sample.");
            host.Now = .85f; state.Step(.01f, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0, 0, 2 }));
            host.Now = .94f; state.Step(.01f, false, host); Assert.That(host.Hits.Count, Is.EqualTo(3));
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void LongTicksPreserveProfileOrderAndDoNotCompleteAtTheLookaheadEnd(int profile)
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = profile }; state.Begin(0);
            state.Step(2, false, host);
            Assert.That(host.Hits, Is.EqualTo(profile == 1 ? Array.Empty<int>() : profile == 2 ? new[] { 0, 1, 2 } : new[] { 0 }));
            Assert.That(host.Projectiles, Is.EqualTo(profile == 1 ? 1 : 0));
            Assert.That(state.Read().Attacking, Is.True); Assert.That(state.Read().NextAttackAt, Is.Zero);
            host.Now = 2; state.Step(.02f, false, host);
            Assert.That(state.Read().Attacking, Is.False); Assert.That(state.Read().NextAttackAt, Is.EqualTo(profile == 2 ? 2.7f : 2.5f));
        }
        [Test] public void CurrentElapsedCompletionHasTheExactLegacyNormalAndBossBoundaries()
        {
            var normal = new EnemyAttackExecution(); var host = new Host { Now = Before(.95000005f) }; normal.Begin(0);
            normal.Step(.1f, false, host); Assert.That(normal.Read().Attacking, Is.True);
            host.Now = .95000005f; normal.Step(.01f, false, host);
            Assert.That(normal.Read().Attacking, Is.False); Assert.That(Bits(normal.Read().NextAttackAt), Is.EqualTo(Bits(1.45f)));
            var boss = new EnemyAttackExecution(); host.Profile = 2; host.Now = Before(1.45f); boss.Begin(0);
            boss.Step(.1f, false, host); Assert.That(boss.Read().Attacking, Is.True);
            host.Now = 1.45f; boss.Step(.01f, false, host);
            Assert.That(boss.Read().Attacking, Is.False); Assert.That(Bits(boss.Read().NextAttackAt), Is.EqualTo(Bits(2.15f)));
        }
        [Test] public void RangedEmissionIsOncePerBeginWhileLateMeleeTargetsAreClaimedIndividually()
        {
            var ranged = new EnemyAttackExecution(); var host = new Host { Profile = 1, Now = .35f }; ranged.Begin(0);
            ranged.Step(.01f, false, host); ranged.Step(.01f, false, host); host.Now = .4f; ranged.Step(.01f, false, host);
            Assert.That(host.Projectiles, Is.EqualTo(1)); Assert.That(ranged.Read().ProjectileHitIndex, Is.EqualTo(1));
            ranged.Cancel(.4f); ranged.Reset(.5f); Assert.That(ranged.Read().ProjectileHitIndex, Is.EqualTo(1));
            ranged.Begin(1); Assert.That(ranged.Read().ProjectileHitIndex, Is.Zero);
            host.Now = 1.35f; ranged.Step(.01f, false, host); Assert.That(host.Projectiles, Is.EqualTo(2));
            var melee = new EnemyAttackExecution(); host = new Host { Now = .35f }; melee.Begin(0);
            int target = 11; var accepted = new List<int>();
            host.OnHit = index => { if (melee.FirstHit(index, target)) accepted.Add(target); };
            melee.Step(.01f, false, host); melee.Step(.01f, false, host);
            target = 12; host.Now = .4f; melee.Step(.01f, false, host);
            target = 13; host.Now = .43f; melee.Step(.01f, false, host);
            Assert.That(accepted, Is.EqualTo(new[] { 11, 12 }));
        }
        [Test] public void LedgerRetainsExecutionHistoryAcrossResetButSeparatesHitsTargetsAndFreshBegins()
        {
            var state = new EnemyAttackExecution(); state.Begin(0);
            Assert.That(state.FirstHit(0, -1), Is.True); Assert.That(state.FirstHit(0, -1), Is.False);
            Assert.That(state.FirstHit(1, -1), Is.True); Assert.That(state.FirstHit(0, 1), Is.True);
            state.Cancel(.1f); state.Reset(.2f);
            Assert.That(state.FirstHit(0, -1), Is.False); Assert.That(state.FirstHit(1, -1), Is.False);
            Assert.That(state.Read().Sequence, Is.EqualTo(1));
            state.Begin(.7f); Assert.That(state.Read().Sequence, Is.EqualTo(2));
            Assert.That(state.FirstHit(0, -1), Is.True); Assert.That(state.FirstHit(1, -1), Is.True);
            var other = new EnemyAttackExecution(); other.Begin(0); Assert.That(other.FirstHit(0, -1), Is.True);
        }
        [Test] public void ControlledTicksSuppressQueriesAndCompletionWithoutCreatingADeferredHitOrClock()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 2, Now = .45f }; state.Begin(0);
            state.Step(.1f, true, host); Assert.That(host.Hits, Is.Empty); Assert.That(state.Read().Attacking, Is.True);
            host.Now = .65f; state.Step(.01f, false, host); Assert.That(host.Hits, Is.EqualTo(new[] { 1 }));
            host.Now = 2; state.Step(.01f, true, host); Assert.That(state.Read().Attacking, Is.True);
            state.Step(.01f, false, host); Assert.That(state.Read().Attacking, Is.False); Assert.That(state.Read().NextAttackAt, Is.EqualTo(2.7f));
            state.Cancel(3); state.Step(2, false, host); Assert.That(host.Hits, Is.EqualTo(new[] { 1 }));
            Assert.That(state.Read().NextAttackAt, Is.EqualTo(3.3f));
        }
        [Test] public void CancelAndResetCommandsKeepTheirOwnDeadlinesEvenWhenAlreadyInactive()
        {
            var state = new EnemyAttackExecution(); state.Cancel(2); Assert.That(state.Read().NextAttackAt, Is.EqualTo(2.3f));
            state.Cancel(3); Assert.That(state.Read().NextAttackAt, Is.EqualTo(3.3f));
            state.Reset(4); Assert.That(state.Read().NextAttackAt, Is.EqualTo(4.5f));
            state.Cancel(5); Assert.That(state.Read().NextAttackAt, Is.EqualTo(5.3f));
            Assert.That(state.Read().StartedAt, Is.EqualTo(-100)); Assert.That(state.Read().Sequence, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void CallbackCancelOrResetDoesNotStopTheAlreadyEnteredOrderedBossLoop(bool reset)
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 2 }; state.Begin(0);
            var claims = new List<bool>(); host.OnHit = index => {
                claims.Add(state.FirstHit(index, 7));
                if (index == 0) { if (reset) state.Reset(host.Now); else state.Cancel(host.Now); }
            };
            state.Step(1, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0, 1, 2 })); Assert.That(claims, Is.EqualTo(new[] { true, true, true }));
            Assert.That(state.Read().Attacking, Is.False); Assert.That(state.Read().NextAttackAt, Is.EqualTo(reset ? .5f : .3f));
            state.Step(1, false, host); Assert.That(host.Hits.Count, Is.EqualTo(3));
        }
        [Test] public void CallbackBeginChangesTheLiveLedgerSequenceButNotTheCachedTickTiming()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 2 }; state.Begin(0);
            Assert.That(state.FirstHit(1, 7), Is.True);
            var claims = new List<bool>(); host.OnHit = index => {
                if (index == 0) state.Begin(10);
                claims.Add(state.FirstHit(index, 7));
            };
            state.Step(1, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0, 1, 2 })); Assert.That(claims, Is.EqualTo(new[] { true, true, true }));
            Assert.That(state.Read().Sequence, Is.EqualTo(2)); Assert.That(state.Read().StartedAt, Is.EqualTo(10));
            Assert.That(state.Read().Attacking, Is.True); Assert.That(state.Read().NextAttackAt, Is.Zero);
        }
        [Test] public void CallbackProfileChangesUseCachedWindowsButLiveEffectKindAndRangedClaim()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 2 }; state.Begin(0);
            host.OnHit = index => host.Profile = 1;
            state.Step(1, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0 })); Assert.That(host.Projectiles, Is.EqualTo(1));
            Assert.That(state.Read().ProjectileHitIndex, Is.EqualTo(1));
            host.Now = 1.5f; state.Step(.01f, false, host);
            Assert.That(state.Read().Attacking, Is.False); Assert.That(state.Read().NextAttackAt, Is.EqualTo(2));
        }
        [Test] public void CompletionReadsTheLiveClockAndProfileRatherThanCachingTheWindowSample()
        {
            var state = new EnemyAttackExecution(); state.Begin(0); var changing = new CompletionHost();
            state.Step(1, false, changing);
            Assert.That(state.Read().Attacking, Is.False); Assert.That(state.Read().NextAttackAt, Is.EqualTo(7.7f));
        }
        sealed class CompletionHost : IEnemyAttackHost
        {
            int clockReads, profileReads;
            public float Now => clockReads++ == 0 ? 1 : 7;
            public int Profile => profileReads++ < 2 ? 0 : 2;
            public void MeleeHit(int hitIndex) { Assert.Fail("Every native hit window precedes completion."); }
            public void FireProjectile() { Assert.Fail("Every native hit window precedes completion."); }
        }
        [Test] public void SynchronousMeleeReentryUsesTheSameLedgerWithoutAnExtraProgressClock()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Now = .35f }; state.Begin(0);
            bool nested = false; int accepted = 0;
            host.OnHit = index => {
                if (state.FirstHit(index, 7)) accepted++;
                if (!nested) { nested = true; state.Step(.01f, false, host); }
            };
            state.Step(.01f, false, host);
            Assert.That(host.Hits, Is.EqualTo(new[] { 0, 0 })); Assert.That(accepted, Is.EqualTo(1));
            Assert.That(state.Read().Sequence, Is.EqualTo(1)); Assert.That(state.Read().StartedAt, Is.Zero);
        }
        [Test] public void RangedFlagIsWrittenAfterTheCallbackIncludingNestedStepOrReset()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 1, Now = .35f }; state.Begin(0);
            bool nested = false; var observed = new List<int>(); host.OnFire = () => {
                observed.Add(state.Read().ProjectileHitIndex);
                if (!nested) { nested = true; state.Step(.01f, false, host); state.Reset(host.Now); }
            };
            state.Step(.01f, false, host);
            Assert.That(host.Projectiles, Is.EqualTo(2)); Assert.That(observed, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(state.Read().Attacking, Is.False); Assert.That(state.Read().ProjectileHitIndex, Is.EqualTo(1));
            Assert.That(state.Read().NextAttackAt, Is.EqualTo(.85f));
        }
        [Test] public void NativeFailurePropagatesWithoutInventingCompletionOrConsumingTheRangedFlag()
        {
            var state = new EnemyAttackExecution(); var host = new Host { Profile = 1, Now = .35f }; state.Begin(0);
            host.OnFire = () => throw new InvalidOperationException("fixture native emission");
            Assert.Throws<InvalidOperationException>(() => state.Step(.01f, false, host));
            Assert.That(state.Read().Attacking, Is.True); Assert.That(state.Read().ProjectileHitIndex, Is.Zero); Assert.That(state.Read().NextAttackAt, Is.Zero);
            host.OnFire = null; state.Step(.01f, false, host);
            Assert.That(host.Projectiles, Is.EqualTo(2)); Assert.That(state.Read().ProjectileHitIndex, Is.EqualTo(1));
        }
    }
}
