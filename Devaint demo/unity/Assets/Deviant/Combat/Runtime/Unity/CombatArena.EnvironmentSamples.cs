using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        const int EnvironmentSampleCapacity = 2048;
        readonly List<EnvironmentAttackSample> environmentSamples = new List<EnvironmentAttackSample>();
        readonly List<EnvironmentBodySweep> environmentBodies = new List<EnvironmentBodySweep>();
        int environmentObservers;
        // Valid only during SimulationCompleted for this arena. Cleared before each step,
        // including manual steps. No callbacks execute inside damage/sweep collection.
        public IReadOnlyList<EnvironmentAttackSample> CompletedEnvironmentAttacks => environmentSamples;
        public IReadOnlyList<EnvironmentBodySweep> CompletedEnvironmentBodies => environmentBodies;
        public bool EnvironmentSampleOverflow { get; private set; }
        public IDisposable ObserveEnvironmentContacts() { environmentObservers++; return new EnvironmentObservation(this); }
        sealed class EnvironmentObservation : IDisposable
        {
            CombatArena owner;
            internal EnvironmentObservation(CombatArena owner) { this.owner = owner; }
            public void Dispose() { if (owner) owner.environmentObservers--; owner = null; }
        }
        internal void RecordEnvironmentAttack(EnvironmentAttackSample sample)
        {
            if (environmentObservers == 0) return;
            if (environmentSamples.Count >= EnvironmentSampleCapacity) { EnvironmentSampleOverflow = true; return; }
            environmentSamples.Add(sample);
        }
        internal void RecordEnvironmentBody(EnvironmentBodySweep sample)
        {
            if (environmentObservers == 0) return;
            if (environmentBodies.Count >= EnvironmentSampleCapacity) { EnvironmentSampleOverflow = true; return; }
            environmentBodies.Add(sample);
        }
        void BeginEnvironmentStep() { environmentSamples.Clear(); environmentBodies.Clear(); EnvironmentSampleOverflow = false; }
    }
}
