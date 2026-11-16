using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public sealed class CombatSimulation
    {
        static readonly CombatTickPhase[] phases = {
            CombatTickPhase.Player, CombatTickPhase.Actions, CombatTickPhase.ActionPush,
            CombatTickPhase.Enemies, CombatTickPhase.EnemyProjectiles, CombatTickPhase.Bombs,
            CombatTickPhase.PlayerProjectiles, CombatTickPhase.Lasers, CombatTickPhase.Bursts, CombatTickPhase.Decoys
        };
        readonly ICombatTickWorld world;
        CombatInputIngress inputIngress;
        bool busy, preparingReset;
        public SimulationClock Clock { get; } = new SimulationClock();
        public bool IsFaulted { get; private set; }
        public CombatSimulation(ICombatTickWorld world) { this.world = world ?? throw new ArgumentNullException(nameof(world)); }
        public void BindInputIngress(CombatInputIngress ingress)
        {
            CheckIdle();
            if (ingress == null) throw new ArgumentNullException(nameof(ingress));
            if (inputIngress != null) throw new InvalidOperationException("This simulation already owns an input ingress.");
            inputIngress = ingress;
        }

        public void Step(float deltaTime, CombatInput? input = null)
        {
            CheckIdle();
            if (IsFaulted) throw new InvalidOperationException("Reset the simulation after a failed step or reset.");
            Clock.ValidateStep(deltaTime); // No input/state mutation for rejected requests.
            busy = true;
            long entryTick = Clock.TickCount; float entryNow = Clock.Now; bool completed = false;
            try
            {
                inputIngress?.BeginTick();
                if (input.HasValue) world.ApplyInput(input.Value);
                foreach (var phase in phases) world.Execute(phase, deltaTime);
                Clock.Advance(deltaTime);
                world.Execute(CombatTickPhase.ExpiredFeedback, deltaTime);
                completed = true;
            }
            catch { IsFaulted = true; throw; }
            finally
            {
                try { inputIngress?.CompleteTick(entryTick, entryNow, Clock.Now, completed); }
                finally { busy = false; }
            }
        }
        public void ValidateResetAdmission()
        {
            if (busy) throw new InvalidOperationException("Simulation steps and resets cannot reenter one another.");
        }
        public void BeginResetPreparation()
        {
            CheckIdle(); preparingReset = true;
        }
        public void EndResetPreparation() { preparingReset = false; }
        public void Reset(SimulationResetMode mode)
        {
            CheckIdle();
            if (mode != SimulationResetMode.KeepTime && mode != SimulationResetMode.NewEpoch)
                throw new ArgumentOutOfRangeException(nameof(mode));
            busy = true;
            try
            {
                if (mode == SimulationResetMode.NewEpoch) Clock.Reset();
                world.ResetState(mode);
                IsFaulted = false;
            }
            catch { IsFaulted = true; throw; }
            finally { busy = false; }
        }
        void CheckIdle()
        {
            if (busy || preparingReset) throw new InvalidOperationException("Simulation steps, resets and movement preflight cannot reenter one another.");
        }
    }
}
