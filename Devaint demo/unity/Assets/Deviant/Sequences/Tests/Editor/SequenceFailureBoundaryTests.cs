using System;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Sequences.Contracts;
using NUnit.Framework;

namespace Deviant.Sequences.Tests
{
    public class SequenceFailureBoundaryTests
    {
        sealed class Clock : ISequenceClock
        {
            public double Time;
            public bool Throw;
            public int Reads;
            public double ReadTime() { Reads++; if (Throw) throw new InvalidOperationException("fixture clock failed"); return Time; }
        }
        sealed class Operation : ISequenceOperation
        {
            public SequenceOperationStatus Status => SequenceOperationStatus.Running;
            public string Error => null;
            public bool Disposed, ThrowPoll, ThrowDispose;
            public Action OnDispose;
            public void Poll(double elapsed) { if (ThrowPoll) throw new InvalidOperationException("fixture poll failed"); }
            public void Dispose()
            {
                if (Disposed) return; Disposed = true; OnDispose?.Invoke();
                if (ThrowDispose) throw new InvalidOperationException("fixture operation cleanup failed");
            }
        }
        sealed class Bindings : ISequenceBindings
        {
            public readonly Operation Operation = new Operation();
            public int Opens, Skips;
            public bool Valid = true, ThrowBegin, Disposed;
            public bool Validate(SequenceDefinition definition, out string error) { error = Valid ? null : "fixture preflight failed"; return Valid; }
            public ISequenceBindingScope OpenScope(Guid executionId, SequenceDefinition definition) { Opens++; return new Scope(this); }
            sealed class Scope : ISequenceBindingScope
            {
                readonly Bindings owner;
                public Scope(Bindings owner) { this.owner = owner; }
                public bool IsActive => !owner.Disposed;
                public ISequenceOperation Begin(SequenceStep step)
                {
                    if (owner.ThrowBegin) throw new InvalidOperationException("fixture begin failed");
                    return owner.Operation;
                }
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) { owner.Skips++; return new Operation(); }
                public void Dispose() { owner.Disposed = true; }
            }
        }
        // Acquire first, then throw: the owner scope must still return a lease
        // which was created before the collaborator reported failure.
        sealed class FailingControls : IControlRequests
        {
            readonly ControlSession controls;
            readonly string failure;
            public IControlScope Acquired;
            public FailingControls(ControlSession controls, string failure) { this.controls = controls; this.failure = failure; }
            public ControlState Current => controls.Current;
            public IControlScope OpenScope(string owner)
            {
                if (failure == "null") return null;
                if (failure == "open") throw new InvalidOperationException("fixture control open failed");
                Acquired = controls.OpenScope(owner); return new Scope(Acquired, failure);
            }
            sealed class Scope : IControlScope
            {
                readonly IControlScope scope; readonly string failure;
                public Scope(IControlScope scope, string failure) { this.scope = scope; this.failure = failure; }
                public bool IsActive => scope.IsActive;
                public IControlLease BlockInput(InputChannels channels)
                {
                    var lease = scope.BlockInput(channels);
                    if (failure == "block") throw new InvalidOperationException("fixture control block failed");
                    return lease;
                }
                public IControlLease PauseSimulation()
                {
                    var lease = scope.PauseSimulation();
                    if (failure == "pause") throw new InvalidOperationException("fixture control pause failed");
                    return lease;
                }
                public void Dispose() => scope.Dispose();
            }
        }
        static SequenceDefinition Definition() => new SequenceDefinition("fixture-boundaries",
            new[] { SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Wait("unstarted", 1) }, Array.Empty<string>(), new SequenceSkipDefinition("fixture-endpoint", 2));
        static SequenceControlPolicy Policy => new SequenceControlPolicy(InputChannels.Combat, true);

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
        public void InitialInvalidClockFailsBeforeOpeningAnyOwner(double time)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var clock = new Clock { Time = time };
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, clock))
                {
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed));
                    Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.ClockFailed));
                    Assert.That(bindings.Opens, Is.Zero); Assert.That(clock.Reads, Is.EqualTo(1));
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void InitialClockExceptionHasTheClockFailureReasonAndNoBindingScope()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var clock = new Clock { Throw = true };
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, clock))
                {
                    Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.ClockFailed));
                    Assert.That(runner.Read().Error, Does.Contain("fixture clock failed")); Assert.That(bindings.Opens, Is.Zero);
                }
            }
        }
        [Test] public void RejectedPreflightDoesNotReadTheClockOrAcquireOwners()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings { Valid = false }; var clock = new Clock { Throw = true };
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, clock))
                {
                    Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed));
                    Assert.That(clock.Reads, Is.Zero); Assert.That(bindings.Opens, Is.Zero);
                }
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void InitialStepBeginOrPollExceptionKeepsTheStepFailureReason(bool poll)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings { ThrowBegin = !poll }; bindings.Operation.ThrowPoll = poll;
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, new Clock()))
                {
                    Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.StepFailed));
                    Assert.That(bindings.Disposed, Is.True); Assert.That(controls.Current.SimulationPaused, Is.False);
                    if (poll) Assert.That(bindings.Operation.Disposed, Is.True);
                }
            }
        }
        [TestCase("open")] [TestCase("null")] [TestCase("block")] [TestCase("pause")]
        public void PartialControlAcquisitionFailureReturnsItsOwnersAndPreservesTheModal(string failure)
        {
            using (var controls = new ControlSession()) using (var modal = controls.OpenScope("fixture external modal"))
            {
                var block = modal.BlockInput(InputChannels.Navigation); var pause = modal.PauseSimulation();
                var bindings = new Bindings(); var requests = new FailingControls(controls, failure);
                using (var runner = new SequenceRunner(Definition(), bindings, requests, Policy, new Clock()))
                {
                    Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); Assert.That(bindings.Disposed, Is.True);
                    if (requests.Acquired != null) Assert.That(requests.Acquired.IsActive, Is.False);
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(controls.Current.SimulationPaused, Is.True);
                    Assert.That(block.IsActive && pause.IsActive, Is.True); Assert.That(bindings.Operation.Disposed, Is.False, "No step started.");
                }
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void SkipClockFailureReturnsCurrentOperationAndNeverStartsTheEndpoint(bool exception)
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var clock = new Clock();
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, clock))
                {
                    clock.Throw = exception; clock.Time = double.NaN;
                    Assert.That(runner.TrySkip(runner.Read().ExecutionId, out var error), Is.False); Assert.That(error, Is.Not.Empty);
                    Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.ClockFailed));
                    Assert.That(bindings.Operation.Disposed && bindings.Disposed, Is.True); Assert.That(bindings.Skips, Is.Zero);
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void CleanupFailureRetainsTheClockErrorAndReturnsOtherScopes()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings(); var clock = new Clock();
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, clock))
                {
                    bindings.Operation.ThrowDispose = true; clock.Throw = true; runner.Poll();
                    Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                    Assert.That(runner.Read().Error, Does.Contain("fixture clock failed").And.Contain("fixture operation cleanup failed"));
                    Assert.That(bindings.Disposed, Is.True); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void CleanupObserversSeeTheTerminalResultAndCannotRestartOrReplaceIt()
        {
            using (var controls = new ControlSession())
            {
                var bindings = new Bindings();
                using (var runner = new SequenceRunner(Definition(), bindings, controls, Policy, new Clock()))
                {
                    bool observed = false;
                    bindings.Operation.OnDispose = () => {
                        observed = true; Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
                        Assert.That(runner.TrySkip(runner.Read().ExecutionId, out _), Is.False);
                        Assert.That(runner.TryCancel(runner.Read().ExecutionId, out _), Is.False);
                    };
                    Assert.That(runner.TryCancel(runner.Read().ExecutionId, out _), Is.True); Assert.That(observed, Is.True);
                    runner.Dispose(); runner.Poll(); Assert.That(runner.Read().EndReason, Is.EqualTo(SequenceEndReason.Requested));
                }
            }
        }
    }
}
