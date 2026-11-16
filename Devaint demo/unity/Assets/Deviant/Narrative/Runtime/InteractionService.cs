using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Narrative.Contracts;

namespace Deviant.Narrative
{
    // One explicit interaction agent. It does not auto-pick the nearest target,
    // bind a key, invent repeat restrictions or store story/economy state.
    public sealed class InteractionService : IDisposable
    {
        readonly IInteractionTargets targets;
        readonly INarrativeConditions conditions;
        readonly InteractionPolicy policy;
        readonly IDialogueCommandBindings commandBindings;
        DialogueSession current;
        bool disposed, busy;
        public IDialogueCommands Current => current;
        public InteractionService(IInteractionTargets targets, INarrativeConditions conditions, InteractionPolicy policy, IDialogueCommandBindings commandBindings = null)
        {
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets)); this.conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy)); this.commandBindings = commandBindings;
        }
        public IReadOnlyList<NpcTarget> Query(out string error)
        {
            error = null;
            if (disposed || busy) { error = "Interaction agent is ended or busy."; return Array.Empty<NpcTarget>(); }
            busy = true;
            try { return Array.AsReadOnly(targets.ReadTargets().Where(Eligible).OrderBy(t => t.PlacementId, StringComparer.Ordinal).ToArray()); }
            catch (Exception exception) { error = "Candidate query failed: " + exception.Message; return Array.Empty<NpcTarget>(); }
            finally { busy = false; }
        }
        public bool TryStart(NpcTarget selected, out IDialogueCommands session, out string error)
        {
            session = null; error = null;
            if (disposed || busy || selected == null) { error = "Interaction agent/selection is unavailable."; return false; }
            busy = true;
            try
            {
                current?.Poll();
                if (current != null && current.Read().Status == DialogueStatus.Active) { error = "This interaction agent already has an active dialogue."; return false; }
                if (!targets.TryRead(selected.WorldSessionId, selected.PlacementId, out var fresh) || fresh.Definition.Id != selected.Definition.Id || fresh.Definition.ContentHash != selected.Definition.ContentHash || !Eligible(fresh))
                { error = "Selected NPC is stale or no longer eligible."; return false; }
                if (!DialogueSession.TryStart(fresh, new Context(this, fresh), policy.AllowPlayerCancel, out var candidate, out error, commandBindings)) return false;
                current?.Dispose(); current = candidate; session = candidate; return true;
            }
            catch (Exception exception) { error = "Interaction start failed: " + exception.Message; return false; }
            finally { busy = false; }
        }
        bool Eligible(NpcTarget target) => target.Distance <= policy.MaxDistance && (policy.AllowInCombat || !target.InCombat) &&
            (target.Definition.ConditionId == null || conditions.Evaluate(target.Definition.ConditionId, target.WorldSessionId, target.PlacementId));
        public void Poll() { if (disposed || busy) return; busy = true; try { current?.Poll(); } finally { busy = false; } }
        public void Dispose()
        {
            if (disposed) return; if (busy) throw new InvalidOperationException("Interaction callbacks cannot reenter agent disposal.");
            disposed = true; current?.Dispose();
        }
        sealed class Context : IDialogueContext
        {
            readonly InteractionService service;
            readonly NpcTarget original;
            public Context(InteractionService service, NpcTarget target) { this.service = service; original = target; }
            public DialogueTargetState CheckTarget()
            {
                if (service.disposed || !service.targets.TryRead(original.WorldSessionId, original.PlacementId, out var target) || target.Definition.Id != original.Definition.Id) return DialogueTargetState.Missing;
                return !service.policy.RequireEligibilityWhileTalking || service.Eligible(target) ? DialogueTargetState.Available : DialogueTargetState.Ineligible;
            }
            public bool Evaluate(string conditionId) => service.conditions.Evaluate(conditionId, original.WorldSessionId, original.PlacementId);
        }
    }
}
