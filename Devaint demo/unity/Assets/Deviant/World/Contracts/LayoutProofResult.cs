using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.World.Contracts
{
    public sealed class LayoutWitnessResult
    {
        public string WitnessId { get; }
        public string ProfileHash { get; }
        public string InputHash { get; }
        public string TraceHash { get; }
        public int TickCount { get; }
        public bool Succeeded { get; }
        public string Error { get; }
        public LayoutWitnessResult(string witnessId, string profileHash, string inputHash, string traceHash, int tickCount, bool succeeded, string error)
        {
            if (tickCount < 0) throw new ArgumentOutOfRangeException(nameof(tickCount));
            WitnessId = LayoutValues.Id(witnessId); ProfileHash = LayoutValues.Id(profileHash); InputHash = LayoutValues.Id(inputHash);
            TraceHash = LayoutValues.Id(traceHash); TickCount = tickCount; Succeeded = succeeded; Error = error;
        }
    }
    public sealed class LayoutProofResult
    {
        public string LayoutHash { get; }
        public string ProfileHash { get; }
        public IReadOnlyList<LayoutWitnessResult> Witnesses { get; }
        public bool Succeeded => Witnesses.Count != 0 && Witnesses.All(witness => witness.Succeeded);
        public LayoutProofResult(string layoutHash, string profileHash, LayoutWitnessResult[] witnesses)
        {
            LayoutHash = LayoutValues.Id(layoutHash); ProfileHash = LayoutValues.Id(profileHash);
            Witnesses = Array.AsReadOnly((LayoutWitnessResult[])(witnesses ?? throw new ArgumentNullException(nameof(witnesses))).Clone());
        }
    }
}
