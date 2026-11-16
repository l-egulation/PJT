using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        Dictionary<string, ChargedMeleeDefinition> chargedMeleeDefinitions;
        public int ChargedMeleeCount => chargedMeleeDefinitions?.Count ?? 0;
        public ChargedMeleeDefinition GetChargedMelee(string id) => id != null && chargedMeleeDefinitions != null && chargedMeleeDefinitions.TryGetValue(id, out var definition) ? definition : null;
        bool InitializeChargedMelee()
        {
            var requested = new HashSet<string>(ChargedMeleeDefinition.SupportedIds, StringComparer.Ordinal);
            var next = new Dictionary<string, ChargedMeleeDefinition>(StringComparer.Ordinal);
            foreach (var form in catalog.forms)
            {
                if (form.specials == null) continue;
                foreach (var source in form.specials)
                {
                    if (source == null || !requested.Contains(source.id)) continue;
                    if (next.ContainsKey(source.id) || !ChargedMeleeDefinition.TryCreate(form.id, source, out var definition, out var error))
                    { Debug.LogError("Invalid charged melee definition: " + source.id); return false; }
                    next.Add(source.id, definition);
                }
            }
            if (next.Count != requested.Count) { Debug.LogError("Both migrated charged melee definitions are required."); return false; }
            chargedMeleeDefinitions = next; return true;
        }
        // Charge release, target admission and activation resources retain their
        // existing owners. Current casts pin their complete start definition.
        public bool TryApplyChargedMeleeDefinitions(IReadOnlyList<ChargedMeleeDefinition> candidates, bool resetScenario, out string error)
        {
            if (!Player || chargedMeleeDefinitions == null) { error = "Combat runtime is not initialized."; return false; }
            if (candidates == null || candidates.Count == 0 || candidates.Count > chargedMeleeDefinitions.Count) { error = "A nonempty unique subset of whole charged numeric candidates is required."; return false; }
            var next = new Dictionary<string, ChargedMeleeDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (candidate == null || !chargedMeleeDefinitions.TryGetValue(candidate.Id, out var current) || next.ContainsKey(candidate.Id) || current.SourceVersion != candidate.SourceVersion)
                { error = "Charged identity/resource/policy metadata changed. Reload the numeric candidate."; return false; }
                candidate.Policy.Resolve(candidate.Policy.Maximum);
                next.Add(candidate.Id, candidate);
            }
            foreach (var current in chargedMeleeDefinitions) if (!next.ContainsKey(current.Key)) next.Add(current.Key, current.Value);
            chargedMeleeDefinitions = next;
            if (resetScenario) ResetTest();
            error = null; return true;
        }
    }
}
