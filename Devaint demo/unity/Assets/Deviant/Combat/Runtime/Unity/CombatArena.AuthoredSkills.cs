using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        Dictionary<string, AuthoredSkillDefinition> authoredSkills;
        public int AuthoredSkillCount => authoredSkills?.Count ?? 0;
        public AuthoredSkillDefinition GetAuthoredSkill(string id) => id != null && authoredSkills != null && authoredSkills.TryGetValue(id, out var value) ? value : null;
        bool InitializeAuthoredSkills(out string error)
        {
            var next = new Dictionary<string, AuthoredSkillDefinition>(StringComparer.Ordinal);
            foreach (var form in catalog.forms) if (form?.specials != null) foreach (var skill in form.specials) {
                if (skill == null || !AuthoredSkillDefinition.Supports(skill.id)) continue;
                if (next.ContainsKey(skill.id)) { error = "Duplicate authored skill: " + skill.id; return false; }
                if (!AuthoredSkillDefinition.TryCreate(form.id, skill, out var value, out error)) { error = "Invalid authored skill: " + skill.id + ". " + error; return false; }
                next.Add(skill.id, value);
            }
            if (next.Count != AuthoredSkillDefinition.SupportedIds.Count) { error = "Every supported authored melee skill is required."; return false; }
            authoredSkills = next; error = null; return true;
        }
        public bool TryApplyAuthoredSkills(IReadOnlyList<AuthoredSkillDefinition> candidates, bool resetScenario, out string error)
        {
            if (!Player || authoredSkills == null) { error = "Combat runtime is not initialized."; return false; }
            if (candidates == null || candidates.Count == 0 || candidates.Count > authoredSkills.Count) { error = "A nonempty unique subset of complete authored numeric candidates is required."; return false; }
            if (specialFrames != null) return TryApplySpecialAuthoredNumeric(candidates, resetScenario, out error);
            var next = new Dictionary<string, AuthoredSkillDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates) {
                if (candidate == null || !authoredSkills.TryGetValue(candidate.Id, out var current) || next.ContainsKey(candidate.Id) || current.SourceVersion != candidate.SourceVersion) { error = "Authored identity/resource/fixed policy metadata changed. Reload before applying."; return false; }
                next.Add(candidate.Id, candidate);
            }
            foreach (var current in authoredSkills) if (!next.ContainsKey(current.Key)) next.Add(current.Key, current.Value);
            authoredSkills = next; if (resetScenario) ResetTest(); error = null; return true;
        }
    }
}
