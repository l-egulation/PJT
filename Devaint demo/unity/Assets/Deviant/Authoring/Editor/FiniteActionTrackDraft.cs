using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class FiniteActionTrackDraft : ScriptableObject
    {
        public SkillData[] sources;
        public ActionTrackDocument[] tracks;
        public ActionTrackNumericDocument[] numerics;
        public void Load(ActionTrackDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0 || definitions.Any(value => value == null) || definitions.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
                throw new ArgumentException("A unique nonempty set of complete finite actions is required.");
            sources = definitions.Select(definition => definition.ToData()).ToArray();
            tracks = definitions.Select(definition => definition.ToDocument()).ToArray();
            numerics = definitions.Select(definition => definition.ToNumericData()).ToArray();
        }
        public ActionTrackDefinition[] Compile()
        {
            if (sources == null || tracks == null || numerics == null || sources.Length == 0 || tracks.Length != sources.Length || numerics.Length != sources.Length)
                throw new ArgumentException("Reload the complete finite action source.");
            var result = new ActionTrackDefinition[sources.Length];
            for (int index = 0; index < result.Length; index++) result[index] = new ActionTrackDefinition(sources[index]).Recompile(tracks[index], numerics[index]);
            if (result.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != result.Length) throw new ArgumentException("Duplicate finite action candidate.");
            return result;
        }
    }
}
