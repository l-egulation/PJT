using System.Collections.Generic;

namespace Deviant.Combat.Execution
{
    // Scratch storage for one serialized tick-world owner, not actor state.
    // CombatSimulation rejects Step/Reset reentry before a phase can recapture it.
    public sealed class CombatTickSnapshot
    {
        readonly List<object> entries = new List<object>();
        public int Count => entries.Count;
        public object this[int index] => entries[index];

        // Copy every entry before invoking any actor, including nulls/duplicates.
        // Unity's destroyed-object check remains at each actual invocation.
        public void Capture<T>(List<T> source) where T : class
        {
            entries.Clear();
            int count = source.Count;
            if (entries.Capacity < count) entries.Capacity = count;
            for (int i = 0; i < count; i++) entries.Add(source[i]);
        }

        // Call in finally: keep warmed capacity, but never keep actor references
        // after the phase, including actors removed before a callback throws.
        public void Clear() => entries.Clear();
    }
}
