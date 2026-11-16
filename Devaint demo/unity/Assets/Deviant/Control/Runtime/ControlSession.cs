using System;
using System.Collections.Generic;
using Deviant.Control.Contracts;

namespace Deviant.Control
{
    // Single simulation/session lifetime. No Unity globals or observer callbacks.
    // Input is unioned, pause wins, and independent slows select the minimum.
    public sealed class ControlSession : IControlRequests, IDisposable
    {
        readonly List<Scope> scopes = new List<Scope>();
        bool disposed;
        public ControlState Current { get; private set; }
        public IControlScope OpenScope(string owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ControlSession));
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A diagnostic owner is required.", nameof(owner));
            var scope = new Scope(this, owner); scopes.Add(scope); return scope;
        }
        void Refresh()
        {
            InputChannels blocked = InputChannels.None; bool paused = false; float scale = 1;
            foreach (var scope in scopes) foreach (var lease in scope.leases)
            { blocked |= lease.channels; paused |= lease.pause; scale = Math.Min(scale, lease.scale); }
            if (blocked != Current.Blocked || paused != Current.SimulationPaused || (!paused && scale != Current.SimulationScale))
                Current = new ControlState(blocked, paused, Current.Revision + 1, scale);
        }
        public void AdvanceAccent(float realDelta)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ControlSession));
            if (float.IsNaN(realDelta) || float.IsInfinity(realDelta) || realDelta < 0) throw new ArgumentOutOfRangeException(nameof(realDelta));
            if (Current.SimulationPaused || realDelta == 0) return;
            foreach (var scope in scopes)
                for (int i = scope.leases.Count - 1; i >= 0; i--)
                {
                    var lease = scope.leases[i];
                    if (lease.remaining < 0) continue;
                    lease.remaining -= realDelta;
                    if (lease.remaining <= 0) { lease.released = true; scope.leases.RemoveAt(i); }
                }
            Refresh();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var scope in scopes.ToArray()) scope.Dispose();
        }
        sealed class Scope : IFeedbackTimeScope
        {
            readonly ControlSession session;
            // A name is diagnostic only; identity is this scope, never string equality.
            public readonly string owner;
            public readonly List<Lease> leases = new List<Lease>();
            public bool IsActive { get; private set; } = true;
            public Scope(ControlSession session, string owner) { this.session = session; this.owner = owner; }
            public IControlLease BlockInput(InputChannels channels)
            {
                Check();
                const InputChannels all = InputChannels.Combat | InputChannels.Interaction | InputChannels.Dialogue | InputChannels.Navigation;
                if (channels == InputChannels.None || (channels & ~all) != 0) throw new ArgumentOutOfRangeException(nameof(channels));
                return Add(channels, false);
            }
            public IControlLease PauseSimulation() { Check(); return Add(InputChannels.None, true); }
            public IControlLease RequestSlow(float scale, float accentSeconds)
            {
                Check();
                if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0 || scale > 1 ||
                    float.IsNaN(accentSeconds) || float.IsInfinity(accentSeconds) || accentSeconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(scale));
                var lease = new Lease(this, InputChannels.None, false, scale, accentSeconds);
                leases.Add(lease); session.Refresh(); return lease;
            }
            Lease Add(InputChannels channels, bool pause)
            {
                var lease = new Lease(this, channels, pause); leases.Add(lease); session.Refresh(); return lease;
            }
            public void Release(Lease lease) { leases.Remove(lease); session.Refresh(); }
            public void Dispose()
            {
                if (!IsActive) return;
                IsActive = false; leases.Clear(); session.scopes.Remove(this); session.Refresh();
            }
            void Check() { if (!IsActive) throw new ObjectDisposedException(nameof(IControlScope)); }
        }
        sealed class Lease : IControlLease
        {
            readonly Scope scope;
            internal bool released;
            public readonly InputChannels channels;
            public readonly bool pause;
            public readonly float scale;
            public float remaining;
            public bool IsActive => !released && scope.IsActive;
            public Lease(Scope scope, InputChannels channels, bool pause, float scale = 1, float remaining = -1)
            { this.scope = scope; this.channels = channels; this.pause = pause; this.scale = scale; this.remaining = remaining; }
            public void Dispose()
            {
                if (!IsActive) return;
                released = true; scope.Release(this);
            }
        }
    }
}
