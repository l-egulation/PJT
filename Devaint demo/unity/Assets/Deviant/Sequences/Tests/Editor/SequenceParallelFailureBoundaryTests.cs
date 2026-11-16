using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Sequences.Contracts;
using NUnit.Framework;

namespace Deviant.Sequences.Tests
{
    public class SequenceParallelFailureBoundaryTests
    {
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Operation : ISequenceOperation
        {
            public readonly string Id;
            public SequenceOperationStatus Result = SequenceOperationStatus.Running;
            public bool ThrowStatus, ThrowError, ThrowPoll, ThrowDispose;
            public int Polls, Disposals;
            bool disposed;
            public Operation(string id) { Id = id; }
            public SequenceOperationStatus Status
            {
                get { if (ThrowStatus) throw new InvalidOperationException("fixture status failed " + Id); return Result; }
            }
            public string Error
            {
                get { if (ThrowError) throw new InvalidOperationException("fixture error failed " + Id); return "fixture result " + Id; }
            }
            public void Poll(double elapsed)
            {
                Polls++; if (ThrowPoll) throw new InvalidOperationException("fixture poll failed " + Id);
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true; Disposals++;
                if (ThrowDispose) throw new InvalidOperationException("fixture dispose failed " + Id);
            }
        }
        sealed class Bindings : ISequenceBindings
        {
            public readonly Dictionary<string, Operation> Operations = new Dictionary<string, Operation>();
            public readonly List<string> Started = new List<string>();
            public bool Active = true, ThrowScopeDispose;
            public string ThrowBegin;
            public int ScopeDisposals;
            public Operation Add(string id) { var operation = new Operation(id); Operations.Add(id, operation); return operation; }
            public bool Validate(SequenceDefinition definition, out string error) { error = null; return true; }
            public ISequenceBindingScope OpenScope(Guid executionId, SequenceDefinition definition) => new Scope(this);
            sealed class Scope : ISequenceBindingScope
            {
                readonly Bindings owner;
                bool disposed;
                public Scope(Bindings owner) { this.owner = owner; }
                public bool IsActive => owner.Active && !disposed;
                public ISequenceOperation Begin(SequenceStep step)
                {
                    owner.Started.Add(step.Id);
                    if (owner.ThrowBegin == step.Id) throw new InvalidOperationException("fixture begin failed " + step.Id);
                    return owner.Operations[step.Id];
                }
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) => throw new InvalidOperationException("No skip endpoint is expected in this fixture.");
                public void Dispose()
                {
                    if (disposed) return; disposed = true; owner.ScopeDisposals++;
                    if (owner.ThrowScopeDispose) throw new InvalidOperationException("fixture scope release failed");
                }
            }
        }
        static SequenceDefinition Definition(params string[] ids) => new SequenceDefinition("fixture-parallel-boundaries",
            new[] { SequenceStep.ParallelAll("together", ids.Select(id => SequenceStep.Cue(id, id + "-binding", 10)).ToArray()) },
            Array.Empty<string>());
        static SequenceRunner Start(Bindings bindings, ControlSession controls, Clock clock, params string[] ids)
            => new SequenceRunner(Definition(ids), bindings, controls, new SequenceControlPolicy(InputChannels.Combat, true), clock);
        static void AssertOnlyExternalControl(ControlSession controls)
        {
            Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
            Assert.That(controls.Current.SimulationPaused, Is.True);
        }
        static SequenceStepSnapshot Child(SequenceSnapshot snapshot, string id) => snapshot.Children.Single(c => c.Id == id);

        [TestCase("status")] [TestCase("error")] [TestCase("poll")]
        public void CollaboratorAccessorOrPollExceptionReleasesEveryStartedPeer(string failure)
        {
            using (var controls = new ControlSession()) using (var modal = controls.OpenScope("fixture external modal"))
            {
                modal.BlockInput(InputChannels.Navigation); modal.PauseSimulation();
                var bindings = new Bindings(); var a = bindings.Add("a"); var b = bindings.Add("b"); var c = bindings.Add("c");
                b.ThrowStatus = failure == "status"; b.ThrowError = failure == "error"; b.ThrowPoll = failure == "poll";
                if (b.ThrowError) b.Result = SequenceOperationStatus.Failed;
                using (var runner = Start(bindings, controls, new Clock(), "a", "b", "c"))
                {
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.StepFailed));
                    Assert.That(runner.Read().Error, Does.Contain("fixture " + failure + " failed b"));
                    Assert.That(a.Disposals, Is.EqualTo(1)); Assert.That(b.Disposals, Is.EqualTo(1));
                    Assert.That(c.Disposals, Is.EqualTo(failure == "poll" ? 1 : 0));
                    Assert.That(c.Polls, Is.Zero, "A peer after an observed failure must not be polled.");
                    Assert.That(bindings.Started, Is.EqualTo(failure == "poll" ? new[] { "a", "b", "c" } : new[] { "a", "b" }));
                    Assert.That(bindings.ScopeDisposals, Is.EqualTo(1)); AssertOnlyExternalControl(controls);
                }
            }
        }
        [Test] public void PartialStartFailureKeepsItsCauseWhenPeerAndScopeCleanupAlsoFail()
        {
            using (var controls = new ControlSession()) using (var modal = controls.OpenScope("fixture external modal"))
            {
                modal.BlockInput(InputChannels.Navigation); modal.PauseSimulation();
                var bindings = new Bindings { ThrowBegin = "b", ThrowScopeDispose = true };
                var a = bindings.Add("a"); a.ThrowDispose = true; var b = bindings.Add("b"); var c = bindings.Add("c");
                using (var runner = Start(bindings, controls, new Clock(), "a", "b", "c"))
                {
                    var state = runner.Read(); Assert.That(state.Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(state.EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                    Assert.That(state.Error, Does.Contain("fixture begin failed b").And.Contain("fixture dispose failed a").And.Contain("fixture scope release failed"));
                    Assert.That(bindings.Started, Is.EqualTo(new[] { "a", "b" })); Assert.That(a.Disposals, Is.EqualTo(1));
                    Assert.That(b.Disposals + c.Disposals, Is.Zero, "The throwing binding owns its partial acquisition; no handle was returned.");
                    Assert.That(Child(state, "a").Status, Is.EqualTo(SequenceStepStatus.Failed));
                    Assert.That(Child(state, "b").Status, Is.EqualTo(SequenceStepStatus.Failed));
                    Assert.That(Child(state, "c").Status, Is.EqualTo(SequenceStepStatus.Pending));
                    Assert.That(bindings.ScopeDisposals, Is.EqualTo(1)); AssertOnlyExternalControl(controls);
                }
            }
        }
        [Test] public void SynchronousCompletionWithCleanupFailureStopsRemainingStarts()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var a = bindings.Add("a"); var b = bindings.Add("b"); var c = bindings.Add("c");
                b.Result = SequenceOperationStatus.Completed; b.ThrowDispose = true;
                using (var runner = Start(bindings, controls, new Clock(), "a", "b", "c"))
                {
                    var state = runner.Read(); Assert.That(state.EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                    Assert.That(Child(state, "b").Status, Is.EqualTo(SequenceStepStatus.Failed));
                    Assert.That(Child(state, "b").EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                    Assert.That(bindings.Started, Is.EqualTo(new[] { "a", "b" })); Assert.That(a.Disposals, Is.EqualTo(1)); Assert.That(b.Disposals, Is.EqualTo(1));
                    Assert.That(c.Disposals, Is.Zero); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void ClockOrScopeLossStopsAllPeersBeforeAnotherPoll(bool scopeLoss)
        {
            using (var controls = new ControlSession()) using (var modal = controls.OpenScope("fixture external modal"))
            {
                modal.BlockInput(InputChannels.Navigation); modal.PauseSimulation();
                var bindings = new Bindings(); var a = bindings.Add("a"); var b = bindings.Add("b"); var clock = new Clock();
                using (var runner = Start(bindings, controls, clock, "a", "b"))
                {
                    int polls = a.Polls + b.Polls;
                    if (scopeLoss) bindings.Active = false; else clock.Time = double.NaN;
                    runner.Poll(); var state = runner.Read(); Assert.That(state.Status, Is.EqualTo(SequenceStatus.Failed));
                    Assert.That(state.EndReason, Is.EqualTo(scopeLoss ? SequenceEndReason.BindingFailed : SequenceEndReason.ClockFailed));
                    Assert.That(a.Polls + b.Polls, Is.EqualTo(polls)); Assert.That(a.Disposals + b.Disposals, Is.EqualTo(2));
                    Assert.That(state.Children.All(c => c.Status == SequenceStepStatus.Cancelled), Is.True);
                    AssertOnlyExternalControl(controls);
                }
            }
        }
    }
}
