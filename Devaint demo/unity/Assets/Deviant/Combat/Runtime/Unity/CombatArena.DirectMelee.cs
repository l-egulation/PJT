using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        Dictionary<string, DirectMeleeSpecialDefinition> directMeleeSpecials;
        public int DirectMeleeSpecialCount => directMeleeSpecials?.Count ?? 0;
        public DirectMeleeSpecialDefinition GetDirectMeleeSpecial(string actionId) => actionId != null && directMeleeSpecials != null && directMeleeSpecials.TryGetValue(actionId, out var definition) ? definition : null;
        bool InitializeDirectMeleeSpecials()
        {
            var requested = new HashSet<string>(DirectMeleeSpecialDefinition.SupportedIds, StringComparer.Ordinal);
            var next = new Dictionary<string, DirectMeleeSpecialDefinition>(StringComparer.Ordinal);
            foreach (var form in catalog.forms)
            {
                if (form.specials == null) continue;
                foreach (var source in form.specials)
                {
                    if (source == null || !requested.Contains(source.id)) continue;
                    if (next.ContainsKey(source.id) || !DirectMeleeSpecialDefinition.TryCreate(form.id, source, out var definition, out var error))
                    { Debug.LogError("Invalid direct melee special: " + source.id); return false; }
                    next.Add(source.id, definition);
                }
            }
            if (next.Count != requested.Count) { Debug.LogError("The three migrated direct melee specials are required."); return false; }
            directMeleeSpecials = next; return true;
        }
        // Numeric candidate publication never reconfigures Equipment resources.
        // Every selected action validates before any publication or reset.
        public bool TryApplyDirectMeleeSpecials(IReadOnlyList<DirectMeleeSpecialDefinition> candidates, bool resetScenario, out string error)
        {
            if (!Player || directMeleeSpecials == null) { error = "Combat runtime is not initialized."; return false; }
            if (candidates == null || candidates.Count == 0 || candidates.Count > directMeleeSpecials.Count) { error = "A nonempty unique subset of direct melee numeric candidates is required."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var replacement = new Dictionary<string, DirectMeleeSpecialDefinition>(StringComparer.Ordinal);
                foreach (var candidate in candidates)
                {
                    if (candidate == null || !directMeleeSpecials.TryGetValue(candidate.Id, out var current) || replacement.ContainsKey(candidate.Id) || candidate.SourceVersion != current.SourceVersion)
                    { error = "Direct melee identity/resource/policy metadata changed. Reload the complete numeric candidate."; return false; }
                    var frame = GetMeleeFrame(candidate.Id, false);
                    if (frame?.Mode == MeleeTimingMode.FrameTime && (candidate.Impact != current.Impact || candidate.Active != current.Active || candidate.Duration != current.Duration))
                    { error = "Active direct FrameTime timing is owned by action markers; return to fixed mode before numeric time edits."; return false; }
                    candidate.Compile();
                    replacement.Add(candidate.Id, candidate);
                }
                foreach (var current in directMeleeSpecials) if (!replacement.ContainsKey(current.Key)) replacement.Add(current.Key, current.Value);
                if (!TryRecompileMeleeFrameNumerics(null, null, replacement.Values, out var frames, out error)) return false;
                if (resetScenario) simulation.ValidateResetAdmission();
                directMeleeSpecials = replacement;
                PublishMeleeFrameNumerics(frames);
            }
            catch (InvalidOperationException exception) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            error = null; return true;
        }
    }
}
