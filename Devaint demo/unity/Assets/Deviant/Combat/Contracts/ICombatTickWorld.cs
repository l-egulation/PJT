namespace Deviant.Combat.Contracts
{
    // These phases preserve the existing game's order during incremental migration.
    public enum CombatTickPhase
    {
        Player, Actions, ActionPush, Enemies, EnemyProjectiles, Bombs,
        PlayerProjectiles, Lasers, Bursts, Decoys, ExpiredFeedback
    }

    public enum SimulationResetMode { KeepTime, NewEpoch }

    public interface ICombatTickWorld
    {
        // Replace pending input for an explicit step; automatic ticks keep sampled input.
        void ApplyInput(CombatInput input);
        void Execute(CombatTickPhase phase, float deltaTime);
        // Reset actors/effects/resources, retaining equipment and definitions.
        // NewEpoch restarts input's epoch; both modes invalidate session tokens.
        void ResetState(SimulationResetMode mode);
    }
}
