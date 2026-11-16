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
    public class SequenceParallelTests
    {
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Operation : ISequenceOperation
        {
            public SequenceOperationStatus Result = SequenceOperationStatus.Running;
            public SequenceOperationStatus Status => Result;
            public string Error { get; set; }
            public readonly List<double> Elapsed = new List<double>();
            public bool Disposed, ThrowDispose;
            public int Returns;
            public Action OnPoll, OnDispose;
            public void Poll(double elapsed) { Elapsed.Add(elapsed); OnPoll?.Invoke(); }
            public void Dispose()
            {
                if (Disposed) return; Disposed = true; Returns++; OnDispose?.Invoke();
                if (ThrowDispose) throw new InvalidOperationException(Error ?? "fixture release failed");
            }
        }
        sealed class Bindings : ISequenceBindings
        {
            public readonly Dictionary<string, Operation> Operations = new Dictionary<string, Operation>(StringComparer.Ordinal);
            public readonly List<string> Started = new List<string>();
            public readonly Operation Endpoint = new Operation();
            public string ThrowBegin, NullBegin;
            public bool ScopeDisposed, ThrowScopeDispose;
            public int Skips;
            public bool Validate(SequenceDefinition definition, out string error) { error = null; return true; }
            public ISequenceBindingScope OpenScope(Guid executionId, SequenceDefinition definition) => new Scope(this);
            public Operation Add(string id, SequenceOperationStatus status = SequenceOperationStatus.Running)
            { var operation = new Operation { Result = status }; Operations.Add(id, operation); return operation; }
            sealed class Scope : ISequenceBindingScope
            {
                readonly Bindings owner;
                public Scope(Bindings owner) { this.owner = owner; }
                public bool IsActive => !owner.ScopeDisposed;
                public ISequenceOperation Begin(SequenceStep step)
                {
                    Assert.That(step.Kind, Is.Not.EqualTo(SequenceStepKind.ParallelAll).And.Not.EqualTo(SequenceStepKind.Wait));
                    owner.Started.Add(step.Id);
                    if (step.Id == owner.ThrowBegin) throw new InvalidOperationException("fixture start failed");
                    return step.Id == owner.NullBegin ? null : owner.Operations[step.Id];
                }
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) { owner.Skips++; return owner.Endpoint; }
                public void Dispose()
                {
                    if (owner.ScopeDisposed) return; owner.ScopeDisposed = true;
                    if (owner.ThrowScopeDispose) throw new InvalidOperationException("fixture scope release failed");
                }
            }
        }
        static SequenceDefinition Definition(SequenceStep[] steps, bool skip = false) => new SequenceDefinition("parallel-fixture", steps, Array.Empty<string>(),
            skip ? new SequenceSkipDefinition("fixture.end", 3) : null);
        static SequenceRunner Start(SequenceDefinition definition, Bindings bindings, ControlSession controls, Clock clock)
            => new SequenceRunner(definition, bindings, controls, new SequenceControlPolicy(InputChannels.Combat, true), clock);
        static SequenceStepSnapshot Child(SequenceSnapshot snapshot, string id) => snapshot.Children.Single(c => c.Id == id);

        [Test] public void ExistingSequentialDefinitionsKeepTheirContentHashes()
        {
            var steps = new[] { SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("frame", "focus", 2), SequenceStep.Cue("effect", "sprite", 5) };
            // Captured from the previous compiled Definitions DLL before adding groups.
            Assert.That(new SequenceDefinition("fixture", steps, new[] { "focus" }).ContentHash, Is.EqualTo("b8d6e05f200bbaf561939ee13213fbc3c54a481f76a16205d8015f43598be5da"));
            Assert.That(new SequenceDefinition("fixture", steps, new[] { "focus" }, new SequenceSkipDefinition("fixture-end", 3)).ContentHash, Is.EqualTo("f3fc893b7421773d02edc504dc1b063fb4bd07598446dbb2c0ad22d79a35dc5d"));
        }
        [Test] public void ParallelDefinitionCopiesChildrenAndHashesOrderGroupAndChildSettings()
        {
            var children = new[] { SequenceStep.Wait("a", 1), SequenceStep.Cue("b", "sprite", 2) };
            var group = SequenceStep.ParallelAll("together", children); var definition = Definition(new[] { group }); children[0] = SequenceStep.Wait("other", 5);
            Assert.That(group.Children.Select(c => c.Id), Is.EqualTo(new[] { "a", "b" })); Assert.That(definition.Leaves.Select(c => c.Id), Is.EqualTo(new[] { "a", "b" }));
            Assert.Throws<NotSupportedException>(() => ((IList<SequenceStep>)group.Children)[0] = children[0]);
            Assert.That(Definition(new[] { SequenceStep.ParallelAll("together", group.Children.ToArray()) }).ContentHash, Is.EqualTo(definition.ContentHash));
            foreach (var changed in new[] {
                SequenceStep.ParallelAll("other-group", group.Children.ToArray()), SequenceStep.ParallelAll("together", group.Children.Reverse().ToArray()),
                SequenceStep.ParallelAll("together", SequenceStep.Wait("a", 1.5), group.Children[1]),
                SequenceStep.ParallelAll("together", group.Children[0], SequenceStep.Cue("b", "other-sprite", 2)),
                SequenceStep.ParallelAll("together", group.Children[0], SequenceStep.Cue("b", "sprite", 3)) })
                Assert.That(Definition(new[] { changed }).ContentHash, Is.Not.EqualTo(definition.ContentHash));
            Assert.That(Definition(group.Children.ToArray()).ContentHash, Is.Not.EqualTo(definition.ContentHash));
        }
        [Test] public void EmptyNestedAndDuplicateGroupOrChildIdsAreRejectedBeforeExecution()
        {
            Assert.Throws<ArgumentException>(() => SequenceStep.ParallelAll("group", (SequenceStep[])null));
            Assert.Throws<ArgumentException>(() => SequenceStep.ParallelAll("group"));
            Assert.Throws<ArgumentException>(() => SequenceStep.ParallelAll("group", new SequenceStep[] { null }));
            Assert.Throws<ArgumentException>(() => SequenceStep.ParallelAll("group", SequenceStep.Wait("a", 1), SequenceStep.Wait("a", 2)));
            Assert.Throws<ArgumentException>(() => SequenceStep.ParallelAll("group", SequenceStep.Wait("group", 1)));
            var group = SequenceStep.ParallelAll("group", SequenceStep.Wait("a", 1));
            Assert.Throws<ArgumentException>(() => SequenceStep.ParallelAll("outer", group));
            Assert.Throws<ArgumentException>(() => Definition(new[] { group, SequenceStep.Wait("a", 2) }));
            Assert.Throws<ArgumentException>(() => Definition(new[] { group, SequenceStep.ParallelAll("other", SequenceStep.Wait("a", 2)) }));
            Assert.Throws<ArgumentException>(() => Definition(new[] { group, SequenceStep.ParallelAll("other", SequenceStep.Wait("group", 2)) }));
        }
        [Test] public void StartsAllChildrenInOrderAndReturnsCompletedHandlesWhileWaitingForPeers()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var fast = bindings.Add("fast"); var slow = bindings.Add("slow"); var after = bindings.Add("after"); var clock = new Clock();
                var definition = Definition(new[] { SequenceStep.ParallelAll("together", SequenceStep.Cue("fast", "sprite", 5), SequenceStep.Camera("slow", "focus", 2), SequenceStep.Wait("wait", 1)), SequenceStep.Dialogue("after", "npc", 10) });
                using (var runner = Start(definition, bindings, controls, clock))
                {
                    Assert.That(bindings.Started, Is.EqualTo(new[] { "fast", "slow" })); var original = runner.Read();
                    Assert.That(original.Children.All(c => c.Status == SequenceStepStatus.Running), Is.True);
                    clock.Time = .5; fast.Result = SequenceOperationStatus.Completed; runner.Poll();
                    Assert.That(fast.Disposed, Is.True); Assert.That(slow.Disposed, Is.False); Assert.That(bindings.Started.Count, Is.EqualTo(2));
                    Assert.That(Child(runner.Read(), "fast").Status, Is.EqualTo(SequenceStepStatus.Completed)); Assert.That(Child(original, "fast").Status, Is.EqualTo(SequenceStepStatus.Running));
                    Assert.That(fast.Elapsed, Is.EqualTo(new[] { 0d, .5 })); Assert.That(slow.Elapsed, Is.EqualTo(new[] { 0d, .5 }));
                    clock.Time = 1; runner.Poll(); Assert.That(Child(runner.Read(), "wait").Status, Is.EqualTo(SequenceStepStatus.Completed)); Assert.That(controls.Current.SimulationPaused, Is.True);
                    clock.Time = 2; slow.Result = SequenceOperationStatus.Completed; runner.Poll();
                    Assert.That(runner.Read().StepId, Is.EqualTo("after")); Assert.That(runner.Read().Children, Is.Empty); Assert.That(after.Elapsed, Is.EqualTo(new[] { 0d }));
                    after.Result = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                    Assert.That(fast.Returns, Is.EqualTo(1)); Assert.That(slow.Returns, Is.EqualTo(1)); Assert.That(after.Returns, Is.EqualTo(1)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void CompletedChildDeadlineIsNotReappliedWhileAnotherChildContinues()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var early = bindings.Add("early"); var clock = new Clock();
                using (var runner = Start(Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Cue("early", "cue", .5), SequenceStep.Wait("long", 2)) }), bindings, controls, clock))
                {
                    clock.Time = .25; early.Result = SequenceOperationStatus.Completed; runner.Poll(); int polls = early.Elapsed.Count;
                    clock.Time = 1; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(early.Elapsed.Count, Is.EqualTo(polls));
                    clock.Time = 2; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Completed)); Assert.That(runner.Read().Children.All(c => c.Status == SequenceStepStatus.Completed), Is.True);
                    runner.Poll(); runner.Dispose(); Assert.That(early.Returns, Is.EqualTo(1));
                }
            }
        }
        [Test] public void GroupStartsAtTheObservationOfThePreviousStepWithoutInheritedTime()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var cue = bindings.Add("cue"); var clock = new Clock();
                using (var runner = Start(Definition(new[] { SequenceStep.Wait("before", 1), SequenceStep.ParallelAll("group", SequenceStep.Cue("cue", "sprite", 1), SequenceStep.Wait("hold", .5)) }), bindings, controls, clock))
                {
                    Assert.That(bindings.Started, Is.Empty); clock.Time = 10; runner.Poll(); Assert.That(cue.Elapsed, Is.EqualTo(new[] { 0d }));
                    clock.Time = 10.5; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Running));
                    clock.Time = 10.75; cue.Result = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                }
            }
        }
        [TestCase("start")] [TestCase("null")] [TestCase("poll")] [TestCase("result")] [TestCase("cancel")] [TestCase("timeout")]
        public void ChildFailureStopsTheGroupReturnsSiblingsAndNeverStartsTheFollowingStep(string failure)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var first = bindings.Add("first"); var broken = bindings.Add("broken"); var last = bindings.Add("last"); var clock = new Clock();
                if (failure == "start") bindings.ThrowBegin = "broken"; if (failure == "null") bindings.NullBegin = "broken";
                var definition = Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Cue("first", "sprite", 5), SequenceStep.Cue("broken", "sprite", .5), SequenceStep.Cue("last", "sprite", 5)), SequenceStep.Wait("after", 1) });
                using (var runner = Start(definition, bindings, controls, clock))
                {
                    if (failure == "poll") broken.OnPoll = () => throw new InvalidOperationException("fixture poll failed");
                    if (failure == "result") { broken.Result = SequenceOperationStatus.Failed; broken.Error = "fixture operation failed"; }
                    if (failure == "cancel") { broken.Result = SequenceOperationStatus.Cancelled; broken.Error = "fixture child cancelled"; }
                    if (failure == "timeout") { broken.Result = SequenceOperationStatus.Completed; clock.Time = .5; }
                    runner.Poll(); var result = runner.Read();
                    Assert.That(result.Status, Is.EqualTo(failure == "cancel" ? SequenceStatus.Cancelled : SequenceStatus.Failed));
                    Assert.That(result.EndReason, Is.EqualTo(failure == "timeout" ? SequenceEndReason.TimedOut : failure == "cancel" ? SequenceEndReason.StepCancelled : SequenceEndReason.StepFailed));
                    Assert.That(result.Error, Does.Contain("broken")); Assert.That(first.Disposed, Is.True); Assert.That(bindings.ScopeDisposed, Is.True);
                    bool beforeLast = failure == "start" || failure == "null";
                    Assert.That(Child(result, "last").Status, Is.EqualTo(beforeLast ? SequenceStepStatus.Pending : SequenceStepStatus.Cancelled)); Assert.That(last.Disposed, Is.EqualTo(!beforeLast));
                    Assert.That(bindings.Started.Contains("after"), Is.False); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                    broken.Result = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(result.Status));
                }
            }
        }
        [Test] public void StartingFailureKeepsAlreadyCompletedChildrenAndDoesNotStartLaterChildren()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings { ThrowBegin = "broken" }; var confirmed = bindings.Add("confirmed", SequenceOperationStatus.Completed); var clock = new Clock();
                using (var runner = Start(Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Cue("confirmed", "cue", 5), SequenceStep.Camera("broken", "camera", 1), SequenceStep.Cue("unstarted", "cue", 5)) }), bindings, controls, clock))
                {
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(Child(runner.Read(), "confirmed").Status, Is.EqualTo(SequenceStepStatus.Completed));
                    Assert.That(Child(runner.Read(), "unstarted").Status, Is.EqualTo(SequenceStepStatus.Pending)); Assert.That(confirmed.Returns, Is.EqualTo(1));
                }
            }
        }
        [Test] public void ExplicitSkipReturnsEveryActiveChildAndCallsOnlyOneEndpoint()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var confirmed = bindings.Add("confirmed"); var active = bindings.Add("active"); var clock = new Clock();
                using (var runner = Start(Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Cue("confirmed", "cue", 5), SequenceStep.Dialogue("active", "npc", 10)), SequenceStep.Wait("after", 1) }, true), bindings, controls, clock))
                {
                    confirmed.Result = SequenceOperationStatus.Completed; runner.Poll(); var id = runner.Read().ExecutionId;
                    Assert.That(runner.TrySkip(id, out var error), Is.True, error); Assert.That(bindings.Skips, Is.EqualTo(1)); Assert.That(active.Returns, Is.EqualTo(1));
                    Assert.That(Child(runner.Read(), "confirmed").Status, Is.EqualTo(SequenceStepStatus.Completed)); Assert.That(Child(runner.Read(), "active").EndReason, Is.EqualTo(SequenceEndReason.Skipped));
                    Assert.That(runner.TrySkip(id, out _), Is.False); Assert.That(controls.Current.SimulationPaused, Is.True);
                    bindings.Endpoint.Result = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Skipped));
                    Assert.That(bindings.Started, Is.EqualTo(new[] { "confirmed", "active" })); Assert.That(controls.Current.SimulationPaused, Is.False); Assert.That(bindings.Endpoint.Returns, Is.EqualTo(1));
                }
            }
        }
        [TestCase(false)] [TestCase(true)] public void CancelAndOwnerDisposalReturnAllChildrenAndPreserveOtherOwners(bool dispose)
        {
            using (var controls = new ControlSession()) using (var other = controls.OpenScope("external modal"))
            {
                var block = other.BlockInput(InputChannels.Navigation); var pause = other.PauseSimulation();
                var bindings = new Bindings(); var a = bindings.Add("a"); var b = bindings.Add("b");
                using (var runner = Start(Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Cue("a", "cue", 5), SequenceStep.Cue("b", "cue", 5), SequenceStep.Wait("wait", 1)) }, true), bindings, controls, new Clock()))
                {
                    a.OnDispose = () => { Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Cancelled)); Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False); };
                    if (dispose) runner.Dispose(); else Assert.That(runner.TryCancel(runner.Read().ExecutionId, out _), Is.True);
                    Assert.That(runner.Read().Children.All(c => c.Status == SequenceStepStatus.Cancelled), Is.True); Assert.That(a.Returns == 1 && b.Returns == 1, Is.True);
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(block.IsActive && pause.IsActive, Is.True);
                    b.Result = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
                }
            }
        }
        [TestCase(false)] [TestCase(true)] public void CleanupFailureStillReturnsEverySiblingAndPreventsSkipEndpoint(bool skip)
        {
            using (var controls = new ControlSession()) using (var other = controls.OpenScope("external modal"))
            {
                other.BlockInput(InputChannels.Navigation);
                var bindings = new Bindings { ThrowScopeDispose = true }; var a = bindings.Add("a"); var b = bindings.Add("b");
                a.ThrowDispose = b.ThrowDispose = true; a.Error = "fixture a release"; b.Error = "fixture b release";
                using (var runner = Start(Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Cue("a", "cue", 5), SequenceStep.Cue("b", "cue", 5)) }, true), bindings, controls, new Clock()))
                {
                    if (skip) Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False); else { a.Result = SequenceOperationStatus.Completed; runner.Poll(); }
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                    Assert.That(runner.Read().Error, Does.Contain("fixture a release").And.Contain("fixture b release").And.Contain("scope release"));
                    Assert.That(a.Returns == 1 && b.Returns == 1 && bindings.ScopeDisposed, Is.True); Assert.That(bindings.Skips, Is.Zero);
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void ACapableBindingCanStartMultipleDialogueChildrenAndCallbacksCannotReenterTheRunner()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var a = bindings.Add("a"); var b = bindings.Add("b");
                using (var runner = Start(Definition(new[] { SequenceStep.ParallelAll("group", SequenceStep.Dialogue("a", "npc-a", 5), SequenceStep.Dialogue("b", "npc-b", 5)) }, true), bindings, controls, new Clock()))
                {
                    a.OnPoll = () => { Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False); Assert.Throws<InvalidOperationException>(() => runner.Dispose()); };
                    runner.Poll(); Assert.That(bindings.Started, Is.EqualTo(new[] { "a", "b" })); Assert.That(a.Disposed || b.Disposed, Is.False);
                    a.Result = b.Result = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                }
            }
        }
    }
}
