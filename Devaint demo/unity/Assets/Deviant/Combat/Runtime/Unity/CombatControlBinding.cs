using System;
using Deviant.Control;
using Deviant.Control.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Composition-owned adapter. The session remains authoritative for input and
    // simulation ticks; only the main game additionally binds Unity's global time.
    public sealed class CombatControlBinding : IControlRequests, IDisposable
    {
        readonly ControlSession session = new ControlSession();
        readonly CombatArena arena;
        EnginePause engine;
        bool disposed;
        bool combatBlocked, paused;
        public ControlState Current => session.Current;
        public bool EngineTimeBound => engine != null;
        public void AdvanceAccent(float realDelta) { session.AdvanceAccent(realDelta); Synchronize(); }
        public CombatControlBinding(CombatArena arena) { this.arena = arena; }
        public IControlScope OpenScope(string owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CombatControlBinding));
            return new Scope(this, session.OpenScope(owner));
        }
        // One explicit global time adapter; other arenas' local requests never
        // write Time.timeScale. The owner binds this at game composition time.
        public IDisposable BindEngineTime()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CombatControlBinding));
            if (engine != null) throw new InvalidOperationException("This session already binds engine time.");
            engine = EnginePause.Acquire(this); Synchronize(); return engine;
        }
        void Synchronize()
        {
            bool blocked = Current.Blocks(InputChannels.Combat);
            if (combatBlocked != blocked || paused != Current.SimulationPaused)
            {
                combatBlocked = blocked; paused = Current.SimulationPaused;
                if (arena && arena.Player) arena.Player.ClearInputState();
            }
            engine?.Apply(Current.SimulationScale);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; session.Dispose(); Synchronize(); engine?.Dispose();
        }
        sealed class Scope : IFeedbackTimeScope
        {
            readonly CombatControlBinding binding;
            readonly IControlScope scope;
            public Scope(CombatControlBinding binding, IControlScope scope) { this.binding = binding; this.scope = scope; }
            public bool IsActive => scope.IsActive;
            public IControlLease BlockInput(InputChannels channels) => Wrap(scope.BlockInput(channels));
            public IControlLease PauseSimulation() => Wrap(scope.PauseSimulation());
            public IControlLease RequestSlow(float scale, float accentSeconds)
                => Wrap(((IFeedbackTimeScope)scope).RequestSlow(scale, accentSeconds));
            IControlLease Wrap(IControlLease lease) { binding.Synchronize(); return new Lease(binding, lease); }
            public void Dispose() { scope.Dispose(); binding.Synchronize(); }
        }
        sealed class Lease : IControlLease
        {
            readonly CombatControlBinding binding;
            readonly IControlLease lease;
            public Lease(CombatControlBinding binding, IControlLease lease) { this.binding = binding; this.lease = lease; }
            public bool IsActive => lease.IsActive;
            public void Dispose() { lease.Dispose(); binding.Synchronize(); }
        }
        sealed class EnginePause : IDisposable
        {
            static EnginePause authority;
            readonly CombatControlBinding binding;
            bool disposed;
            readonly float baseTimeScale;
            float appliedScale = 1;
            EnginePause(CombatControlBinding binding) { this.binding = binding; baseTimeScale = Time.timeScale; }
            public static EnginePause Acquire(CombatControlBinding binding)
            {
                if (authority != null) throw new InvalidOperationException("Only one composition may bind Unity global time.");
                authority = new EnginePause(binding); return authority;
            }
            public void Apply(float value)
            {
                if (disposed || appliedScale == value) return;
                Time.timeScale = baseTimeScale * value; appliedScale = value;
            }
            public void Dispose()
            {
                if (disposed) return;
                Apply(1); disposed = true;
                if (ReferenceEquals(authority, this)) authority = null;
                if (ReferenceEquals(binding.engine, this)) binding.engine = null;
            }
        }
    }
}
