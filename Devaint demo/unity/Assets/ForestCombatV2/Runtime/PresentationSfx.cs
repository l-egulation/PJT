using System;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Reused item/UI sound effects for the village and chapter presentation events (resources/authoring/needed-assets
    // audio-reuse.json). The asset (Resources/PresentationSfx) references the existing WAVs under
    // Assets/Deviant/Audio/SFX by GUID, so nothing is copied. One 2D source plays each event once when the caller's
    // committed event happens; it never loops and is never driven from a per-frame draw.
    [CreateAssetMenu(menuName = "Deviant/Presentation SFX")]
    public sealed class PresentationSfx : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string id; public AudioClip clip; [Range(0, 1)] public float volume = .8f; }
        public Entry[] entries;

        public const string Unlock = "unlock", PigmentPickup = "pigment-pickup", AltarAppear = "altar-appear", AltarAcquire = "altar-acquire",
            AltarDiscard = "altar-discard", InkDestroy = "ink-destroy", ExitOpen = "exit-open";

        static PresentationSfx catalog;
        static AudioSource source;
        public static string LastPlayed { get; private set; }
        public static int PlayCount { get; private set; }
        static readonly System.Collections.Generic.Dictionary<string, int> counts = new System.Collections.Generic.Dictionary<string, int>();
        public static int CountOf(string id) => counts.TryGetValue(id, out var n) ? n : 0;
        static float lastCurrencySound = float.NegativeInfinity, lastPigmentSound = float.NegativeInfinity;
        public static void PlayCurrencyPickup() => PlayCurrencyPickup("gold-pickup", ref lastCurrencySound);
        public static void PlayPigmentPickup() => PlayCurrencyPickup(PigmentPickup, ref lastPigmentSound);

        static void PlayCurrencyPickup(string id, ref float lastSound)
        {
            // Multiple drops of the same type can pop on one frame.
            if (Time.unscaledTime - lastSound < .04f) return;
            lastSound = Time.unscaledTime;
            Play(id);
        }

        public static void Play(string id)
        {
            if (!catalog) catalog = Resources.Load<PresentationSfx>("PresentationSfx");
            var entry = catalog ? Array.Find(catalog.entries, e => e.id == id) : null;
            LastPlayed = id; PlayCount++; counts[id] = CountOf(id) + 1;
            PlayClip(entry != null && entry.clip ? entry.clip : Deviant.Media.CompressedAudioResources.LoadSelected("presentation:" + id), entry?.volume ?? .8f);
        }

        public static void PlaySelected(string id) { counts[id] = CountOf(id) + 1; PlayClip(SelectedAudioClips.Find(id), .8f); }

        static void PlayClip(AudioClip clip, float volume)
        {
            if (!clip) return;
            if (!source)
            {
                var go = new GameObject("Presentation SFX"); DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.ignoreListenerPause = true;
            }
            source.PlayOneShot(clip, volume);
        }
    }
}
