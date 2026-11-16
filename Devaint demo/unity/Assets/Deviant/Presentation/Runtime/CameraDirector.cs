using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Presentation.Contracts;

namespace Deviant.Presentation
{
    // Resolves framing only. Lens, shake, blend duration and content priorities
    // are separate policies. Equal priority uses the latest request sequence.
    public sealed class CameraDirector : ICameraRequests, IDisposable
    {
        readonly List<Scope> scopes = new List<Scope>();
        long sequence;
        bool disposed, busy;
        public Guid SessionId { get; private set; } = Guid.NewGuid();
        public int ActiveCount => scopes.Sum(s => s.handles.Count(h => h.Status == CameraRequestStatus.Active));
        public CameraSelection LastSelection { get; private set; }
        public ICameraScope OpenScope(string owner)
        {
            Check(); if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A diagnostic owner is required.", nameof(owner));
            var scope = new Scope(this); scopes.Add(scope); return scope;
        }
        public CameraSelection Resolve(CameraPosition baseline)
        {
            Check(); busy = true;
            try
            {
                foreach (var request in scopes.SelectMany(s => s.handles).Where(h => h.Status == CameraRequestStatus.Active)
                    .OrderByDescending(h => h.Priority).ThenByDescending(h => h.Sequence).ToArray())
                {
                    try { return LastSelection = new CameraSelection(request.source.Read(), SessionId, request.Sequence); }
                    catch (Exception exception) { request.Fail("Camera source failed: " + exception.Message); }
                }
                return LastSelection = new CameraSelection(baseline, SessionId, null);
            }
            finally { busy = false; }
        }
        public void Reset()
        {
            Check(); foreach (var scope in scopes.ToArray()) scope.Close();
            SessionId = Guid.NewGuid(); sequence = 0; LastSelection = new CameraSelection(default, SessionId, null);
        }
        public void Dispose()
        {
            if (disposed) return; Check(); disposed = true;
            foreach (var scope in scopes.ToArray()) scope.Close();
        }
        void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CameraDirector));
            if (busy) throw new InvalidOperationException("Camera requests cannot reenter source resolution.");
        }
        sealed class Scope : ICameraScope
        {
            readonly CameraDirector director;
            readonly Guid session;
            bool closed;
            internal readonly List<Handle> handles = new List<Handle>();
            public bool IsActive => !closed && !director.disposed && session == director.SessionId;
            public Scope(CameraDirector director) { this.director = director; session = director.SessionId; }
            public ICameraHandle Request(int priority, ICameraPositionSource source)
            {
                director.Check(); if (!IsActive) throw new ObjectDisposedException(nameof(ICameraScope));
                if (source == null) throw new ArgumentNullException(nameof(source));
                if (director.sequence == long.MaxValue) throw new InvalidOperationException("Camera request sequence exhausted.");
                var handle = new Handle(director, session, ++director.sequence, priority, source); handles.Add(handle); return handle;
            }
            public void Dispose() { if (closed) return; director.Check(); Close(); }
            internal void Close()
            {
                if (closed) return; closed = true;
                foreach (var handle in handles) handle.End(CameraRequestStatus.Cancelled);
                handles.Clear(); director.scopes.Remove(this);
            }
        }
        sealed class Handle : ICameraHandle
        {
            readonly CameraDirector director;
            internal readonly ICameraPositionSource source;
            public Guid SessionId { get; }
            public long Sequence { get; }
            public int Priority { get; }
            public CameraRequestStatus Status { get; private set; } = CameraRequestStatus.Active;
            public string Error { get; private set; }
            public Handle(CameraDirector director, Guid session, long sequence, int priority, ICameraPositionSource source)
            { this.director = director; SessionId = session; Sequence = sequence; Priority = priority; this.source = source; }
            internal void Fail(string error) { if (Status != CameraRequestStatus.Active) return; Error = error; Status = CameraRequestStatus.Failed; }
            internal void End(CameraRequestStatus status) { if (Status == CameraRequestStatus.Active) Status = status; }
            void Release(CameraRequestStatus status) { if (Status != CameraRequestStatus.Active) return; director.Check(); End(status); }
            public void Complete() => Release(CameraRequestStatus.Completed);
            public void Cancel() => Release(CameraRequestStatus.Cancelled);
            public void Dispose() => Cancel();
        }
    }
}
