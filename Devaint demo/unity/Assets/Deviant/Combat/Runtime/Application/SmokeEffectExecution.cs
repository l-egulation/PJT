using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embed privately; never copy or box this mutable effect owner. Cast retains
    // activation, cancellation, primary completion, saved pose and simulation time.
    // The actor alone owns its current concealment deadline, including exit grace.
    public struct SmokeEffectExecution
    {
        readonly SkillEffectDefinition definition;
        float end;
        bool spent;
        public float End => end;
        public bool Spent => spent;
        public SmokeEffectExecution(SkillEffectDefinition definition)
        { this.definition = definition; end = 0; spent = false; }
        public SmokeEffectSnapshot Read() => new SmokeEffectSnapshot(end, spent);
        public void Activate(float activation) { end = activation + definition.Duration; }
        public void MarkSpent() { spent = true; }
        public bool IsActive(float elapsed) => definition != null && definition.Type == "smoke" && elapsed < end;
        public bool TryRefresh(float elapsed, bool ownerOverlaps, float now, float step, out float until)
        {
            until = 0;
            if (spent || !ownerOverlaps || !IsActive(elapsed)) return false;
            // A false result supplies zero so the caller clears only smoke.
            // Keep the existing in-region refresh timing and operation order.
            until = now + step * 1.5f;
            return true;
        }
    }
}
