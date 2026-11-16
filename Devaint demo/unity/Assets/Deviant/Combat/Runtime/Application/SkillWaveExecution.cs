using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embedded privately by CombatCast. Never copy or box the mutable owner;
    // Native callbacks must observe and mutate this same field.
    public struct SkillWaveExecution
    {
        float x, y, previousX, previousY, travel;
        bool dead;
        public SkillWaveSnapshot Read() => new SkillWaveSnapshot(x, y, previousX, previousY, travel, dead);
        public void Activate<T>(float ownerX, float ownerY, SkillEffectDefinition effect, ref T host)
            where T : struct, ISkillWaveExecutionHost
        {
            x = ownerX + effect.X * host.Direction; y = ownerY + effect.Y;
            previousX = x; previousY = y;
            if (host.Blocked(x, y) || host.Occluded(x, y)) dead = true;
            else host.CreateVisual(x, y);
        }
        public void Step<T>(float step, SkillEffectDefinition effect, ref T host)
            where T : struct, ISkillWaveExecutionHost
        {
            if (!dead)
            {
                previousX = x; previousY = y;
                float move = Min(effect.Distance - travel, effect.Speed * step);
                float allowed = host.Travel(x, y, host.Direction, move);
                // Preserve Vector2.right * currentDirection * allowed, including
                // its float operation order. Direction is not a launch snapshot.
                int direction = host.Direction;
                x += 1f * direction * allowed; y += 0f * direction * allowed;
                travel += allowed;
                host.ApplyBox(previousX, previousY, x, y);
                // A throwing callback retains the moved state, but does not
                // advance the terminal claim or renderer. No rollback/retry.
                if (allowed < move - .0001f || travel >= effect.Distance - .0001f) dead = true;
                host.UpdateVisual(x, y);
            }
            if (dead) host.DestroyVisual();
        }
        public float RetainUntil(float last, float elapsed, float step)
            => !dead ? Max(last, elapsed + step * 2) : last;
        static float Min(float a, float b) => a < b ? a : b;
        static float Max(float a, float b) => a > b ? a : b;
    }
}
