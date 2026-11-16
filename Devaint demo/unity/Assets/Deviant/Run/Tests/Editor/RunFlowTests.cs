using Deviant.Run;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class RunFlowTests
    {
        static RunFlow Flow(params int[] seeds)
        {
            int next = 0;
            return new RunFlow(() => seeds[next++]);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DebugChapterStartsFreshWithoutSettlingOrMarkingBoss(int chapter)
        {
            var flow = Flow(11, 22); int settlements = 0, starts = 0;
            flow.Ended += _ => settlements++;
            flow.Started += _ => starts++;
            var old = flow.StartNewRun(0); old.RecordKill(); old.MarkBossDefeated();
            var run = flow.StartDebugChapter(10, chapter);
            Assert.AreNotSame(old, run);
            Assert.AreEqual(chapter, run.Chapter);
            Assert.AreEqual(22, run.Seed);
            Assert.AreEqual(0, run.Kills);
            Assert.IsFalse(run.BossDefeated);
            Assert.IsFalse(run.Ended);
            Assert.IsNull(run.Plan);
            Assert.AreEqual(0, settlements);
            Assert.AreEqual(2, starts);
            Assert.IsFalse(flow.TryClearChapter(20));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => flow.StartDebugChapter(30, 3));
            Assert.AreSame(run, flow.Current);
        }

        [Test] public void DiscardDropsTheRunWithoutSettlementAndAllowsAFreshStart()
        {
            var flow = Flow(11, 22);
            flow.StartNewRun(0).RecordKill();
            int settlements = 0;
            flow.Ended += _ => settlements++;
            flow.DiscardCurrentRun();
            Assert.IsNull(flow.Current);
            Assert.IsFalse(flow.TryDie(10));
            Assert.AreEqual(0, settlements);
            Assert.AreEqual(22, flow.StartNewRun(20).Seed);
            Assert.AreEqual(0, flow.Current.Kills);
        }

        [Test]
        public void EachNewRunTakesAFreshSeedUnlessOneIsGiven()
        {
            var flow = Flow(11, 22);
            Assert.AreEqual(11, flow.StartNewRun(0).Seed);
            Assert.AreEqual(22, flow.StartNewRun(0).Seed);
            Assert.AreEqual(7, flow.StartNewRun(0, 7).Seed);
        }

        [Test]
        public void PigmentAddsUpAcrossChaptersUntilTheRunEnds()
        {
            var flow = Flow(5);
            var run = flow.StartNewRun(0);
            run.RecordPigment(3); run.RecordPigment(0); run.RecordPigment(-2);
            run.MarkBossDefeated();
            Assert.IsTrue(flow.TryAdvanceChapter(50));
            run.RecordPigment(4);
            Assert.AreEqual(7, run.Pigment, "both chapters count; zero and negative amounts are ignored");
            Assert.IsTrue(flow.TryDie(60));
            run.RecordPigment(5);
            Assert.AreEqual(7, run.Pigment, "an ended run keeps its result total");
        }

        [Test]
        public void DeathSettlesOnceWithElapsedTimeAndKills()
        {
            var flow = Flow(5); int settled = 0; RunResult last = default;
            flow.Ended += result => { settled++; last = result; };
            var run = flow.StartNewRun(10);
            run.RecordKill(); run.RecordKill();
            Assert.IsTrue(flow.TryDie(70));
            Assert.IsFalse(flow.TryDie(80));
            Assert.IsFalse(flow.TryClearChapter(90));
            run.RecordKill();
            Assert.AreEqual(1, settled);
            Assert.AreEqual(RunOutcome.Died, last.Outcome);
            Assert.AreEqual(60, last.ElapsedSeconds, 1e-9);
            Assert.AreEqual(2, last.Kills);
            Assert.AreEqual(5, last.Seed);
        }

        [Test]
        public void ChapterClearRequiresTheBossFirst()
        {
            var flow = Flow(1);
            var run = flow.StartNewRun(0);
            Assert.IsFalse(flow.TryClearChapter(5));
            Assert.IsFalse(run.Ended);
            run.MarkBossDefeated();
            Assert.IsTrue(flow.TryClearChapter(6));
            Assert.AreEqual(RunOutcome.ChapterCleared, run.Result.Value.Outcome);
            Assert.IsFalse(flow.TryDie(7));
        }

        [Test]
        public void NewRunReplacesAnEndedRunWithCleanCounters()
        {
            var flow = Flow(1, 2);
            var first = flow.StartNewRun(0); first.RecordKill(); first.MarkBossDefeated(); flow.TryDie(1);
            var second = flow.StartNewRun(2);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(0, second.Kills);
            Assert.IsFalse(second.BossDefeated);
            Assert.IsFalse(second.Ended);
        }

        [Test]
        public void ChapterClockReplacesTheStartTimeOnlyOnce()
        {
            var flow = Flow(1);
            var run = flow.StartNewRun(100);
            run.StartClock(3); run.StartClock(50);
            flow.TryDie(13);
            Assert.AreEqual(10, run.Result.Value.ElapsedSeconds, 1e-9);
        }

        [Test]
        public void ChapterTransitionKeepsRunAndOnlySettlesAtItsEnd()
        {
            var flow=Flow(42);int settlements=0;flow.Ended+=_=>settlements++;
            var run=flow.StartNewRun(0);run.StartClock(0);run.RecordKill();
            Assert.IsFalse(flow.TryAdvanceChapter(10));
            run.MarkBossDefeated();Assert.IsTrue(flow.TryAdvanceChapter(20));
            Assert.AreSame(run,flow.Current);Assert.AreEqual(2,run.Chapter);
            Assert.IsFalse(run.Ended);Assert.IsFalse(run.BossDefeated);Assert.AreEqual(0,settlements);
            Assert.IsFalse(flow.TryAdvanceChapter(21));
            run.StartClock(0);run.RecordKill();
            Assert.IsTrue(flow.TryDie(15));Assert.AreEqual(35,run.Result.Value.ElapsedSeconds);
            Assert.AreEqual(2,run.Kills);Assert.AreEqual(1,settlements);
            Assert.IsFalse(flow.TryDie(16));
        }

        [Test]
        public void EndingWithoutARunDoesNothing()
        {
            var flow = Flow();
            Assert.IsFalse(flow.TryDie(0));
            Assert.IsFalse(flow.TryClearChapter(0));
        }
    }
}
