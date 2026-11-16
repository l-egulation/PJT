using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class ChargedMeleeDraft : ScriptableObject
    {
        public SkillData[] sources;
        public ChargedMeleeNumericData[] numerics;
        public void Load(ChargedMeleeDefinition[] values)
        {
            if (values == null || values.Length == 0 || values.Any(value => value == null)) throw new ArgumentException("A nonempty whole charged candidate set is required.");
            sources = values.Select(value => value.ToData()).ToArray(); numerics = values.Select(value => value.ToNumericData()).ToArray();
        }
        public ChargedMeleeDefinition[] Compile()
        {
            if (sources == null || numerics == null || sources.Length == 0 || sources.Length != numerics.Length) throw new ArgumentException("Reload the whole charged numeric draft.");
            var result = new ChargedMeleeDefinition[sources.Length];
            for (int index = 0; index < result.Length; index++) {
                string id = sources[index]?.id; int separator = id?.LastIndexOf('_') ?? -1; string form = separator > 0 ? id.Substring(0, separator) : "";
                if (!ChargedMeleeDefinition.SupportedIds.Contains(id) || !ChargedMeleeDefinition.TryCreate(form, sources[index], out var source, out _)) throw new ArgumentException("Invalid charged draft source: " + id);
                result[index] = source.Recompile(numerics[index]);
            }
            if (result.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != result.Length) throw new ArgumentException("Duplicate charged candidate.");
            return result;
        }
    }
}
