using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        CombatSimulation simulation;
        CombatControlBinding controls;
        public CombatControlBinding Controls => controls ?? (controls = new CombatControlBinding(this));
        public event Action Deactivated;
        // Environmental devices share the same fixed/manual simulation clock.
        public event Action<float> BeforePlayerStep;
        public event Action<float> PlayerStepCompleted;
        // Runs after all hit phases and clock advancement, including manual simulation.
        public event Action<float> SimulationStepCompleted;
        void OnDisable() { BeginEnvironmentStep(); HideInkStatusVisuals(); Audio?.Reset(); Deactivated?.Invoke(); }
        void OnDestroy() { ShieldPresentation?.Dispose(); ReleaseInkStatusVisuals(); Audio?.Dispose(); ReleaseCommands(); ReleaseReadModel(); ReleaseCharacterPresentation(); RangedPresentation?.Dispose(); ContactShadows?.Dispose(); controls?.Dispose(); }
        ManualSimulation manualSimulation;
        public bool ManualSimulationActive => manualSimulation != null;

        // One owner advances this arena; global time and other arenas' scheduling are untouched.
        public ManualSimulation AcquireManualSimulation()
        {
            if (initializing || CompositionProfile == CombatCompositionProfile.Unconfigured || !Player || !isActiveAndEnabled) throw new InvalidOperationException("Combat runtime is not initialized and active.");
            if (manualSimulation != null) throw new InvalidOperationException("This arena already has a manual simulation owner.");
            Player.ClearInputState();
            return manualSimulation = new ManualSimulation(this);
        }

        public sealed class ManualSimulation : IDisposable
        {
            CombatArena arena;
            bool executing;
            internal ManualSimulation(CombatArena arena) { this.arena = arena; }
            void Check()
            {
                if (!arena || arena.manualSimulation != this) throw new ObjectDisposedException(nameof(ManualSimulation));
                if (executing || !arena.isActiveAndEnabled) throw new InvalidOperationException("Simulation is inactive or already advancing.");
            }
            public void Step(float deltaTime, CombatInput input = default) => Advance(deltaTime, input);
            // Consume admitted input without replacing receipt provenance/order.
            public void StepPending(float deltaTime) => Advance(deltaTime, null);
            void Advance(float deltaTime, CombatInput? input)
            {
                Check();
                if (arena.Controls.Current.SimulationPaused) throw new InvalidOperationException("Simulation is paused by an active control request.");
                executing = true;
                try { arena.BeginFeedbackStep(); arena.simulation.Step(deltaTime, input); }
                finally { executing = false; }
                arena.NotifyCompletedStep(deltaTime);
            }
            // A new experiment epoch. Ordinary gameplay ResetTest retains its clock.
            public void Reset()
            {
                Check(); executing = true;
                try { arena.simulation.Reset(SimulationResetMode.NewEpoch); }
                finally { executing = false; }
            }
            public void Dispose()
            {
                if (executing) throw new InvalidOperationException("Cannot release an advancing simulation.");
                if (arena && arena.manualSimulation == this)
                {
                    if (arena.Player) arena.Player.ClearInputState();
                    arena.manualSimulation = null;
                }
                arena = null;
            }
        }
    }
}
