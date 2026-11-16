using System;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Sequences.Contracts;

namespace Deviant.Sequences
{
    public sealed class SequenceControlPolicy
    {
        public InputChannels BlockedInput { get; }
        public bool PauseSimulation { get; }
        public SequenceControlPolicy(InputChannels blockedInput, bool pauseSimulation)
        {
            if ((blockedInput & ~(InputChannels.Combat | InputChannels.Interaction | InputChannels.Dialogue | InputChannels.Navigation)) != 0)
                throw new ArgumentOutOfRangeException(nameof(blockedInput));
            BlockedInput = blockedInput; PauseSimulation = pauseSimulation;
        }
    }

    // One execution, one immutable definition, one owner lifetime. Polling never
    // restarts a failed step or applies the remaining steps as skip side effects.
    public sealed class SequenceRunner : ISequenceCommands
    {
        readonly Guid execution = Guid.NewGuid();
        readonly SequenceDefinition definition;
        readonly ISequenceClock clock;
        ISequenceBindingScope bindings;
        IControlScope control;
        ISequenceOperation operation;
        SequenceStepExecution currentStep;
        SequenceSnapshot snapshot;
        int index;
        double lastTime, startedAt;
        bool busy, skipping;
        public SequenceRunner(SequenceDefinition definition, ISequenceBindings bindings, IControlRequests controls,
            SequenceControlPolicy policy, ISequenceClock clock)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            Publish(SequenceStatus.Running, SequenceEndReason.None, null); busy = true;
            var failureReason = SequenceEndReason.BindingFailed;
            try
            {
                if (!bindings.Validate(definition, out var error)) { Finish(SequenceStatus.Failed, SequenceEndReason.BindingFailed, error ?? "Sequence bindings are unavailable."); return; }
                failureReason = SequenceEndReason.ClockFailed; lastTime = ReadTime();
                failureReason = SequenceEndReason.BindingFailed;
                this.bindings = bindings.OpenScope(execution, definition) ?? throw new InvalidOperationException("Sequence binding scope is missing.");
                control = controls.OpenScope("sequence " + execution) ?? throw new InvalidOperationException("Sequence control scope is missing.");
                if (policy.BlockedInput != InputChannels.None) control.BlockInput(policy.BlockedInput);
                if (policy.PauseSimulation) control.PauseSimulation();
                if (!this.bindings.IsActive || !control.IsActive) throw new InvalidOperationException("Sequence owner scope is no longer active.");
                failureReason = SequenceEndReason.StepFailed;
                StartStep(lastTime);
                Advance(lastTime);
            }
            catch (Exception exception) { Finish(SequenceStatus.Failed, failureReason, exception.Message); }
            finally { busy = false; }
        }
        public SequenceSnapshot Read() => snapshot;
        public void Poll()
        {
            if (busy) throw new InvalidOperationException("Sequence mutations cannot reenter binding callbacks.");
            if (snapshot.Status != SequenceStatus.Running) return;
            busy = true;
            try
            {
                double now;
                try { now = ReadTime(); }
                catch (Exception exception) { Finish(SequenceStatus.Failed, SequenceEndReason.ClockFailed, exception.Message); return; }
                if (!bindings.IsActive || !control.IsActive) { Finish(SequenceStatus.Failed, SequenceEndReason.BindingFailed, "Sequence owner scope is no longer active."); return; }
                Advance(now);
            }
            catch (Exception exception) { Finish(SequenceStatus.Failed, SequenceEndReason.StepFailed, exception.Message); }
            finally { busy = false; }
        }
        public bool TryCancel(Guid expectedExecution, out string error)
        {
            if (!Accept(expectedExecution, out error)) return false;
            busy = true;
            try { Finish(SequenceStatus.Cancelled, SequenceEndReason.Requested, null); return true; }
            finally { busy = false; }
        }
        public bool TrySkip(Guid expectedExecution, out string error)
        {
            if (!Accept(expectedExecution, out error)) return false;
            if (definition.Skip == null || skipping) { error = "Sequence has no skip policy or a skip is already in progress."; return false; }
            busy = true;
            try
            {
                // Cleanup must succeed before asking a service for an endpoint.
                var cleanup = ReleaseOperation(SequenceEndReason.Skipped);
                if (cleanup != null) { Finish(SequenceStatus.Failed, SequenceEndReason.CleanupFailed, cleanup); error = snapshot.Error; return false; }
                skipping = true; Publish(SequenceStatus.Running, SequenceEndReason.None, null);
                double now;
                try { now = ReadTime(); }
                catch (Exception exception) { Finish(SequenceStatus.Failed, SequenceEndReason.ClockFailed, exception.Message); error = snapshot.Error; return false; }
                startedAt = now;
                if (!bindings.IsActive || !control.IsActive) throw new InvalidOperationException("Sequence owner scope is no longer active.");
                operation = bindings.BeginSkip(definition.Skip) ?? throw new InvalidOperationException("Skip endpoint returned no operation.");
                Advance(now); error = snapshot.Error; return snapshot.Status != SequenceStatus.Failed;
            }
            catch (Exception exception) { Finish(SequenceStatus.Failed, SequenceEndReason.StepFailed, exception.Message); error = snapshot.Error; return false; }
            finally { busy = false; }
        }
        bool Accept(Guid expectedExecution, out string error)
        {
            error = null;
            if (busy || snapshot.Status != SequenceStatus.Running || expectedExecution != execution)
            { error = "Stale, ended or reentrant sequence input."; return false; }
            return true;
        }
        double ReadTime()
        {
            double now = clock.ReadTime();
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || now < lastTime) throw new InvalidOperationException("Sequence clock must return finite nonnegative monotonic time.");
            lastTime = now; return now;
        }
        void StartStep(double now)
        {
            startedAt = now;
            currentStep = new SequenceStepExecution(definition.Steps[index]); operation = currentStep;
            Publish(SequenceStatus.Running, SequenceEndReason.None, null);
            currentStep.Start(bindings);
        }
        void Advance(double now)
        {
            while (snapshot.Status == SequenceStatus.Running)
            {
                double elapsed = now - startedAt;
                // Normal leaf deadlines are owned by their step execution; a
                // parallel peer cannot reset or extend an unfinished deadline.
                if (skipping && elapsed >= definition.Skip.Timeout) { Finish(SequenceStatus.Failed, SequenceEndReason.TimedOut, "Sequence skip timed out."); return; }
                operation.Poll(elapsed); var status = operation.Status;
                if (status == SequenceOperationStatus.Running) { Publish(SequenceStatus.Running, SequenceEndReason.None, null); return; }
                if (status == SequenceOperationStatus.Cancelled) { Finish(SequenceStatus.Cancelled, SequenceEndReason.StepCancelled, operation.Error); return; }
                if (status != SequenceOperationStatus.Completed)
                { Finish(SequenceStatus.Failed, skipping ? SequenceEndReason.StepFailed : currentStep.EndReason, operation.Error ?? "Sequence operation returned an invalid or failed result."); return; }
                var cleanup = ReleaseOperation();
                if (cleanup != null) { Finish(SequenceStatus.Failed, SequenceEndReason.CleanupFailed, cleanup); return; }
                if (skipping) { Finish(SequenceStatus.Skipped, SequenceEndReason.Skipped, null); return; }
                if (index + 1 == definition.Steps.Count) { Finish(SequenceStatus.Completed, SequenceEndReason.Completed, null); return; }
                index++; StartStep(now);
                // Time spent awaiting earlier steps is never donated to a newly
                // started dialogue/camera/Cue. Each starts at this observation.
            }
        }
        string ReleaseOperation(SequenceEndReason reason = SequenceEndReason.OwnerDisposed)
        {
            var previous = operation; operation = null;
            try
            {
                if (previous is SequenceStepExecution step) step.Stop(reason);
                else previous?.Dispose();
                return null;
            }
            catch (Exception exception) { return "Sequence operation cleanup failed: " + exception.Message; }
        }
        void Finish(SequenceStatus status, SequenceEndReason reason, string error)
        {
            if (snapshot.Status != SequenceStatus.Running) return;
            // Publish before cleanup callbacks so an observer cannot see an
            // active execution or reenter termination while resources return.
            Publish(status, reason, error);
            string cleanup = ReleaseOperation(reason);
            var previousBindings = bindings; bindings = null;
            try { previousBindings?.Dispose(); }
            catch (Exception exception) { cleanup = Join(cleanup, "Sequence bindings cleanup failed: " + exception.Message); }
            var previousControl = control; control = null;
            try { previousControl?.Dispose(); }
            catch (Exception exception) { cleanup = Join(cleanup, "Sequence control cleanup failed: " + exception.Message); }
            if (cleanup != null) Publish(SequenceStatus.Failed, SequenceEndReason.CleanupFailed, Join(error, cleanup));
            else Publish(status, reason, error);
        }
        void Publish(SequenceStatus status, SequenceEndReason reason, string error)
            => snapshot = new SequenceSnapshot(execution, definition.ContentHash, index, definition.Steps[index].Id, skipping, status, reason, error, currentStep?.ReadChildren());
        static string Join(string first, string second) => first == null ? second : first + "; " + second;
        public void Dispose()
        {
            if (busy) throw new InvalidOperationException("Sequence mutations cannot reenter binding callbacks.");
            if (snapshot.Status != SequenceStatus.Running) return;
            busy = true; try { Finish(SequenceStatus.Cancelled, SequenceEndReason.OwnerDisposed, null); } finally { busy = false; }
        }
    }
}
