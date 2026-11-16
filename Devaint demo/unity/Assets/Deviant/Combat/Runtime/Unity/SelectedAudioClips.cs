using System;
using UnityEngine;

namespace Deviant.Combat
{
    // Explicit asset references keep the selected WAVs at their normal SFX/Music paths.
    public sealed class SelectedAudioClips : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string id; public AudioClip clip; }
        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        static SelectedAudioClips catalog;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { catalog = null; loaded = false; }

        public static AudioClip Find(string id)
        {
            if (!loaded) { catalog = Resources.Load<SelectedAudioClips>("SelectedAudioClips"); loaded = true; }
            if (!catalog) return null;
            foreach (var entry in catalog.entries)
                if (entry.id == id) return entry.clip ? entry.clip : Deviant.Media.CompressedAudioResources.LoadSelected(id);
            return null;
        }
    }
}
