using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;

namespace Deviant.Presentation
{
    // One presentation thread, one retained screen channel per owner. Playback
    // time is supplied per handle; Resolve never advances a peer's animation.
    public sealed class FadeDirector : IFadeRequests, IDisposable
    {
        readonly List<Scope> scopes = new List<Scope>();
        long sequence;
        bool disposed;
        public Guid SessionId { get; private set; } = Guid.NewGuid();
        public bool IsAvailable => !disposed;
        public int RequestCount => scopes.Count(s => s.current != null && s.current.IsRetained);
        public FadeSelection LastSelection { get; private set; }
        public IFadeScope OpenScope(string owner)
        {
            Check(); if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A diagnostic owner is required.", nameof(owner));
            var scope = new Scope(this); scopes.Add(scope); return scope;
        }
        public FadeSelection Resolve(FadeFrame baseline)
        {
            Check();
            var selected = scopes.Select(s => s.current).Where(h => h != null && h.IsRetained)
                .OrderByDescending(h => h.Priority).ThenByDescending(h => h.Sequence).FirstOrDefault();
            return LastSelection = selected == null ? new FadeSelection(baseline, SessionId, null) : new FadeSelection(selected.Frame, SessionId, selected.Sequence);
        }
        public void Reset()
        {
            Check(); foreach (var scope in scopes.ToArray()) scope.Close();
            SessionId = Guid.NewGuid(); sequence = 0; LastSelection = new FadeSelection(default, SessionId, null);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var scope in scopes.ToArray()) scope.Close();
        }
        void Check() { if (disposed) throw new ObjectDisposedException(nameof(FadeDirector)); }
        sealed class Scope : IFadeScope
        {
            readonly FadeDirector director;
            readonly Guid session;
            bool closed;
            internal Handle current;
            public bool IsActive => !closed && !director.disposed && session == director.SessionId;
            public Scope(FadeDirector director) { this.director = director; session = director.SessionId; }
            public IFadeHandle Play(FadeDefinition definition, int priority)
            {
                director.Check(); if (!IsActive) throw new ObjectDisposedException(nameof(IFadeScope));
                if (definition == null) throw new ArgumentNullException(nameof(definition));
                if (director.sequence == long.MaxValue) throw new InvalidOperationException("Fade request sequence exhausted.");
                // Validate/prepare the replacement before returning the old
                // channel. Invalid input must preserve the current retained frame.
                var next = new Handle(this, director, session, director.sequence + 1, priority, definition);
                current?.Stop(); director.sequence++; current = next; return next;
            }
            internal void Release(Handle handle) { if (ReferenceEquals(current, handle)) current = null; }
            public void Dispose() { if (closed) return; director.Check(); Close(); }
            internal void Close()
            {
                if (closed) return; closed = true;
                current?.Stop(); current = null; director.scopes.Remove(this);
            }
        }
        sealed class Handle : IFadeHandle
        {
            readonly Scope scope;
            readonly FadeDirector director;
            readonly FadeDefinition definition;
            double lastElapsed;
            bool retained = true;
            public Guid SessionId { get; }
            public long Sequence { get; }
            public int Priority { get; }
            public string DefinitionVersion => definition.ContentHash;
            public FadeStatus Status { get; private set; } = FadeStatus.Playing;
            public string Error { get; private set; }
            public FadeFrame Frame { get; private set; }
            public bool IsRetained => retained && scope.IsActive;
            public Handle(Scope scope, FadeDirector director, Guid session, long sequence, int priority, FadeDefinition definition)
            {
                this.scope = scope; this.director = director; this.definition = definition;
                SessionId = session; Sequence = sequence; Priority = priority;
                Frame = new FadeFrame(definition.Red, definition.Green, definition.Blue, definition.FromOpacity);
            }
            public void Poll(double elapsed)
            {
                if (Status != FadeStatus.Playing) return;
                if (!scope.IsActive) { Stop(); return; }
                if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0 || elapsed < lastElapsed)
                {
                    Error = "Fade elapsed time must be finite, nonnegative and monotonic.";
                    Status = FadeStatus.Failed; Return(); return;
                }
                lastElapsed = elapsed;
                Frame = new FadeFrame(definition.Red, definition.Green, definition.Blue, definition.OpacityAt(elapsed));
                if (elapsed < definition.Duration) return;
                Status = FadeStatus.Completed;
                if (!definition.HoldAtEnd) Return();
            }
            void Return() { retained = false; scope.Release(this); }
            internal void Stop()
            {
                if (Status == FadeStatus.Playing) Status = FadeStatus.Cancelled;
                Return();
            }
            public void Cancel()
            {
                if (Status != FadeStatus.Playing && !IsRetained) return;
                director.Check(); Stop();
            }
            public void Dispose() => Cancel();
        }
    }
}
