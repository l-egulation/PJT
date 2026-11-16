using System;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Unity
{
    // This is only one AudioSource output. It has no Update, clock, handle or
    // completion callback; CuePlayer alone samples and terminates the cue.
    [DisallowMultipleComponent]
    public sealed class AudioCueOutputBinding : MonoBehaviour, ICueView
    {
        AudioCueFactory owner;
        AudioSource source;
        GameObject root;
        AudioClip clip;
        AudioCueDefinition definition;
        bool prepared, activated, sampled, advancing, disposed;
        Func<bool> held;
        Func<float> timeScale;
        bool continuousStarted;
        float basePitch;
        public int SeekCount { get; private set; }
        float previousElapsed;
        public AudioSource Source => source;
        public bool IsDisposed => disposed;
        public bool IsAvailable => prepared && !disposed && this && enabled && source && source.enabled && clip &&
            source.clip == clip && clip.loadState == AudioDataLoadState.Loaded &&
            (!activated || isActiveAndEnabled && source.gameObject.activeInHierarchy);

        internal void Prepare(AudioCueFactory owner, AudioSource source, AudioClip clip, AudioCueDefinition definition,
            Func<bool> held = null, Func<float> timeScale = null)
        {
            if (prepared || disposed) throw new InvalidOperationException("Audio output has already been prepared or returned.");
            this.owner = owner; this.source = source; root = source.gameObject; this.clip = clip; this.definition = definition;
            this.held = held; this.timeScale = timeScale;
            source.playOnAwake = false; source.loop = definition.Loop; source.pitch = 1; source.clip = clip;
            prepared = true;
        }

        public void Sample(float elapsed)
        {
            if (!IsAvailable) throw new InvalidOperationException("Cue audio output was disabled, destroyed or lost its clip.");
            int position = definition.SampleAt(elapsed);
            if (!activated) { activated = true; gameObject.SetActive(true); }
            if (held != null) { SampleContinuous(elapsed, position); return; }
            bool advance = sampled && elapsed > previousElapsed;
            // DSP interpolates only between polls. A newly prepared output starts
            // held; an equal/backward elapsed pauses it and seeks exactly. It can
            // resume only after its owner observes positive clock advancement.
            // This is sampled synchronization, not sample-accurate DSP coupling.
            if (sampled && !advance) source.Pause();
            source.timeSamples = position; SeekCount++;
            if (advance)
            {
                if (!advancing) source.UnPause();
                if (!source.isPlaying) source.Play();
            }
            previousElapsed = elapsed; sampled = true; advancing = advance;
        }

        void SampleContinuous(float elapsed, int position)
        {
            if (!sampled) basePitch = source.pitch;
            // Seek only for initial placement or an explicit backwards clock.
            // Re-seeking on every fixed step repeats/skips PCM and creates clicks.
            if (!sampled || elapsed < previousElapsed)
            {
                if (continuousStarted) source.Pause();
                source.timeSamples = position; SeekCount++;
                continuousStarted = false; advancing = false;
            }
            previousElapsed = elapsed; sampled = true;
            float scale = timeScale();
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0) throw new InvalidOperationException("Invalid audio playback scale.");
            if (held() || scale == 0)
            {
                if (advancing) source.Pause();
                advancing = false; return;
            }
            source.pitch = Mathf.Clamp(basePitch * scale, .01f, 3);
            if (!continuousStarted) { source.Play(); continuousStarted = true; }
            else if (!advancing) source.UnPause();
            // A naturally finished one-shot stays finished. Polling must never
            // call Play again just because isPlaying is false at its tail.
            advancing = true;
        }

        public void Dispose() => Release();
        void OnDisable() { if (activated) Release(); }
        void OnDestroy() => Release();
        void Release()
        {
            if (disposed) return; disposed = true;
            owner?.Return(this);
            if (source)
            {
                source.Stop(); source.enabled = false; source.clip = null;
            }
            // Native destruction is deferred in Play Mode, but the source stops
            // and deactivates synchronously before any handle becomes terminal.
            if (root)
            {
                root.SetActive(false);
                if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
            }
        }
    }
}
