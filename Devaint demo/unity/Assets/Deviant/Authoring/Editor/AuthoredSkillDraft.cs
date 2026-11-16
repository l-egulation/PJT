using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class AuthoredSkillDraft : ScriptableObject
    {
        public SkillData[] sources;
        public SkillNumericDocument[] numerics;
        public void Load(AuthoredSkillDefinition[] values)
        {
            if (values == null || values.Length == 0 || values.Any(value => value == null)) throw new ArgumentException("A nonempty complete authored candidate set is required.");
            sources = values.Select(value => value.ToData()).ToArray(); numerics = values.Select(value => value.ToNumericData()).ToArray();
        }
        public AuthoredSkillDefinition Source(int index)
        {
            if (sources == null || index < 0 || index >= sources.Length || sources[index] == null) throw new ArgumentException("Reload the authored numeric draft.");
            string id = sources[index].id; string form = id.Substring(0, id.IndexOf('_')); return new AuthoredSkillDefinition(form, sources[index]);
        }
        public AuthoredSkillDefinition[] Compile()
        {
            if (sources == null || numerics == null || sources.Length == 0 || sources.Length != numerics.Length) throw new ArgumentException("Reload the whole authored numeric draft.");
            var values = new AuthoredSkillDefinition[sources.Length]; for (int index = 0; index < values.Length; index++) values[index] = Source(index).Recompile(numerics[index]);
            if (values.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new ArgumentException("Duplicate authored numeric candidate.");
            return values;
        }
    }
}
