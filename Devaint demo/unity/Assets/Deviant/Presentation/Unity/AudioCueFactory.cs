using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Unity
{
    // Bindings are injected, versioned Unity references, not a sound catalog.
    // CuePlayer remains the only owner of handles, clocks and completion.
    public sealed class AudioCueFactory : ICueViewFactory, IDisposable
    {
        sealed class Binding { public string Version; public AudioClip Clip; public float Pitch, Volume; public Func<bool> Held; public Func<float> TimeScale; }
        readonly Dictionary<string, Binding> latest = new Dictionary<string, Binding>(StringComparer.Ordinal);
        readonly HashSet<AudioCueOutputBinding> outputs = new HashSet<AudioCueOutputBinding>();
        readonly Transform outputRoot;
        readonly bool hasOutputRoot;
        bool disposed;
        public int LiveOutputCount => outputs.Count;
        public AudioCueFactory(Transform outputRoot = null)
        {
            this.outputRoot = outputRoot; hasOutputRoot = !ReferenceEquals(outputRoot, null);
        }

        public void Publish(AudioCueDefinition definition, AudioClip clip, float playbackRate = 1, float volume = 1)
        {
            Check();
            if (definition == null || !clip) throw new ArgumentException("An audio definition and injected clip are required.");
            if (clip.samples != definition.SampleCount || clip.frequency != definition.Frequency)
                throw new ArgumentException("Audio clip sample metadata must match the cue definition.", nameof(clip));
            if (float.IsNaN(playbackRate) || float.IsInfinity(playbackRate) || playbackRate <= 0 ||
                float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException(nameof(playbackRate));
            latest[definition.Id] = new Binding { Version = definition.ContentHash, Clip = clip, Pitch = playbackRate, Volume = volume };
        }

        // Live gameplay must let DSP read adjacent samples. The authoring
        // Publish API retains exact sampled/seek semantics for scrubbing.
        public void PublishContinuous(AudioCueDefinition definition, AudioClip clip, float playbackRate, float volume,
            Func<bool> held, Func<float> timeScale)
        {
            if (held == null || timeScale == null) throw new ArgumentNullException(nameof(held));
            Publish(definition, clip, playbackRate, volume);
            latest[definition.Id].Held = held; latest[definition.Id].TimeScale = timeScale;
        }

        public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
        {
            Check();
            if (!(definition is AudioCueDefinition audio))
            { resolved(CueViewResult.Failure("Audio factory requires an audio cue definition.")); return null; }
            if (!latest.TryGetValue(audio.Id, out var binding) || binding.Version != audio.ContentHash || !Available(audio, binding.Clip))
            { resolved(CueViewResult.Failure("Missing or unavailable audio binding for cue version: " + audio.Id)); return null; }
            return Begin(audio, binding.Clip, anchor, resolved, binding.Pitch, binding.Volume, binding.Held, binding.TimeScale);
        }

        internal Captured Capture(AudioCueDefinition definition, AudioClip clip)
        {
            Check();
            if (definition == null || !Available(definition, clip)) throw new ArgumentException("A loaded audio clip matching the cue sample metadata is required.");
            return new Captured(this, definition, clip);
        }
        static bool Available(AudioCueDefinition definition, AudioClip clip) => clip && clip.samples == definition.SampleCount &&
            clip.frequency == definition.Frequency && clip.loadState == AudioDataLoadState.Loaded;
        IDisposable Begin(AudioCueDefinition audio, AudioClip clip, CueAnchor anchor, Action<CueViewResult> resolved, float pitch = 1, float volume = 1,
            Func<bool> held = null, Func<float> timeScale = null)
        {
            var gameObject = new GameObject(audio.Id); gameObject.SetActive(false);
            AudioCueOutputBinding output = null;
            try
            {
                if (hasOutputRoot) gameObject.transform.SetParent(outputRoot, false);
                var source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
                gameObject.transform.position = new Vector3(anchor.X, anchor.Y, 0);
                output = gameObject.AddComponent<AudioCueOutputBinding>();
                output.Prepare(this, source, clip, audio, held, timeScale); outputs.Add(output);
                source.pitch = Mathf.Clamp(pitch, .01f, 3); source.volume = volume;
                resolved(CueViewResult.Ready(output));
            }
            catch
            {
                if (output) output.Dispose();
                else if (Application.isPlaying) Object.Destroy(gameObject); else Object.DestroyImmediate(gameObject);
                throw;
            }
            return null;
        }

        internal sealed class Captured : ICueViewFactory
        {
            readonly AudioCueFactory owner;
            readonly AudioCueDefinition definition;
            readonly AudioClip clip;
            internal Captured(AudioCueFactory owner, AudioCueDefinition definition, AudioClip clip)
            { this.owner = owner; this.definition = definition; this.clip = clip; }
            internal void Validate()
            {
                owner.Check();
                if (!Available(definition, clip)) throw new InvalidOperationException("Captured audio clip is unavailable: " + definition.Id);
            }
            public IDisposable Begin(ICueDefinition requested, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                Validate();
                if (!(requested is AudioCueDefinition) || requested.Id != definition.Id || requested.ContentHash != definition.ContentHash)
                { resolved(CueViewResult.Failure("Captured audio cue version does not match.")); return null; }
                return owner.Begin(definition, clip, anchor, resolved);
            }
        }

        internal void Return(AudioCueOutputBinding output) => outputs.Remove(output);
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var output in outputs.ToArray()) output.Dispose();
            latest.Clear();
        }
        void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(AudioCueFactory));
            if (hasOutputRoot && !outputRoot) throw new InvalidOperationException("The explicitly supplied Audio Cue output root is unavailable.");
        }
    }
}
