using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Deviant.Combat.Contracts;

namespace Deviant.CombatLab.Contracts
{
    // Immutable tick intents, not a world save, action-success log or raw device
    // event stream. The scenario owner supplies initial state and runtime versions.
    public sealed class CombatInputRecording
    {
        public string ScenarioId { get; }
        public float StepSeconds { get; }
        public IReadOnlyList<CombatInput> Inputs { get; }
        public int TickCount => Inputs.Count;
        public string ContentHash { get; }
        public CombatInputRecording(string scenarioId, float stepSeconds, CombatInput[] inputs)
        {
            ScenarioId = ValidateScenario(scenarioId); StepSeconds = ValidateStep(stepSeconds);
            if (inputs == null || inputs.Length == 0) throw new ArgumentException("A recording requires at least one tick intent.", nameof(inputs));
            Inputs = Array.AsReadOnly((CombatInput[])inputs.Clone());
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(3); writer.Write(ScenarioId); writer.Write(StepSeconds); writer.Write(Inputs.Count);
                    foreach (var input in Inputs) { writer.Write(input.Axis); writer.Write((int)input.Pressed); writer.Write(input.SpecialHeld); writer.Write((int)input.Released); writer.Write(input.ClimbAxis); writer.Write((int)input.HeldSpecials); }
                }
                using (var hash = SHA256.Create()) ContentHash = BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public static string ValidateScenario(string scenarioId) => !string.IsNullOrWhiteSpace(scenarioId) ? scenarioId : throw new ArgumentException("A stable experiment scenario ID is required.", nameof(scenarioId));
        public static float ValidateStep(float stepSeconds) => !float.IsNaN(stepSeconds) && !float.IsInfinity(stepSeconds) && stepSeconds > 0 ? stepSeconds : throw new ArgumentOutOfRangeException(nameof(stepSeconds));
    }
}
