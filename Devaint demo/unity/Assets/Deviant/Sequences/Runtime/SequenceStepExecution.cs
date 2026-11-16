using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Sequences.Contracts;

namespace Deviant.Sequences
{
    // A runner-owned leaf or flat wait-all group. Starts/polls in declaration
    // order on one thread; all children share the group's elapsed time.
    internal sealed class SequenceStepExecution : ISequenceOperation
    {
        readonly SequenceStep definition;
        readonly SequenceStepExecution[] children;
        ISequenceOperation operation;
        bool disposed;
        SequenceStepStatus state = SequenceStepStatus.Pending;
        public SequenceEndReason EndReason { get; private set; }
        public string Error { get; private set; }
        public SequenceOperationStatus Status => state == SequenceStepStatus.Completed ? SequenceOperationStatus.Completed :
            state == SequenceStepStatus.Cancelled ? SequenceOperationStatus.Cancelled : state == SequenceStepStatus.Failed ? SequenceOperationStatus.Failed : SequenceOperationStatus.Running;
        public SequenceStepExecution(SequenceStep definition)
        {
            this.definition = definition;
            children = definition.Children.Select(c => new SequenceStepExecution(c)).ToArray();
        }
        public void Start(ISequenceBindingScope bindings)
        {
            if (state != SequenceStepStatus.Pending || disposed) throw new InvalidOperationException("Sequence step already started or disposed.");
            state = SequenceStepStatus.Running;
            if (definition.Kind == SequenceStepKind.ParallelAll)
            {
                foreach (var child in children)
                {
                    child.Start(bindings);
                    if (Propagate(child)) return;
                }
                CompleteGroup(); return;
            }
            if (definition.Kind == SequenceStepKind.Wait) return;
            try
            {
                operation = bindings.Begin(definition) ?? throw new InvalidOperationException("Sequence binding returned no operation.");
                Observe();
            }
            catch (Exception exception) { Fail(SequenceEndReason.StepFailed, exception.Message); }
        }
        public void Poll(double elapsed)
        {
            if (state != SequenceStepStatus.Running || disposed) return;
            if (definition.Kind == SequenceStepKind.ParallelAll)
            {
                foreach (var child in children)
                {
                    child.Poll(elapsed);
                    if (Propagate(child)) return;
                }
                CompleteGroup(); return;
            }
            if (definition.Timeout.HasValue && elapsed >= definition.Timeout.Value)
            { Fail(SequenceEndReason.TimedOut, "Sequence step timed out: " + definition.Id); return; }
            if (definition.Kind == SequenceStepKind.Wait)
            {
                if (elapsed >= definition.Duration) { state = SequenceStepStatus.Completed; EndReason = SequenceEndReason.Completed; }
                return;
            }
            try { operation.Poll(elapsed); Observe(); }
            catch (Exception exception) { Fail(SequenceEndReason.StepFailed, exception.Message); }
        }
        void Observe()
        {
            var result = operation.Status;
            if (result == SequenceOperationStatus.Running) return;
            if (result == SequenceOperationStatus.Completed)
            {
                state = SequenceStepStatus.Completed; EndReason = SequenceEndReason.Completed;
                string cleanup = Release();
                if (cleanup != null) Fail(SequenceEndReason.CleanupFailed, cleanup);
            }
            else if (result == SequenceOperationStatus.Cancelled)
            { state = SequenceStepStatus.Cancelled; EndReason = SequenceEndReason.StepCancelled; Error = operation.Error; }
            else Fail(SequenceEndReason.StepFailed, operation.Error ?? "Sequence operation returned an invalid or failed result.");
        }
        bool Propagate(SequenceStepExecution child)
        {
            if (child.state != SequenceStepStatus.Failed && child.state != SequenceStepStatus.Cancelled) return false;
            state = child.state; EndReason = child.EndReason;
            Error = child.definition.Id + ": " + (child.Error ?? child.EndReason.ToString()); return true;
        }
        void CompleteGroup()
        {
            if (children.All(c => c.state == SequenceStepStatus.Completed))
            { state = SequenceStepStatus.Completed; EndReason = SequenceEndReason.Completed; }
        }
        void Fail(SequenceEndReason reason, string error)
        { state = SequenceStepStatus.Failed; EndReason = reason; Error = error; }
        string Release()
        {
            var previous = operation; operation = null;
            try { previous?.Dispose(); return null; }
            catch (Exception exception) { return "Sequence step cleanup failed: " + definition.Id + ": " + exception.Message; }
        }
        public SequenceStepSnapshot[] ReadChildren() => children.Select(c => new SequenceStepSnapshot(c.definition.Id, c.state, c.EndReason, c.Error)).ToArray();
        public void Stop(SequenceEndReason reason)
        {
            if (disposed) return; disposed = true;
            if (state == SequenceStepStatus.Running) { state = SequenceStepStatus.Cancelled; EndReason = reason; }
            var errors = new List<Exception>();
            foreach (var child in children)
            {
                try { child.Stop(reason); }
                catch (Exception exception) { errors.Add(exception); }
            }
            string cleanup = Release();
            if (cleanup != null) errors.Add(new InvalidOperationException(cleanup));
            if (errors.Count == 0) return;
            string detail = string.Join("; ", errors.Select(e => e.Message));
            Fail(SequenceEndReason.CleanupFailed, Error == null ? detail : Error + "; " + detail);
            throw new AggregateException("Sequence step cleanup failed: " + definition.Id, errors);
        }
        public void Dispose() => Stop(SequenceEndReason.OwnerDisposed);
    }
}
