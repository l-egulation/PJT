using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class DirectMeleeSpecialDraft : ScriptableObject
    {
        public SkillData[] sources;
        public BasicMeleeHitData[] numerics;
        public void Load(DirectMeleeSpecialDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0 || definitions.Any(value => value == null)) throw new ArgumentException("A nonempty direct candidate set is required.");
            sources = definitions.Select(value => value.ToData()).ToArray(); numerics = definitions.Select(value => {
                var data = value.ToBasicData(); return new BasicMeleeHitData { damage = data.damage, duration = data.duration, impact = data.impact, active = data.active, box = data.box };
            }).ToArray();
        }
        public DirectMeleeSpecialDefinition[] Compile()
        {
            if (sources == null || numerics == null || sources.Length == 0 || sources.Length != numerics.Length) throw new ArgumentException("Reload the complete direct numeric draft.");
            var values = new DirectMeleeSpecialDefinition[sources.Length];
            for (int index = 0; index < values.Length; index++) {
                string id = sources[index]?.id; int separator = id?.LastIndexOf('_') ?? -1;
                string form = id == "base_sword_default" ? "base_sword" : separator > 0 ? id.Substring(0, separator) : "";
                if (!DirectMeleeSpecialDefinition.SupportedIds.Contains(id) || !DirectMeleeSpecialDefinition.TryCreate(form, sources[index], out var source, out var error)) throw new ArgumentException("Invalid direct source: " + id);
                values[index] = source.Recompile(numerics[index]?.ToData());
            }
            if (values.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new ArgumentException("Duplicate direct numeric candidate.");
            return values;
        }
    }
}
