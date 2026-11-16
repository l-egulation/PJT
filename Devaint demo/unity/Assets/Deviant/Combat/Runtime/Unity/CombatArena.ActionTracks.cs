using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        Dictionary<string, ActionTrackDefinition> actionTracks;
        public int ActionTrackCount => actionTracks?.Count ?? 0;
        public ActionTrackDefinition GetActionTracks(string id) => id != null && actionTracks != null && actionTracks.TryGetValue(id, out var value) ? value : null;
        bool InitializeActionTracks(out string error)
        {
            try
            {
                var compiled = new Dictionary<string, ActionTrackDefinition>(StringComparer.Ordinal);
                foreach (var form in catalog.forms)
                    if (form?.specials != null) foreach (var skill in form.specials)
                    {
                        if (skill == null) continue;
                        bool supported = ActionTrackDefinition.Supports(skill.id);
                        if (ActionTrackDocument.IsAbsent(skill.actionTracks))
                        {
                            if (supported) throw new ArgumentException("Missing generated finite track source: " + skill.id + ". Export the combat catalog.");
                            continue;
                        }
                        if (!supported || compiled.ContainsKey(skill.id)) throw new ArgumentException("Unexpected or duplicate generated finite action: " + skill.id);
                        compiled.Add(skill.id, new ActionTrackDefinition(skill));
                    }
                if (compiled.Count != ActionTrackDefinition.SupportedIds.Count) throw new ArgumentException("Every migrated finite action source is required.");
                actionTracks = compiled; error = null; return true;
            }
            catch (ArgumentException exception) { error = "Invalid generated finite action tracks: " + exception.Message; return false; }
        }
        // Each supplied action is a complete immutable graph. Publish a nonempty
        // unique subset atomically, preserving every unselected action version.
        public bool TryApplyActionTracks(IReadOnlyList<ActionTrackDefinition> candidates, bool resetScenario, out string error)
        {
            if (!Player || actionTracks == null) { error = "Combat runtime is not initialized."; return false; }
            if (candidates == null || candidates.Count == 0 || candidates.Count > actionTracks.Count) { error = "A nonempty unique subset of complete finite actions is required."; return false; }
            if (specialFrames != null) return TryApplySpecialFiniteNumeric(candidates, resetScenario, out error);
            var replacement = new Dictionary<string, ActionTrackDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (candidate == null || !actionTracks.TryGetValue(candidate.Id, out var current) || replacement.ContainsKey(candidate.Id) || current.SourceVersion != candidate.SourceVersion)
                { error = "Finite action metadata changed or the candidate list is incomplete/duplicated. Reload before applying."; return false; }
                replacement.Add(candidate.Id, candidate);
            }
            foreach (var current in actionTracks) if (!replacement.ContainsKey(current.Key)) replacement.Add(current.Key, current.Value);
            actionTracks = replacement;
            if (resetScenario) ResetTest();
            error = null; return true;
        }
    }
}
