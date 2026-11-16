using System;
using Deviant.CombatLab.Contracts;

namespace Deviant.CombatLab
{
    // Input-only preset for the existing fixed lab fixture. Initial world state,
    // equipment and A/B definition versions still belong to the scenario owner.
    public sealed class SwordDashInputSet
    {
        public const int AttackTicks = 60;
        public const int WallTicks = 20;
        public CombatInputRecording Attack { get; }
        public CombatInputRecording Wall { get; }
        public SwordDashInputSet(CombatInputRecording attack, CombatInputRecording wall)
        {
            Validate(attack, "attack", AttackTicks); Validate(wall, "wall", WallTicks);
            Attack = attack; Wall = wall;
        }
        internal static void Validate(CombatInputRecording recording, string segment, int count)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));
            if (recording.ScenarioId != SwordDashComparison.ScenarioId + "/" + segment || recording.StepSeconds != SwordDashComparison.Step || recording.TickCount != count)
                throw new ArgumentException("Input recording does not match the fixed experiment segment.", nameof(recording));
        }
    }
}
