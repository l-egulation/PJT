using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerComboExecutionTests
    {
        struct Host : IPlayerComboHost
        {
            public float Time;
            public int Count, ClockReads, CountReads;
            public bool FailCount;
            public float Now { get { ClockReads++; return Time; } }
            public int BasicCount
            {
                get
                {
                    CountReads++;
                    if (FailCount) throw new InvalidOperationException("form count unavailable");
                    return Count;
                }
            }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float value) => BitConverter.Int32BitsToSingle(Bits(value) - 1);
        static float After(float value) => BitConverter.Int32BitsToSingle(Bits(value) + 1);

        [Test] public void NewOwnerHasOriginalMinusOneIndexAndZeroDeadline()
        {
            var combo = PlayerComboExecution.Create(); var read = combo.Read();
            Assert.That(read.BasicIndex, Is.EqualTo(-1)); Assert.That(Bits(read.LateUntil), Is.Zero);
            var host = new Host { Time = 0, FailCount = true };
            Assert.That(combo.SelectStart(ref host), Is.Zero); Assert.That(combo.BasicIndex, Is.EqualTo(-1));
            Assert.That(host.ClockReads, Is.EqualTo(1)); Assert.That(host.CountReads, Is.Zero);
        }
        [Test] public void GraceEndsInclusivelyAtKnownPointFourTwoFiveAndDoesNotConsumeTheWindow()
        {
            var combo = PlayerComboExecution.Create(); combo.BeginBasic(0); combo.OpenWindow(.125f);
            var host = new Host { Count = 3, Time = Before(.425f) };
            Assert.That(Bits(combo.Read().LateUntil), Is.EqualTo(Bits(.425f)));
            Assert.That(combo.SelectStart(ref host), Is.EqualTo(1));
            host.Time = .425f;
            Assert.That(combo.SelectStart(ref host), Is.EqualTo(1)); Assert.That(combo.SelectStart(ref host), Is.EqualTo(1));
            Assert.That(combo.BasicIndex, Is.Zero); Assert.That(Bits(combo.Read().LateUntil), Is.EqualTo(Bits(.425f)));
            host.Time = After(.425f); host.FailCount = true;
            Assert.That(combo.SelectStart(ref host), Is.Zero); Assert.That(host.CountReads, Is.EqualTo(3));
            Assert.That(combo.BasicIndex, Is.Zero, "Selection alone neither commits nor expires stored combo history.");
        }
        [Test] public void ZeroCompletionKeepsTheExactPointThreeBoundaryAndNewCompletionReplacesIt()
        {
            var combo = PlayerComboExecution.Create(); combo.BeginBasic(1); combo.OpenWindow(0);
            Assert.That(Bits(combo.Read().LateUntil), Is.EqualTo(Bits(.3f)));
            var host = new Host { Count = 3, Time = .3f };
            Assert.That(combo.SelectStart(ref host), Is.EqualTo(2)); host.Time = After(.3f);
            Assert.That(combo.SelectStart(ref host), Is.Zero);
            combo.OpenWindow(.125f); host.Time = .425f;
            Assert.That(combo.SelectStart(ref host), Is.EqualTo(2));
            combo.OpenWindow(0); Assert.That(combo.SelectStart(ref host), Is.Zero);
        }
        [Test] public void QueuedSelectionUsesTheCurrentCountWithoutReadingAClockOrChangingTheCommittedHit()
        {
            var combo = PlayerComboExecution.Create(); combo.BeginBasic(1); combo.OpenWindow(.125f);
            var host = new Host { Count = 4, Time = 100 };
            Assert.That(combo.SelectNext(ref host), Is.EqualTo(2));
            host.Count = 2; Assert.That(combo.SelectNext(ref host), Is.Zero);
            host.Count = 3; Assert.That(combo.SelectNext(ref host), Is.EqualTo(2));
            host.Count = 1; Assert.That(combo.SelectNext(ref host), Is.Zero);
            Assert.That(host.ClockReads, Is.Zero); Assert.That(host.CountReads, Is.EqualTo(4));
            Assert.That(combo.BasicIndex, Is.EqualTo(1)); Assert.That(combo.Read().LateUntil, Is.EqualTo(.425f));
        }
        [Test] public void EachExplicitCycleCommitsOnlyItsChosenIndexAndNeverOpensANewWindow()
        {
            var combo = PlayerComboExecution.Create(); var host = new Host { Count = 4 };
            foreach (int expected in new[] { 0, 1, 2, 3, 0, 1 })
            {
                int selected = combo.SelectNext(ref host); Assert.That(selected, Is.EqualTo(expected));
                Assert.That(combo.BasicIndex, Is.Not.EqualTo(selected)); combo.BeginBasic(selected);
                Assert.That(combo.BasicIndex, Is.EqualTo(expected)); Assert.That(Bits(combo.Read().LateUntil), Is.Zero);
            }
            host.Count = 1; Assert.That(combo.SelectNext(ref host), Is.Zero);
        }
        [Test] public void LateSelectionAlsoUsesTheLiveFormCountRatherThanTheCountAtCompletion()
        {
            var combo = PlayerComboExecution.Create(); combo.BeginBasic(1); combo.OpenWindow(.125f);
            var host = new Host { Count = 2, Time = .25f };
            Assert.That(combo.SelectStart(ref host), Is.Zero);
            host.Count = 3; Assert.That(combo.SelectStart(ref host), Is.EqualTo(2));
            host.Count = 1; Assert.That(combo.SelectStart(ref host), Is.Zero);
            Assert.That(combo.BasicIndex, Is.EqualTo(1));
        }
        [Test] public void ClearResetsIndexAndDeadlineWithoutResettingAnotherPlayerOrOldSnapshots()
        {
            var first = PlayerComboExecution.Create(); var second = PlayerComboExecution.Create();
            first.BeginBasic(2); first.OpenWindow(.125f); second.BeginBasic(1); second.OpenWindow(1.25f);
            var old = first.Read(); first.Clear(); var cleared = first.Read();
            Assert.That(cleared.BasicIndex, Is.EqualTo(-1)); Assert.That(cleared.LateUntil, Is.EqualTo(-1));
            Assert.That(old.BasicIndex, Is.EqualTo(2)); Assert.That(old.LateUntil, Is.EqualTo(.425f));
            Assert.That(second.BasicIndex, Is.EqualTo(1)); Assert.That(second.Read().LateUntil, Is.EqualTo(1.55f));
            var host = new Host { Count = 3, Time = .25f };
            Assert.That(first.SelectStart(ref host), Is.Zero); Assert.That(first.SelectNext(ref host), Is.Zero);
            Assert.That(second.SelectStart(ref host), Is.EqualTo(2)); first.Clear();
            Assert.That(first.Read().LateUntil, Is.EqualTo(-1));
        }
        [Test] public void BeginCommitsBeforeAConsumerFailureAndDoesNotRollBackTheExistingDeadline()
        {
            var combo = PlayerComboExecution.Create(); combo.BeginBasic(0); combo.OpenWindow(.125f);
            combo.BeginBasic(2); var host = new Host { Time = .25f, Count = 3, FailCount = true };
            Assert.Throws<InvalidOperationException>(() => combo.SelectStart(ref host));
            Assert.That(combo.BasicIndex, Is.EqualTo(2)); Assert.That(combo.Read().LateUntil, Is.EqualTo(.425f));
            host.FailCount = false;
            Assert.That(combo.SelectStart(ref host), Is.Zero, "An exception does not invent a rollback of an already committed index.");
            Assert.That(host.ClockReads, Is.EqualTo(2)); Assert.That(host.CountReads, Is.EqualTo(2));
        }
        [Test] public void ExpiredSelectionDoesNotQueryAnUnavailableFormButQueuedSelectionStillDoes()
        {
            var combo = PlayerComboExecution.Create(); combo.BeginBasic(3); combo.OpenWindow(.125f);
            var host = new Host { Time = .5f, FailCount = true };
            Assert.That(combo.SelectStart(ref host), Is.Zero); Assert.That(host.CountReads, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => combo.SelectNext(ref host));
            Assert.That(host.ClockReads, Is.EqualTo(1)); Assert.That(host.CountReads, Is.EqualTo(1));
            Assert.That(combo.BasicIndex, Is.EqualTo(3)); Assert.That(combo.Read().LateUntil, Is.EqualTo(.425f));
        }
    }
}
