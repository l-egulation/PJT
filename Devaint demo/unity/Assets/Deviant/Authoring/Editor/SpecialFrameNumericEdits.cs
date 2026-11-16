using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;

namespace Deviant.Authoring
{
    // Source/API authority gate shared by the existing numeric editors. Loads
    // only the declared mode record; the numeric compilers remain nonrecursive.
    public static class SpecialFrameNumericEdits
    {
        public static void RequireAuthored(string repository, AuthoredSkillDefinition original, AuthoredSkillDefinition candidate)
        {
            if (original == null || candidate == null || original.Id != candidate.Id) throw new ArgumentException("One complete existing authored numeric policy is required.");
            if (SpecialFrameSource.HasFrameOverride(repository, original.Id)) SpecialFrameTimingGuards.RequireAuthoredNumericEdit(original, candidate);
        }
        public static void RequireFinite(string repository, ActionTrackDefinition original, ActionTrackDefinition candidate)
        {
            if (original == null || candidate == null || original.Id != candidate.Id) throw new ArgumentException("One complete existing finite numeric policy is required.");
            if (SpecialFrameSource.HasFrameOverride(repository, original.Id)) SpecialFrameTimingGuards.RequireFiniteNumericEdit(original, candidate);
        }
        public static bool CanEditAuthoredTime(string repository, AuthoredSkillDefinition original, string group, string field)
            => !SpecialFrameTimingGuards.OwnsAuthoredField(original, group, field) || !SpecialFrameSource.HasFrameOverride(repository, original.Id);
        public static bool CanEditFiniteTime(string repository, ActionTrackDefinition original)
            => !SpecialFrameSource.HasFrameOverride(repository, (original ?? throw new ArgumentNullException(nameof(original))).Id);
        public static void ValidateAuthoredCandidates(string repository, IReadOnlyList<AuthoredSkillDefinition> originals, IReadOnlyList<AuthoredSkillDefinition> candidates)
        {
            if (originals == null || candidates == null || candidates.Count == 0) throw new ArgumentException("Nonempty whole authored numeric candidates are required.");
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var original = candidate == null ? null : originals.SingleOrDefault(value => value.Id == candidate.Id);
                if (original == null || !selected.Add(candidate.Id)) throw new ArgumentException("Every selected authored numeric candidate must keep its original identity.");
                RequireAuthored(repository, original, candidate);
            }
        }
        public static void ValidateFiniteCandidates(string repository, IReadOnlyList<ActionTrackDefinition> originals, IReadOnlyList<ActionTrackDefinition> candidates)
        {
            if (originals == null || candidates == null || candidates.Count == 0) throw new ArgumentException("Nonempty whole finite numeric candidates are required.");
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var original = candidate == null ? null : originals.SingleOrDefault(value => value.Id == candidate.Id);
                if (original == null || !selected.Add(candidate.Id)) throw new ArgumentException("Every selected finite numeric candidate must keep its original identity.");
                RequireFinite(repository, original, candidate);
            }
        }
    }
}
