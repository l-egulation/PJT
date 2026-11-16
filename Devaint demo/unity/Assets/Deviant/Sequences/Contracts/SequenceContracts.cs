using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.Sequences.Contracts
{
    public enum SequenceStatus { Running, Completed, Skipped, Cancelled, Failed }
    public enum SequenceEndReason { None, Completed, Skipped, Requested, OwnerDisposed, StepCancelled, BindingFailed, StepFailed, TimedOut, ClockFailed, CleanupFailed }
    public enum SequenceOperationStatus { Running, Completed, Cancelled, Failed }
    public enum SequenceStepStatus { Pending, Running, Completed, Cancelled, Failed }
    public sealed class SequenceStepSnapshot
    {
        public string Id { get; }
        public SequenceStepStatus Status { get; }
        public SequenceEndReason EndReason { get; }
        public string Error { get; }
        public SequenceStepSnapshot(string id, SequenceStepStatus status, SequenceEndReason reason, string error)
        { Id = id; Status = status; EndReason = reason; Error = error; }
    }
    public sealed class SequenceSnapshot
    {
        public Guid ExecutionId { get; }
        public string DefinitionVersion { get; }
        public int StepIndex { get; }
        public string StepId { get; }
        public bool IsSkipping { get; }
        public SequenceStatus Status { get; }
        public SequenceEndReason EndReason { get; }
        public string Error { get; }
        public IReadOnlyList<SequenceStepSnapshot> Children { get; }
        public SequenceSnapshot(Guid executionId, string version, int stepIndex, string stepId, bool isSkipping,
            SequenceStatus status, SequenceEndReason endReason, string error, SequenceStepSnapshot[] children = null)
        {
            ExecutionId = executionId; DefinitionVersion = version; StepIndex = stepIndex; StepId = stepId; IsSkipping = isSkipping; Status = status; EndReason = endReason; Error = error;
            Children = Array.AsReadOnly(children == null ? Array.Empty<SequenceStepSnapshot>() : (SequenceStepSnapshot[])children.Clone());
        }
    }
    // Absolute finite monotonic time. Its source explicitly selects simulation,
    // presentation or unscaled time; the runner never changes an engine clock.
    public interface ISequenceClock { double ReadTime(); }
    public interface ISequenceCommands : IDisposable
    {
        SequenceSnapshot Read();
        void Poll();
        bool TryCancel(Guid expectedExecution, out string error);
        bool TrySkip(Guid expectedExecution, out string error);
    }
    public interface ISequenceOperation : IDisposable
    {
        SequenceOperationStatus Status { get; }
        string Error { get; }
        // Elapsed time belongs to this step and starts at zero. Completion is
        // observed by polling; a disposed operation must never revive later.
        void Poll(double elapsed);
    }
    public interface ISequenceBindingScope : IDisposable
    {
        bool IsActive { get; }
        // Begin receives only a leaf. ParallelAll and Wait are runner-owned;
        // independent leaf handles may remain active at the same time.
        ISequenceOperation Begin(SequenceStep step);
        ISequenceOperation BeginSkip(SequenceSkipDefinition skip);
    }
    public interface ISequenceBindings
    {
        // Read-only preflight of all required anchors, bindings and skip endpoint
        // before acquiring control or starting any step. Begin must recheck life.
        bool Validate(SequenceDefinition definition, out string error);
        // Capture the used binding versions for this definition at execution
        // start. A throwing OpenScope/Begin releases partially acquired state.
        ISequenceBindingScope OpenScope(Guid executionId, SequenceDefinition definition);
    }
    public interface ISequenceSkipEndpoint
    {
        bool Validate(string endpointId, out string error);
        // Request only the declared endpoint. The service must deduplicate any
        // delayed domain work using executionId; skipped steps are never replayed.
        ISequenceOperation Begin(Guid executionId, string endpointId);
    }
}
