using System;
using System.Collections;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public sealed class ContinuousAudioPlaybackTests
    {
        sealed class Clock : ICueClock { public float Time; public float ReadElapsed() => Time; }
        sealed class PlaybackState { public bool Held; public float Scale = 1; public bool IsHeld() => Held; public float ReadScale() => Scale; }
        static AudioClip Tone(string name, int frames)
        {
            var clip = AudioClip.Create(name, frames, 1, 48000, false); var pcm = new float[frames];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = .2f * Mathf.Sin(2 * Mathf.PI * 437 * i / 48000f);
            clip.SetData(pcm, 0); return clip;
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator FixedStepJitterDoesNotSeekOrPauseAndExplicitPauseResumesInPlace()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var listener = new GameObject("ContinuousAudioListener", typeof(AudioListener)); var clip = Tone("ContinuousTone", 480000);
            var state = new PlaybackState();
            using (var factory = new AudioCueFactory()) using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("continuous"))
            {
                var d = new AudioCueDefinition("ContinuousAudioTest", "tone", clip.samples, clip.frequency);
                factory.PublishContinuous(d, clip, 1, 1, state.IsHeld, state.ReadScale); var clock = new Clock();
                var handle = owner.Play(d, new CueOccurrence(Guid.NewGuid(), d.Id), new CueAnchor(0, 0, 1), clock);
                var output = GameObject.Find(d.Id).GetComponent<AudioCueOutputBinding>(); var source = output.Source;
                Assert.That(source.isPlaying, Is.True, "Start on the event rather than the following fixed step.");
                for (int i = 0; i < 15; i++)
                {
                    clock.Time = (i / 2) * .02f; handle.Poll(); handle.Poll();
                    Assert.That(source.isPlaying, Is.True, "Repeated/fixed-clock polls must preserve DSP playback.");
                    yield return new WaitForSecondsRealtime(.01f);
                }
                Assert.That(source.timeSamples, Is.GreaterThan(1000));
                Assert.That(output.SeekCount, Is.EqualTo(1), "Only initial placement may seek during monotonic gameplay.");
                state.Held = true; handle.Poll(); yield return new WaitForSecondsRealtime(.05f); int stopped = source.timeSamples;
                Assert.That(source.isPlaying, Is.False); yield return new WaitForSecondsRealtime(.05f); handle.Poll();
                Assert.That(source.timeSamples, Is.EqualTo(stopped));
                state.Held = false; state.Scale = .5f; handle.Poll(); Assert.That(source.pitch, Is.EqualTo(.5f));
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(source.timeSamples, Is.GreaterThan(stopped)); Assert.That(output.SeekCount, Is.EqualTo(1));
                handle.Cancel(); Assert.That(source.isPlaying, Is.False); Assert.That(factory.LiveOutputCount, Is.Zero);
            }
            Object.Destroy(clip); Object.Destroy(listener); yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator FinishedOneShotDoesNotRestartWhenSimulationClockStalls()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var listener = new GameObject("OneShotListener", typeof(AudioListener)); var clip = Tone("ShortTone", 4800);
            using (var factory = new AudioCueFactory()) using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("tail"))
            {
                var d = new AudioCueDefinition("FinishedContinuousAudio", "tone", clip.samples, clip.frequency);
                factory.PublishContinuous(d, clip, 1, 1, () => false, () => 1); var clock = new Clock();
                var handle = owner.Play(d, new CueOccurrence(Guid.NewGuid(), d.Id), new CueAnchor(0, 0, 1), clock);
                var output = GameObject.Find(d.Id).GetComponent<AudioCueOutputBinding>(); var source = output.Source;
                yield return new WaitForSecondsRealtime(.3f); Assert.That(source.isPlaying, Is.False);
                clock.Time = .04f; handle.Poll(); Assert.That(source.isPlaying, Is.False, "No repeated attack transient after DSP completion.");
                Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing), "CuePlayer still owns lifecycle completion.");
                clock.Time = .1f; handle.Poll(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed));
                Assert.That(factory.LiveOutputCount, Is.Zero);
            }
            Object.Destroy(clip); Object.Destroy(listener); yield return new ExitPlayMode();
        }
    }
}
