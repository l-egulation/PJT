using System;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;

namespace Deviant.Presentation
{
    // One exclusive owner per presentation view. This session owns pose values,
    // never a gameplay actor, input, world state or simulation tick.
    public sealed class StageActorSession : IStageActorRequests, IDisposable
    {
        Scope current;
        bool disposed;
        public Guid SessionId { get; private set; } = Guid.NewGuid();
        public bool IsAvailable => !disposed;
        public bool IsOwned => current != null && current.IsActive;
        public IStageActorScope OpenScope(string owner)
        {
            Check();
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A diagnostic stage owner is required.", nameof(owner));
            if (IsOwned) throw new InvalidOperationException("Stage actor view already has an owner.");
            return current = new Scope(this, SessionId);
        }
        public StagePose Resolve(StagePose baseline)
        {
            Check(); if (baseline.Facing != -1 && baseline.Facing != 1) throw new ArgumentException("A valid stage baseline is required.", nameof(baseline));
            return current?.handle != null && current.handle.IsRetained ? current.handle.Pose : baseline;
        }
        public void Reset()
        { Check(); current?.Close(); current = null; SessionId = Guid.NewGuid(); }
        public void Dispose()
        { if (disposed) return; disposed = true; current?.Close(); current = null; }
        void Check() { if (disposed) throw new ObjectDisposedException(nameof(StageActorSession)); }
        sealed class Scope : IStageActorScope
        {
            readonly StageActorSession session;
            readonly Guid id;
            bool closed;
            internal Handle handle;
            public bool IsActive => !closed && session.IsAvailable && id == session.SessionId;
            public Scope(StageActorSession session, Guid id) { this.session = session; this.id = id; }
            public IStageActorHandle Play(StageMoveDefinition definition, StagePoint from, StagePoint to)
            {
                session.Check(); if (!IsActive) throw new ObjectDisposedException(nameof(IStageActorScope));
                if (definition == null) throw new ArgumentNullException(nameof(definition));
                var next = new Handle(this, id, definition, from, to);
                handle?.Stop(); handle = next; return next;
            }
            public void Dispose() { if (!closed) Close(); }
            internal void Close()
            {
                if (closed) return; closed = true; handle?.Stop(); handle = null;
                if (ReferenceEquals(session.current, this)) session.current = null;
            }
        }
        sealed class Handle : IStageActorHandle
        {
            readonly Scope scope;
            readonly StageMoveDefinition definition;
            readonly StagePoint from, to;
            double lastElapsed;
            bool retained = true;
            public Guid SessionId { get; }
            public string DefinitionVersion => definition.ContentHash;
            public StageMoveStatus Status { get; private set; } = StageMoveStatus.Playing;
            public string Error { get; private set; }
            public StagePose Pose { get; private set; }
            public bool IsRetained => retained && scope.IsActive;
            public Handle(Scope scope, Guid session, StageMoveDefinition definition, StagePoint from, StagePoint to)
            { this.scope = scope; SessionId = session; this.definition = definition; this.from = from; this.to = to; Pose = At(0); }
            public void Poll(double elapsed)
            {
                if (Status != StageMoveStatus.Playing) return;
                if (!scope.IsActive) { Stop(); return; }
                if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0 || elapsed < lastElapsed)
                { Status = StageMoveStatus.Failed; Error = "Stage elapsed time must be finite, nonnegative and monotonic."; retained = false; return; }
                lastElapsed = elapsed; Pose = At(definition.ProgressAt(elapsed));
                if (elapsed >= definition.Duration) Status = StageMoveStatus.Completed;
            }
            StagePose At(double progress)
            {
                // Use double subtraction to avoid overflow between finite but
                // opposite extreme float coordinates.
                var point = new StagePoint((float)(from.X + ((double)to.X - from.X) * progress), (float)(from.Y + ((double)to.Y - from.Y) * progress));
                return new StagePose(point, definition.Depth, definition.Facing, true);
            }
            internal void Stop() { retained = false; if (Status == StageMoveStatus.Playing) Status = StageMoveStatus.Cancelled; }
            public void Cancel() => Stop();
            public void Dispose() => Stop();
        }
    }
}
