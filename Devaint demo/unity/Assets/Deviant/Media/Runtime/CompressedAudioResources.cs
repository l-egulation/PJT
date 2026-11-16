using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Media
{
    public static class CompressedAudioResources
    {
        [Serializable] public sealed class Entry { public string id, resource, payload, name; }
        [Serializable] public sealed class Catalog { public int schema; public Entry[] entries; }
        static Catalog catalog;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        public static int DecodedClipCount => clips.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { catalog = null; clips.Clear(); }

        static Catalog Data()
        {
            if (catalog != null) return catalog;
            var data = Resources.Load<TextAsset>("CompressedAudio/Catalog");
            if (!data) return null;
            catalog = JsonUtility.FromJson<Catalog>(data.text);
            if (catalog.schema != 1 || catalog.entries == null) throw new InvalidOperationException("Invalid compressed audio catalog");
            return catalog;
        }
        public static AudioClip LoadSelected(string id)
        {
            foreach (var entry in Data()?.entries ?? Array.Empty<Entry>()) if (entry.id == id) return Load(entry);
            return null;
        }
        public static AudioClip Load(string resource)
        {
#if UNITY_EDITOR
            // Editor authoring and waveform inspections keep their original AudioClip assets.
            return Resources.Load<AudioClip>(resource);
#else
            foreach (var entry in Data()?.entries ?? Array.Empty<Entry>()) if (entry.resource == resource) return Load(entry);
            return Resources.Load<AudioClip>(resource);
#endif
        }
        public static AudioClip Load(Entry entry)
        {
            if (clips.TryGetValue(entry.payload, out var cached) && cached) return cached;
            var encoded = Resources.Load<TextAsset>(entry.payload);
            if (!encoded) throw new InvalidOperationException("Compressed audio payload missing: " + entry.payload);
            var pcm = OggOpusDecoder.Decode(encoded.bytes);
            var clip = AudioClip.Create(entry.name, pcm.Frames, pcm.Channels, OggOpusDecoder.Pcm.Frequency, false);
            if (!clip.SetData(pcm.Samples, 0)) { UnityEngine.Object.Destroy(clip); throw new InvalidOperationException("Cannot upload decoded Opus audio: " + entry.name); }
            clips[entry.payload] = clip;
            return clip;
        }
    }
}
