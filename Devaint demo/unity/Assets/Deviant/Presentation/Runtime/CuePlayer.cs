using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;

namespace Deviant.Presentation
{
    // Single presentation-thread lifetime. The supplied clock authorizes only
    // media sampling, never combat. History belongs to a disposable owner scope.
    public sealed class CuePlayer : ICuePlayer, IDisposable
    {
        readonly ICueViewFactory factory;
        readonly List<Scope> scopes = new List<Scope>();
        bool disposed, busy;
        public Guid SessionId { get; private set; } = Guid.NewGuid();
        public int ActiveCount => scopes.Sum(s => s.handles.Values.Count(h => !h.Terminal));
        public CuePlayer(ICueViewFactory factory) { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); }
        public ICueScope OpenScope(string owner, ICueViewFactory factory = null)
        {
            Check(); if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A diagnostic owner is required.", nameof(owner));
            var scope = new Scope(this, factory ?? this.factory); scopes.Add(scope); return scope;
        }
        public void Tick()
        {
            Check(); busy = true;
            try { foreach (var scope in scopes.ToArray()) foreach (var handle in scope.handles.Values.ToArray()) handle.Sample(); }
            finally { busy = false; }
        }
        public void Reset()
        {
            Check(); busy = true;
            try { foreach (var scope in scopes.ToArray()) scope.Close(); SessionId = Guid.NewGuid(); }
            finally { busy = false; }
        }
        public void Dispose()
        {
            if (disposed) return; Check(); busy = true; disposed = true;
            try { foreach (var scope in scopes.ToArray()) scope.Close(); }
            finally { busy = false; }
        }
        void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CuePlayer));
            if (busy) throw new InvalidOperationException("Cue mutations cannot reenter media callbacks.");
        }
        sealed class Scope : ICueScope
        {
            readonly CuePlayer player;
            readonly ICueViewFactory factory;
            readonly Guid session;
            bool closed;
            internal readonly Dictionary<CueOccurrence, Handle> handles = new Dictionary<CueOccurrence, Handle>();
            public bool IsActive => !closed && !player.disposed && session == player.SessionId;
            public Scope(CuePlayer player, ICueViewFactory factory) { this.player = player; this.factory = factory; session = player.SessionId; }
            public ICueHandle Play(ICueDefinition definition, CueOccurrence occurrence, CueAnchor anchor, ICueClock clock)
            {
                player.Check(); if (!IsActive) throw new ObjectDisposedException(nameof(ICueScope));
                if (definition == null || clock == null || occurrence.ExecutionId == Guid.Empty || occurrence.CueId != definition.Id || occurrence.Ordinal < 0 || anchor.Facing != -1 && anchor.Facing != 1)
                    throw new ArgumentException("Definition, clock, occurrence identity and anchor are required.");
                if (handles.TryGetValue(occurrence, out var previous)) return previous; // Starting version wins, including terminal occurrences.
                var handle = new Handle(this, session, definition, occurrence, clock); handles.Add(occurrence, handle);
                player.busy = true;
                try
                {
                    try { handle.SetTicket(factory.Begin(definition, anchor, handle.Resolve)); handle.Sample(); }
                    catch (Exception exception) { handle.Fail("Cue preparation failed: " + exception.Message); }
                    return handle;
                }
                finally { player.busy = false; }
            }
            public void Dispose()
            {
                if (closed) return; player.Check(); player.busy = true;
                try { Close(); } finally { player.busy = false; }
            }
            internal void Close()
            {
                if (closed) return; closed = true;
                foreach (var handle in handles.Values) handle.End(CueStatus.Cancelled);
                handles.Clear(); player.scopes.Remove(this);
            }
            internal void Receive(Handle handle, CueViewResult result)
            {
                bool previous = player.busy; player.busy = true;
                try { handle.Accept(result); } finally { player.busy = previous; }
            }
            internal void Poll(Handle handle)
            {
                if (handle.Terminal) return; player.Check(); player.busy = true;
                try { handle.Sample(); } finally { player.busy = false; }
            }
            internal void Cancel(Handle handle)
            {
                if (handle.Terminal) return; player.Check(); player.busy = true;
                try { handle.End(CueStatus.Cancelled); } finally { player.busy = false; }
            }
        }
        sealed class Handle : ICueHandle
        {
            readonly Scope scope;
            readonly ICueDefinition definition;
            readonly ICueClock clock;
            IDisposable ticket;
            ICueView view, accepted;
            bool resolved;
            public Guid SessionId { get; }
            public CueOccurrence Occurrence { get; }
            public string DefinitionVersion => definition.ContentHash;
            public CueStatus Status { get; private set; } = CueStatus.Pending;
            public string Error { get; private set; }
            internal bool Terminal => Status == CueStatus.Completed || Status == CueStatus.Cancelled || Status == CueStatus.Failed;
            public Handle(Scope scope, Guid session, ICueDefinition definition, CueOccurrence occurrence, ICueClock clock)
            { this.scope = scope; SessionId = session; this.definition = definition; Occurrence = occurrence; this.clock = clock; }
            internal void SetTicket(IDisposable value)
            {
                if (Terminal) { Release(value); return; }
                ticket = value;
                if (!resolved && value == null) Fail("Pending cue preparation returned no cancellation ticket.");
            }
            internal void Resolve(CueViewResult result) => scope.Receive(this, result);
            internal void Accept(CueViewResult result)
            {
                if (result?.View != null && ReferenceEquals(result.View, accepted)) return;
                if (resolved || Terminal || !scope.IsActive)
                { if (result?.View != null) Release(result.View); return; }
                resolved = true;
                if (result == null || result.View == null) { Fail(result?.Error ?? "Cue preparation returned no result."); return; }
                view = accepted = result.View;
            }
            internal void Sample()
            {
                if (Terminal) return;
                if (!scope.IsActive) { End(CueStatus.Cancelled); return; }
                try
                {
                    float elapsed = clock.ReadElapsed();
                    if (float.IsNaN(elapsed) || float.IsInfinity(elapsed)) throw new InvalidOperationException("Cue clock must return finite time.");
                    if (!definition.Loop && elapsed >= definition.Duration)
                    {
                        if (Status == CueStatus.Pending) Fail("Cue media was not shown before its playback interval ended.");
                        else End(CueStatus.Completed);
                        return;
                    }
                    if (view == null) return;
                    view.Sample(elapsed); Status = CueStatus.Playing;
                }
                catch (Exception exception) { Fail("Cue sampling failed: " + exception.Message); }
            }
            internal void Fail(string error) { if (Terminal) return; Error = error; End(CueStatus.Failed); }
            internal void End(CueStatus status)
            {
                if (Terminal) return; Status = status;
                var oldView = view; var oldTicket = ticket; view = null; ticket = null;
                Release(oldView); Release(oldTicket);
                if (Error != null && Status == CueStatus.Completed) Status = CueStatus.Failed;
            }
            void Release(IDisposable value)
            {
                if (value == null) return;
                try { value.Dispose(); }
                catch (Exception exception) { Error = (Error == null ? "" : Error + "; ") + "Cue cleanup failed: " + exception.Message; }
            }
            public void Poll() => scope.Poll(this);
            public void Cancel() => scope.Cancel(this);
            public void Dispose() => Cancel();
        }
    }
}
