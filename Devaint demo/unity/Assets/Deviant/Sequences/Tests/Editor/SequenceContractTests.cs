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
    public class SequenceContractTests
    {
        public sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Operation : ISequenceOperation
        {
            public SequenceOperationStatus Status { get; set; } = SequenceOperationStatus.Running;
            public string Error { get; set; }
            public bool Disposed, ThrowDispose;
            public int Polls;
            public Action<double> OnPoll;
            public void Poll(double elapsed) { Polls++; OnPoll?.Invoke(elapsed); }
            public void Dispose() { if (Disposed) return; Disposed = true; if (ThrowDispose) throw new InvalidOperationException("operation release"); }
        }
        sealed class Bindings : ISequenceBindings
        {
            public readonly List<string> Started = new List<string>();
            public readonly Queue<Operation> Pending = new Queue<Operation>();
            public readonly Operation Skip = new Operation();
            public Scope Last;
            public bool Valid = true, ThrowValidate, ThrowOpen, ThrowBegin, ThrowScopeDispose;
            public int Opens, Skips;
            public bool Validate(SequenceDefinition definition, out string error)
            { if (ThrowValidate) throw new InvalidOperationException("preflight"); error = Valid ? null : "missing anchor"; return Valid; }
            public ISequenceBindingScope OpenScope(Guid execution, SequenceDefinition definition)
            { Opens++; if (ThrowOpen) throw new InvalidOperationException("scope start"); return Last = new Scope(this); }
            public sealed class Scope : ISequenceBindingScope
            {
                readonly Bindings owner;
                public bool Active = true, Disposed;
                public bool IsActive => Active && !Disposed;
                public Scope(Bindings owner) { this.owner = owner; }
                public ISequenceOperation Begin(SequenceStep step)
                { owner.Started.Add(step.Id); if (owner.ThrowBegin) throw new InvalidOperationException("step start"); return owner.Pending.Dequeue(); }
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) { owner.Skips++; return owner.Skip; }
                public void Dispose() { if (Disposed) return; Disposed = true; if (owner.ThrowScopeDispose) throw new InvalidOperationException("scope release"); }
            }
        }
        static SequenceDefinition Definition(bool skip = false) => new SequenceDefinition("fixture", new[] {
            SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("frame", "focus", 2), SequenceStep.Cue("effect", "sprite", 5)
        }, new[] { "focus" }, skip ? new SequenceSkipDefinition("fixture-end", 3) : null);
        static SequenceControlPolicy Policy => new SequenceControlPolicy(InputChannels.Combat | InputChannels.Interaction, true);
        static SequenceRunner Start(SequenceDefinition definition, Bindings bindings, ControlSession controls, Clock clock) => new SequenceRunner(definition, bindings, controls, Policy, clock);

        [Test] public void RuntimeAndContractsHaveNoEngineOrOtherFeatureImplementationDependencies()
        {
            foreach (var type in new[] { typeof(SequenceDefinition), typeof(ISequenceBindings), typeof(SequenceRunner) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Narrative") || a.Name.StartsWith("Deviant.Presentation") || a.Name.StartsWith("Deviant.Combat") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void DefinitionCopiesOrderedStepsAndCanonicalAnchorsAndHashesAllPolicies()
        {
            var steps = new[] { SequenceStep.Wait("a", 1), SequenceStep.Wait("b", 2) }; var anchors = new[] { "z", "a" };
            var definition = new SequenceDefinition("fixture", steps, anchors); steps[0] = SequenceStep.Wait("other", 3); anchors[0] = "other";
            Assert.That(definition.Steps[0].Id, Is.EqualTo("a")); Assert.That(definition.RequiredAnchors, Is.EqualTo(new[] { "a", "z" }));
            var same = new SequenceDefinition("fixture", new[] { SequenceStep.Wait("a", 1), SequenceStep.Wait("b", 2) }, new[] { "a", "z" });
            Assert.That(same.ContentHash, Is.EqualTo(definition.ContentHash));
            Assert.That(new SequenceDefinition("fixture", same.Steps.Reverse().ToArray(), new[] { "a", "z" }).ContentHash, Is.Not.EqualTo(same.ContentHash));
            Assert.That(new SequenceDefinition("fixture", same.Steps.ToArray(), new[] { "a", "z" }, new SequenceSkipDefinition("end", 1)).ContentHash, Is.Not.EqualTo(same.ContentHash));
            Assert.That(new SequenceDefinition("fixture", new[] { SequenceStep.Wait("a", 1.5), SequenceStep.Wait("b", 2) }, new[] { "a", "z" }).ContentHash, Is.Not.EqualTo(same.ContentHash));
        }
        [Test] public void InvalidIdentityDuplicateReferencesAndUnsupportedDurationsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new SequenceDefinition("", new[] { SequenceStep.Wait("a", 1) }, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new SequenceDefinition("fixture", Array.Empty<SequenceStep>(), Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new SequenceDefinition("fixture", new SequenceStep[] { null }, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new SequenceDefinition("fixture", new[] { SequenceStep.Wait("a", 1), SequenceStep.Wait("a", 2) }, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new SequenceDefinition("fixture", new[] { SequenceStep.Wait("a", 1) }, new[] { "anchor", "anchor" }));
            Assert.Throws<ArgumentException>(() => SequenceStep.Dialogue("a", " ", 1));
            Assert.Throws<ArgumentException>(() => SequenceStep.Fade("a", " ", 1));
            Assert.Throws<ArgumentException>(() => SequenceStep.StageMove("a", " ", 1));
            foreach (var value in new[] { 0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => SequenceStep.Wait("a", value));
                Assert.Throws<ArgumentOutOfRangeException>(() => SequenceStep.Camera("a", "focus", value));
                Assert.Throws<ArgumentOutOfRangeException>(() => SequenceStep.Cue("a", "effect", value));
                Assert.Throws<ArgumentOutOfRangeException>(() => SequenceStep.Fade("a", "fade", value));
                Assert.Throws<ArgumentOutOfRangeException>(() => SequenceStep.StageMove("a", "actor", value));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceSkipDefinition("end", value));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceControlPolicy((InputChannels)1024, false));
        }
        [Test] public void SequentialCompletionPinsStartingDefinitionAndReturnsOnlyOwnedControl()
        {
            using (var controls = new ControlSession()) using (var other = controls.OpenScope("modal"))
            {
                var block = other.BlockInput(InputChannels.Navigation); var pause = other.PauseSimulation();
                var bindings = new Bindings(); var a = new Operation(); var b = new Operation(); var c = new Operation();
                bindings.Pending.Enqueue(a); bindings.Pending.Enqueue(b); bindings.Pending.Enqueue(c); var clock = new Clock(); var definition = Definition();
                using (var runner = Start(definition, bindings, controls, clock))
                {
                    var initial = runner.Read(); Assert.That(bindings.Started, Is.EqualTo(new[] { "talk" }));
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation));
                    a.Status = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(a.Disposed, Is.True); Assert.That(bindings.Started, Is.EqualTo(new[] { "talk", "frame" }));
                    b.Status = SequenceOperationStatus.Completed; runner.Poll(); c.Status = SequenceOperationStatus.Completed; runner.Poll();
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Completed)); Assert.That(runner.Read().DefinitionVersion, Is.EqualTo(definition.ContentHash));
                    Assert.That(runner.Read().ExecutionId, Is.EqualTo(initial.ExecutionId)); Assert.That(c.Disposed && bindings.Last.Disposed, Is.True);
                    Assert.That(runner.TrySkip(initial.ExecutionId, out _), Is.False); Assert.That(runner.TryCancel(initial.ExecutionId, out _), Is.False);
                    runner.Poll(); runner.Dispose(); Assert.That(bindings.Started.Count, Is.EqualTo(3));
                    Assert.That(block.IsActive && pause.IsActive, Is.True); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(controls.Current.SimulationPaused, Is.True);
                }
            }
        }
        [Test] public void ANewStepStartsAtObservationTimeAndDoesNotInheritPreviousElapsedTime()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var clock = new Clock(); var a = new Operation(); bindings.Pending.Enqueue(a);
                var definition = new SequenceDefinition("fixture", new[] { SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Wait("wait", 2) }, Array.Empty<string>());
                using (var runner = Start(definition, bindings, controls, clock))
                {
                    clock.Time = 8; a.Status = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().StepId, Is.EqualTo("wait"));
                    clock.Time = 9; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Running));
                    clock.Time = 10; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                }
            }
        }
        [Test] public void MissingSkipPolicyAndOldExecutionIdsCannotMutateActiveWork()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var active = new Operation(); bindings.Pending.Enqueue(active);
                using (var runner = Start(Definition(), bindings, controls, new Clock()))
                {
                    Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False);
                    Assert.That(runner.TryCancel(Guid.NewGuid(), out _), Is.False); Assert.That(active.Disposed, Is.False); Assert.That(bindings.Skips, Is.Zero);
                    Assert.That(runner.TryCancel(runner.Read().ExecutionId, out _), Is.True); Assert.That(active.Disposed, Is.True);
                    active.Status = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
                    Assert.That(bindings.Started, Is.EqualTo(new[] { "talk" })); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                }
            }
        }
        [Test] public void ExplicitSkipCancelsCurrentAndCallsOnlyTheDeclaredEndpointOnce()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var a = new Operation(); bindings.Pending.Enqueue(a); var clock = new Clock();
                using (var runner = Start(Definition(true), bindings, controls, clock))
                {
                    var id = runner.Read().ExecutionId;
                    Assert.That(runner.TrySkip(id, out _), Is.True); Assert.That(a.Disposed, Is.True); Assert.That(bindings.Skips, Is.EqualTo(1));
                    Assert.That(runner.Read().IsSkipping, Is.True); Assert.That(runner.TrySkip(id, out _), Is.False); Assert.That(controls.Current.SimulationPaused, Is.True);
                    bindings.Skip.Status = SequenceOperationStatus.Completed; runner.Poll();
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Skipped)); Assert.That(bindings.Started, Is.EqualTo(new[] { "talk" }));
                    Assert.That(bindings.Skip.Disposed, Is.True); runner.Poll(); Assert.That(bindings.Skips, Is.EqualTo(1)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [TestCase(false)] [TestCase(true)] public void ExternalStepAndSkipHaveDeadlinesAndLateSuccessCannotReviveThem(bool skip)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var a = new Operation(); bindings.Pending.Enqueue(a); var clock = new Clock();
                using (var runner = Start(Definition(skip), bindings, controls, clock))
                {
                    if (skip) Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.True);
                    var operation = skip ? bindings.Skip : a; operation.Status = SequenceOperationStatus.Completed;
                    clock.Time = skip ? 3 : 10; runner.Poll();
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.TimedOut));
                    operation.Status = SequenceOperationStatus.Completed; runner.Poll(); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed));
                    Assert.That(operation.Disposed && bindings.Last.Disposed, Is.True); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [TestCase(SequenceOperationStatus.Cancelled, SequenceStatus.Cancelled)]
        [TestCase(SequenceOperationStatus.Failed, SequenceStatus.Failed)]
        public void ChildCancellationAndFailureStopRemainingSteps(SequenceOperationStatus child, SequenceStatus expected)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); bindings.Pending.Enqueue(new Operation { Status = child, Error = "fixture result" });
                using (var runner = Start(Definition(), bindings, controls, new Clock()))
                {
                    Assert.That(runner.Read().Status, Is.EqualTo(expected)); Assert.That(runner.Read().Error, Does.Contain("fixture result"));
                    Assert.That(bindings.Started.Count, Is.EqualTo(1)); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                }
            }
        }
        [TestCase("validate")] [TestCase("validate-throw")] [TestCase("open")] [TestCase("begin")]
        public void FailedPreflightOrBindingStartReturnsAcquiredControl(string failure)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings { Valid = failure != "validate", ThrowValidate = failure == "validate-throw", ThrowOpen = failure == "open", ThrowBegin = failure == "begin" };
                using (var runner = Start(Definition(), bindings, controls, new Clock()))
                {
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(runner.Read().Error, Is.Not.Empty);
                    Assert.That(controls.Current.SimulationPaused, Is.False); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                    if (failure.StartsWith("validate")) Assert.That(bindings.Opens, Is.Zero);
                    if (failure == "begin") Assert.That(bindings.Last.Disposed, Is.True);
                }
            }
        }
        [Test] public void CleanupFailuresStillReturnOtherScopesAndPreventSkipEndpoint()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings { ThrowScopeDispose = true }; var op = new Operation { ThrowDispose = true }; bindings.Pending.Enqueue(op);
                using (var runner = Start(Definition(true), bindings, controls, new Clock()))
                {
                    Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False);
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                    Assert.That(runner.Read().Error, Does.Contain("operation release").And.Contain("scope release"));
                    Assert.That(bindings.Skips, Is.Zero); Assert.That(bindings.Last.Disposed, Is.True); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void ScopeLossAndOwnerDisposalNeverCompleteOrLeakControl()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); bindings.Pending.Enqueue(new Operation());
                var runner = Start(Definition(), bindings, controls, new Clock()); bindings.Last.Active = false; runner.Poll();
                Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); Assert.That(controls.Current.SimulationPaused, Is.False);
                var nextBindings = new Bindings(); nextBindings.Pending.Enqueue(new Operation());
                var next = Start(Definition(), nextBindings, controls, new Clock()); runner.Dispose();
                Assert.That(next.TryCancel(runner.Read().ExecutionId, out _), Is.False); Assert.That(controls.Current.SimulationPaused, Is.True);
                next.Dispose(); next.Dispose(); Assert.That(next.Read().EndReason, Is.EqualTo(SequenceEndReason.OwnerDisposed)); Assert.That(controls.Current.SimulationPaused, Is.False);
            }
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
        public void InvalidOrBackwardClockFailsAndReturnsOwnerScopes(double time)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); bindings.Pending.Enqueue(new Operation()); var clock = new Clock { Time = 1 };
                using (var runner = Start(Definition(), bindings, controls, clock))
                {
                    clock.Time = time; runner.Poll(); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.ClockFailed));
                    Assert.That(controls.Current.SimulationPaused, Is.False); Assert.That(bindings.Last.Disposed, Is.True);
                }
            }
        }
        [Test] public void ReentrantMutationIsRejectedWhileTheOriginalStepRemainsOwned()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var op = new Operation(); bindings.Pending.Enqueue(op);
                using (var runner = Start(Definition(true), bindings, controls, new Clock()))
                {
                    op.OnPoll = elapsed => {
                        Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False);
                        Assert.That(runner.TryCancel(runner.Read().ExecutionId, out _), Is.False);
                        Assert.Throws<InvalidOperationException>(() => runner.Dispose()); Assert.Throws<InvalidOperationException>(() => runner.Poll());
                    };
                    runner.Poll(); Assert.That(op.Disposed, Is.False); Assert.That(bindings.Skips, Is.Zero); Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Running));
                }
            }
        }
    }
}
